using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for SimulationBatch persistence.
/// </summary>
public interface ISimulationBatchRepository
{
    Task<SimulationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SimulationBatch batch, CancellationToken cancellationToken = default);
    Task UpdateAsync(SimulationBatch batch, CancellationToken cancellationToken = default);
}
