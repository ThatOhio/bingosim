using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Executes a single simulation run: load run + snapshot, run simulation, persist results, update run and batch status.
/// </summary>
public interface ISimulationRunExecutor
{
    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a run with preloaded run and snapshot. Skips DB fetch and claim; for local perf path only.
    /// </summary>
    Task ExecuteAsync(SimulationRun run, EventSnapshotDto snapshot, CancellationToken cancellationToken = default);
}
