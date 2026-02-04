using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for SimulationRun persistence.
/// </summary>
public interface ISimulationRunRepository
{
    Task<SimulationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimulationRun>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<SimulationRun> runs, CancellationToken cancellationToken = default);
    Task UpdateAsync(SimulationRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions run from Pending to Running. Returns true if claimed; false if already claimed or terminal.
    /// </summary>
    Task<bool> TryClaimAsync(Guid runId, DateTimeOffset startedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions multiple runs from Pending to Running in one round-trip.
    /// Returns IDs of runs that were successfully claimed (others may have been claimed by another worker).
    /// </summary>
    Task<IReadOnlyList<Guid>> ClaimBatchAsync(
        IReadOnlyList<Guid> runIds,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk transition runs from Running to Completed. Used by batched persist.
    /// </summary>
    Task<int> BulkMarkCompletedAsync(IReadOnlyList<Guid> runIds, DateTimeOffset completedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets runs stuck in Running back to Pending for retry. Used for recovery when buffer flush failed.
    /// </summary>
    Task<int> ResetStuckRunsToPendingAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the batch has any runs with status Pending or Running. Lightweight check for finalization.
    /// </summary>
    Task<bool> HasNonTerminalRunsAsync(Guid batchId, CancellationToken cancellationToken = default);
}
