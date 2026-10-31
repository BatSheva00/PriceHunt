using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Interfaces;
using PriceHunt.Api.Models;
using PriceHunt.Api.Orchestration;
using PriceHunt.Api.Streaming;
using PriceHunt.Api.Suppliers;

namespace PriceHunt.Api.Services;

public sealed class SearchService : ISearchService
{
    private readonly PriceHuntDbContext _db;
    private readonly SupplierCatalog _catalog;
    private readonly SearchOrchestrator _orchestrator;
    private readonly SearchStreamGate _streams;

    public SearchService(
        PriceHuntDbContext db,
        SupplierCatalog catalog,
        SearchOrchestrator orchestrator,
        SearchStreamGate streams)
    {
        _db = db;
        _catalog = catalog;
        _orchestrator = orchestrator;
        _streams = streams;
    }

    public async Task<(SearchCreatedDto? Created, SearchRejection? Rejection)> CreateAsync(SearchRequestDto? body)
    {
        if (body is null)
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "A request body is required."));
        }

        var from = body.FromLocation?.Trim() ?? string.Empty;
        var to = body.ToLocation?.Trim() ?? string.Empty;

        if (!IsPlaceName(from) || !IsPlaceName(to))
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "Enter a pickup and a delivery place."));
        }

        if (body.FromDate == default || body.ToDate == default)
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "Both dates are required."));
        }

        if (body.ToDate.Date < body.FromDate.Date)
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "The delivery date cannot be before the pickup date."));
        }

        var suppliers = _catalog.Resolve(body.Suppliers ?? []);
        if (suppliers.Count == 0)
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "Select at least one supplier."));
        }

        var run = new SearchRun
        {
            FromLocation = from,
            ToLocation = to,
            FromDate = body.FromDate.Date,
            ToDate = body.ToDate.Date,
            SupplierNamesCsv = string.Join(',', suppliers.Select(supplier => supplier.Name)),
            TotalSuppliers = suppliers.Count,
            Status = SearchStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        _db.SearchRuns.Add(run);
        await _db.SaveChangesAsync();

        return (new SearchCreatedDto(
            run.Id,
            suppliers.Select(supplier => supplier.Name).ToArray(),
            (int)SearchOrchestrator.Deadline.TotalMilliseconds), null);
    }

    public async Task<SearchRejection?> StreamAsync(Guid id, HttpContext http)
    {
        var run = await _db.SearchRuns.FirstOrDefaultAsync(record => record.Id == id, http.RequestAborted);
        if (run is null)
        {
            return new SearchRejection(StatusCodes.Status404NotFound, "That search does not exist.");
        }

        if (run.Status != SearchStatus.Running)
        {
            return new SearchRejection(StatusCodes.Status409Conflict, $"That search already finished ({run.Status}).");
        }

        var suppliers = _catalog.Resolve(run.SupplierNamesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries));
        if (suppliers.Count == 0)
        {
            return new SearchRejection(StatusCodes.Status409Conflict, "That search has no suppliers.");
        }

        if (!_streams.TryEnter(id, out var cancelToken))
        {
            return new SearchRejection(StatusCodes.Status409Conflict, "That search is already streaming.");
        }

        try
        {
            var current = await _db.SearchRuns.AsNoTracking()
                .Where(record => record.Id == id)
                .Select(record => (SearchStatus?)record.Status)
                .FirstOrDefaultAsync(http.RequestAborted);

            if (current is null)
            {
                return new SearchRejection(StatusCodes.Status404NotFound, "That search does not exist.");
            }

            if (current != SearchStatus.Running)
            {
                return new SearchRejection(StatusCodes.Status409Conflict, $"That search already finished ({current}).");
            }

            SseWriter.Prepare(http.Response);
            await http.Response.StartAsync(http.RequestAborted);

            var writer = new SseWriter(http.Response);
            await _orchestrator.RunAsync(
                run,
                suppliers,
                sseEvent => writer.WriteAsync(sseEvent, http.RequestAborted),
                _db,
                http.RequestAborted,
                cancelToken);
            return null;
        }
        finally
        {
            _streams.Exit(id);
        }
    }

    public async Task<SearchRejection?> CancelAsync(Guid id)
    {
        var exists = await _db.SearchRuns.AsNoTracking().AnyAsync(record => record.Id == id);
        if (!exists)
        {
            return new SearchRejection(StatusCodes.Status404NotFound, "That search does not exist.");
        }

        var finishedAt = DateTime.UtcNow;
        var updated = await _db.SearchRuns
            .Where(record => record.Id == id && record.Status == SearchStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.Status, SearchStatus.Cancelled)
                .SetProperty(record => record.FinishedAt, finishedAt));

        if (updated > 0)
        {
            _streams.Cancel(id);
        }

        return null;
    }

    private static bool IsPlaceName(string name) => name.Length is >= 2 and <= 80;
}
