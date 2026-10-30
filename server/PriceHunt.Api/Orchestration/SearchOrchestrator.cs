using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Models;
using PriceHunt.Api.Streaming;
using PriceHunt.Api.Suppliers;

namespace PriceHunt.Api.Orchestration;

internal sealed record SupplierOutcome(string SupplierName, QuoteStatus Status, decimal Price, int ResponseTimeMs, DateTime At);

/// <summary>
/// Fans out one search. Each result is saved, then sent, before the next one is sent.
/// The database is touched only by the reader loop: the context is not thread-safe.
/// </summary>
public sealed class SearchOrchestrator
{
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(6);

    private readonly ILogger<SearchOrchestrator> _logger;

    public SearchOrchestrator(ILogger<SearchOrchestrator> logger) => _logger = logger;

    public async Task RunAsync(
        SearchRun run,
        IReadOnlyList<ISupplier> suppliers,
        Func<SseEvent, Task> emit,
        PriceHuntDbContext db,
        CancellationToken clientToken,
        TimeSpan? deadline = null)
    {
        var request = new SearchRequest(run.FromLocation, run.ToLocation, run.FromDate, run.ToDate);

        using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(clientToken);
        deadlineSource.CancelAfter(deadline ?? Deadline);

        var channel = Channel.CreateUnbounded<SupplierOutcome>(new UnboundedChannelOptions { SingleReader = true });
        var workers = suppliers
            .Select(supplier => RunOneAsync(supplier, request, channel.Writer, deadlineSource.Token))
            .ToArray();
        var closed = CloseWhenSettledAsync(workers, channel.Writer);

        var responded = 0;
        var unanswered = false;

        try
        {
            await emit(new SseEvent("run_started",
                new RunStartedDto(run.Id, suppliers.Count, suppliers.Select(supplier => supplier.Name).ToArray())));

            await foreach (var outcome in channel.Reader.ReadAllAsync(clientToken))
            {
                Tally(outcome, ref responded);
                unanswered |= IsUnanswered(outcome.Status);

                await PersistAsync(db, run, outcome, responded);
                await emit(new SseEvent("quote", new QuoteDto(
                    outcome.SupplierName,
                    outcome.Price,
                    outcome.ResponseTimeMs,
                    outcome.Status.ToString(),
                    outcome.At)));
                await emit(new SseEvent("progress", new ProgressDto(responded, suppliers.Count)));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
        finally
        {
            await closed;

            while (channel.Reader.TryRead(out var pending))
            {
                Tally(pending, ref responded);
                unanswered |= IsUnanswered(pending.Status);
                await PersistAsync(db, run, pending, responded);
            }

            var status = clientToken.IsCancellationRequested
                ? SearchStatus.Cancelled
                : unanswered
                    ? SearchStatus.TimedOut
                    : SearchStatus.Completed;

            await CloseRunAsync(db, run, status, responded);
            await EmitCompletionAsync(run, responded, suppliers.Count, emit, clientToken);
        }
    }

    private static async Task RunOneAsync(
        ISupplier supplier,
        SearchRequest request,
        ChannelWriter<SupplierOutcome> writer,
        CancellationToken deadlineToken)
    {
        var stopwatch = Stopwatch.StartNew();

        SupplierOutcome outcome;
        try
        {
            var price = await supplier.GetPriceAsync(request, deadlineToken);
            outcome = new SupplierOutcome(supplier.Name, QuoteStatus.Success, price, Elapsed(stopwatch), DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            var status = supplier.NeverResponds ? QuoteStatus.NoResponse : QuoteStatus.Timeout;
            outcome = new SupplierOutcome(supplier.Name, status, 0m, Elapsed(stopwatch), DateTime.UtcNow);
        }
        catch (Exception)
        {
            outcome = new SupplierOutcome(supplier.Name, QuoteStatus.Failed, 0m, Elapsed(stopwatch), DateTime.UtcNow);
        }

        await writer.WriteAsync(outcome);
    }

    private async Task CloseWhenSettledAsync(IEnumerable<Task> workers, ChannelWriter<SupplierOutcome> writer)
    {
        try
        {
            await Task.WhenAll(workers);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "A supplier task faulted outside its own error handling.");
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static async Task PersistAsync(PriceHuntDbContext db, SearchRun run, SupplierOutcome outcome, int responded)
    {
        db.Quotes.Add(new SupplierQuote
        {
            SearchRunId = run.Id,
            SupplierName = outcome.SupplierName,
            Price = outcome.Price,
            ResponseTimeMs = outcome.ResponseTimeMs,
            RespondedAt = outcome.At,
            Status = outcome.Status,
            FromLocation = run.FromLocation,
            ToLocation = run.ToLocation,
            SearchStartedAt = run.StartedAt,
            FromDate = run.FromDate,
            ToDate = run.ToDate
        });

        run.RespondedCount = responded;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task CloseRunAsync(PriceHuntDbContext db, SearchRun run, SearchStatus status, int responded)
    {
        var persisted = await db.SearchRuns
            .AsNoTracking()
            .Where(record => record.Id == run.Id)
            .Select(record => record.Status)
            .FirstOrDefaultAsync(CancellationToken.None);

        run.Status = persisted == SearchStatus.Cancelled ? SearchStatus.Cancelled : status;
        run.RespondedCount = responded;
        run.FinishedAt ??= DateTime.UtcNow;

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task EmitCompletionAsync(
        SearchRun run,
        int responded,
        int total,
        Func<SseEvent, Task> emit,
        CancellationToken clientToken)
    {
        if (clientToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await emit(new SseEvent("run_completed", new RunCompletedDto(
                run.Status.ToString(),
                responded,
                total,
                (long)((run.FinishedAt ?? DateTime.UtcNow) - run.StartedAt).TotalMilliseconds)));
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    public static async Task MarkOrphanedRunsAsync(PriceHuntDbContext db)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        var orphaned = await db.SearchRuns
            .Where(run => run.Status == SearchStatus.Running && run.StartedAt < cutoff)
            .ToListAsync();

        if (orphaned.Count == 0)
        {
            return;
        }

        foreach (var run in orphaned)
        {
            run.Status = SearchStatus.TimedOut;
            run.FinishedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private static void Tally(SupplierOutcome outcome, ref int responded)
    {
        if (outcome.Status is QuoteStatus.Success or QuoteStatus.Failed)
        {
            responded++;
        }
    }

    private static bool IsUnanswered(QuoteStatus status) =>
        status is QuoteStatus.Timeout or QuoteStatus.NoResponse;

    private static int Elapsed(Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return (int)stopwatch.ElapsedMilliseconds;
    }
}
