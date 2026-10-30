namespace PriceHunt.Api.Suppliers;

public sealed record SearchRequest(string FromLocation, string ToLocation, DateTime FromDate, DateTime ToDate);

public interface ISupplier
{
    string Name { get; }

    /// <summary>True for the supplier that never answers, so a deadline cut-off can be reported precisely.</summary>
    bool NeverResponds { get; }

    Task<decimal> GetPriceAsync(SearchRequest request, CancellationToken cancellationToken);
}

public sealed record SupplierProfile(
    string Name,
    int MinDelayMs,
    int MaxDelayMs,
    double BaseFee,
    double PricePerKm,
    double FailureRate,
    bool NeverResponds);

/// <summary>
/// One configurable simulation used by all seven suppliers. Behaviour lives in data
/// (<see cref="SupplierCatalog"/>), not in seven near-identical classes.
/// </summary>
public sealed class SimulatedSupplier : ISupplier
{
    private readonly SupplierProfile _profile;

    public SimulatedSupplier(SupplierProfile profile) => _profile = profile;

    public string Name => _profile.Name;

    public bool NeverResponds => _profile.NeverResponds;

    public async Task<decimal> GetPriceAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        if (_profile.NeverResponds)
        {
            // Cancelled only by the deadline: this supplier never completes on its own.
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        else
        {
            var delay = Random.Shared.Next(_profile.MinDelayMs, _profile.MaxDelayMs + 1);
            await Task.Delay(delay, cancellationToken);
        }

        if (Random.Shared.NextDouble() < _profile.FailureRate)
        {
            throw new InvalidOperationException($"{Name} rejected the request.");
        }

        // Deterministic base per route, jitter on top: plausible prices that still differ run to run.
        var distance = RouteDistance(request.FromLocation, request.ToLocation);
        var daysAhead = Math.Max(0, (request.FromDate.Date - DateTime.UtcNow.Date).Days);
        var surge = 1.0 + Math.Min(daysAhead, 14) * 0.015;
        var jitter = 0.92 + Random.Shared.NextDouble() * 0.16;
        var price = (_profile.BaseFee + distance * _profile.PricePerKm) * surge * jitter;

        return Math.Round((decimal)price, 2, MidpointRounding.AwayFromZero);
    }

    private static double RouteDistance(string from, string to)
    {
        var start = Math.Abs(StableHash(from)) % 400;
        var end = Math.Abs(StableHash(to)) % 400;
        return 80 + Math.Abs(start - end) * 1.7;
    }

    private static int StableHash(string value)
    {
        var hash = 17;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            hash = unchecked(hash * 31 + character);
        }

        return hash;
    }
}
