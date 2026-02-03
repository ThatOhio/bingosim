using System.Collections.Concurrent;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Process-local, concurrency-safe snapshot cache. Keyed by BatchId.
/// Bounded size and TTL eviction to avoid unbounded memory growth.
/// Does not store EF entities or DbContext references.
/// </summary>
public sealed class SharedSnapshotCache : ISnapshotCache
{
    private const int MaxEntries = 32;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private readonly object _evictionLock = new();

    public EventSnapshotDto? Get(Guid batchId)
    {
        if (!_cache.TryGetValue(batchId, out var entry))
            return null;

        if (DateTimeOffset.UtcNow - entry.AddedAt > Ttl)
        {
            _cache.TryRemove(batchId, out _);
            return null;
        }

        return entry.Snapshot;
    }

    public void Set(Guid batchId, EventSnapshotDto snapshot)
    {
        var entry = new CacheEntry(snapshot, DateTimeOffset.UtcNow);
        _cache[batchId] = entry;

        if (_cache.Count > MaxEntries)
            EvictOldest();
    }

    private void EvictOldest()
    {
        lock (_evictionLock)
        {
            if (_cache.Count <= MaxEntries)
                return;

            var oldest = _cache
                .Select(kv => (kv.Key, kv.Value.AddedAt))
                .OrderBy(x => x.AddedAt)
                .FirstOrDefault();

            if (oldest.Key != default)
                _cache.TryRemove(oldest.Key, out _);
        }
    }

    private sealed record CacheEntry(EventSnapshotDto Snapshot, DateTimeOffset AddedAt);
}
