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
}
