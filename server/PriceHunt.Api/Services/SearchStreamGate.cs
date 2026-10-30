using System.Collections.Concurrent;

namespace PriceHunt.Api.Services;

/// <summary>One open stream per search. A second stream would call the suppliers again.</summary>
public sealed class SearchStreamGate
{
    private readonly ConcurrentDictionary<Guid, byte> _active = new();

    public bool TryEnter(Guid searchId) => _active.TryAdd(searchId, 0);

    public void Exit(Guid searchId) => _active.TryRemove(searchId, out _);
}
