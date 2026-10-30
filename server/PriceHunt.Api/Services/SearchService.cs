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
    private readonly IPlaceService _places;
    private readonly SearchOrchestrator _orchestrator;
    private readonly SearchStreamGate _streams;

    public SearchService(
        PriceHuntDbContext db,
        SupplierCatalog catalog,
        IPlaceService places,
        SearchOrchestrator orchestrator,
        SearchStreamGate streams)
    {
        _db = db;
        _catalog = catalog;
        _places = places;
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

        if (from.Length == 0 || to.Length == 0 ||
            !await _places.ExistsAsync(from, CancellationToken.None) ||
            !await _places.ExistsAsync(to, CancellationToken.None))
        {
            return (null, new SearchRejection(StatusCodes.Status400BadRequest, "Choose a pickup and a delivery city from the list."));
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

        if (!_streams.TryEnter(id))
        {
            return new SearchRejection(StatusCodes.Status409Conflict, "That search is already streaming.");
        }

        try
        {
            SseWriter.Prepare(http.Response);
            await http.Response.StartAsync(http.RequestAborted);

            var writer = new SseWriter(http.Response);
            await _orchestrator.RunAsync(
                run,
                suppliers,
                sseEvent => writer.WriteAsync(sseEvent, http.RequestAborted),
                _db,
                http.RequestAborted);
            return null;
        }
        finally
        {
            _streams.Exit(id);
        }
    }

    public async Task<SearchRejection?> CancelAsync(Guid id)
    {
        var run = await _db.SearchRuns.FirstOrDefaultAsync(record => record.Id == id);
        if (run is null)
        {
            return new SearchRejection(StatusCodes.Status404NotFound, "That search does not exist.");
        }

        if (run.Status == SearchStatus.Running)
        {
            run.Status = SearchStatus.Cancelled;
            run.FinishedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return null;
    }
}
