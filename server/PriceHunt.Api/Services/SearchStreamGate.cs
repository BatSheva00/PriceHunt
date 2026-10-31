using System.Collections.Concurrent;

namespace PriceHunt.Api.Services;

/// <summary>
/// One open stream per search. The same registration carries the token that
/// <see cref="SearchService.CancelAsync"/> uses to stop that search's suppliers.
/// </summary>
public sealed class SearchStreamGate
{
    private sealed class ActiveSearch
    {
        public CancellationTokenSource Source { get; } = new();

        public object Sync { get; } = new();

        public bool Disposed { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, ActiveSearch> _active = new();

    public bool TryEnter(Guid searchId, out CancellationToken token)
    {
        var created = new ActiveSearch();
        if (!_active.TryAdd(searchId, created))
        {
            created.Source.Dispose();
            token = default;
            return false;
        }

        token = created.Source.Token;
        return true;
    }

    /// <summary>
    /// Cancels the suppliers for a search that is still streaming.
    /// <see cref="Exit"/> and this method share one lock, so Cancel never runs after Dispose.
    /// </summary>
    public void Cancel(Guid searchId)
    {
        if (!_active.TryGetValue(searchId, out var active))
        {
            return;
        }

        lock (active.Sync)
        {
            if (active.Disposed)
            {
                return;
            }

            active.Source.Cancel();
        }
    }

    public void Exit(Guid searchId)
    {
        if (!_active.TryRemove(searchId, out var active))
        {
            return;
        }

        lock (active.Sync)
        {
            active.Disposed = true;
            active.Source.Dispose();
        }
    }
}
