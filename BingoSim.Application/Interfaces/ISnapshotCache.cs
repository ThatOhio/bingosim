using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Process-local cache for simulation snapshots keyed by batch ID.
/// Stores fully materialized EventSnapshotDto (no EF entities or DbContext references).
/// Used to eliminate redundant DB loads when multiple runs from the same batch execute concurrently.
/// </summary>
public interface ISnapshotCache
{
    /// <summary>
    /// Gets a cached snapshot for the batch, or null if not present or expired.
    /// </summary>
    EventSnapshotDto? Get(Guid batchId);

    /// <summary>
    /// Stores a snapshot for the batch. May evict older entries if at capacity.
    /// </summary>
    void Set(Guid batchId, EventSnapshotDto snapshot);
}
