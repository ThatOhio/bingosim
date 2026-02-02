namespace BingoSim.Application.Interfaces;

/// <summary>
/// Idempotent batch finalization: computes aggregates and marks batch Completed/Error when all runs are terminal.
/// Safe under concurrency with multiple workers.
/// </summary>
public interface IBatchFinalizationService
{
    /// <summary>
    /// Finalizes a batch if all runs are terminal. Returns true if this process completed finalization.
    /// </summary>
    Task<bool> TryFinalizeAsync(Guid batchId, CancellationToken cancellationToken = default);
}
