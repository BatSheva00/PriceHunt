using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceHunt.Api.Data;
using PriceHunt.Api.Dtos;
using PriceHunt.Api.Models;

namespace PriceHunt.Api.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController : ControllerBase
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private static readonly string[] SortFields = ["date", "route", "supplier", "price", "responseTime"];

    private readonly PriceHuntDbContext _db;

    public HistoryController(PriceHuntDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string[]? suppliers,
        [FromQuery] string? fromLocation,
        [FromQuery] string? toLocation,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        {
            return BadRequest(new { error = "The end date cannot be before the start date." });
        }

        var field = string.IsNullOrWhiteSpace(sortBy) ? "date" : sortBy.Trim();
        if (!SortFields.Contains(field, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Sort by one of: date, route, supplier, price, responseTime." });
        }

        var direction = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir.Trim();
        if (direction.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            direction = "asc";
        }
        else if (direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            direction = "desc";
        }
        else
        {
            return BadRequest(new { error = "Sort direction must be asc or desc." });
        }

        var pageNumber = page ?? 1;
        if (pageNumber < 1)
        {
            return BadRequest(new { error = "Page must be at least 1." });
        }

        var size = pageSize ?? DefaultPageSize;
        if (size is < 1 or > MaxPageSize)
        {
            return BadRequest(new { error = "Page size must be between 1 and 100." });
        }

        var query = _db.Quotes.AsNoTracking();

        if (from.HasValue)
        {
            var start = from.Value.Date;
            query = query.Where(quote => quote.SearchStartedAt >= start);
        }

        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(quote => quote.SearchStartedAt < end);
        }

        var names = SupplierNames(suppliers);
        if (names.Length > 0)
        {
            query = query.Where(quote => names.Contains(quote.SupplierName));
        }

        if (!string.IsNullOrWhiteSpace(fromLocation))
        {
            var origin = fromLocation.Trim().ToLowerInvariant();
            query = query.Where(quote => quote.FromLocation.ToLower().Contains(origin));
        }

        if (!string.IsNullOrWhiteSpace(toLocation))
        {
            var destination = toLocation.Trim().ToLowerInvariant();
            query = query.Where(quote => quote.ToLocation.ToLower().Contains(destination));
        }

        var total = await query.CountAsync(cancellationToken);
        var descending = direction == "desc";
        var rows = await Order(query, field, descending)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(quote => new
            {
                quote.Id,
                quote.SearchStartedAt,
                quote.FromLocation,
                quote.ToLocation,
                quote.SupplierName,
                Price = quote.Status == QuoteStatus.Success ? (decimal?)quote.Price : null,
                quote.ResponseTimeMs
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(quote => new HistoryRowDto(
                quote.Id,
                quote.SearchStartedAt,
                $"{quote.FromLocation} → {quote.ToLocation}",
                quote.SupplierName,
                quote.Price,
                quote.ResponseTimeMs))
            .ToArray();

        return Ok(new HistoryPageDto(items, total, pageNumber, size));
    }

    /// <summary>Rows without a successful quote stay after priced rows, ascending and descending.</summary>
    private static IOrderedQueryable<SupplierQuote> Order(
        IQueryable<SupplierQuote> query,
        string sortBy,
        bool descending)
    {
        IOrderedQueryable<SupplierQuote> ordered = sortBy.ToLowerInvariant() switch
        {
            "route" => descending
                ? query.OrderByDescending(quote => quote.FromLocation).ThenByDescending(quote => quote.ToLocation)
                : query.OrderBy(quote => quote.FromLocation).ThenBy(quote => quote.ToLocation),
            "supplier" => descending
                ? query.OrderByDescending(quote => quote.SupplierName)
                : query.OrderBy(quote => quote.SupplierName),
            "price" => descending
                ? query.OrderBy(quote => quote.Status == QuoteStatus.Success ? 0 : 1)
                    .ThenByDescending(quote => quote.Status == QuoteStatus.Success ? quote.Price : 0)
                : query.OrderBy(quote => quote.Status == QuoteStatus.Success ? 0 : 1)
                    .ThenBy(quote => quote.Status == QuoteStatus.Success ? quote.Price : 0),
            "responsetime" => descending
                ? query.OrderByDescending(quote => quote.ResponseTimeMs)
                : query.OrderBy(quote => quote.ResponseTimeMs),
            _ => descending
                ? query.OrderByDescending(quote => quote.SearchStartedAt)
                : query.OrderBy(quote => quote.SearchStartedAt)
        };

        return ordered.ThenBy(quote => quote.Id);
    }

    private static string[] SupplierNames(string[]? suppliers)
    {
        if (suppliers is null || suppliers.Length == 0)
        {
            return [];
        }

        return suppliers
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
