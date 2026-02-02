using BingoSim.Core.Entities;
using BingoSim.Core.Enums;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for SimulationBatch persistence.
/// </summary>
public interface ISimulationBatchRepository
{
    Task<SimulationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SimulationBatch batch, CancellationToken cancellationToken = default);
    Task UpdateAsync(SimulationBatch batch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions batch from Pending/Running to Completed or Error. Returns true if this process won.
    /// </summary>
    Task<bool> TryTransitionToFinalAsync(Guid id, BatchStatus newStatus, DateTimeOffset completedAt, string? errorMessage, CancellationToken cancellationToken = default);
}
