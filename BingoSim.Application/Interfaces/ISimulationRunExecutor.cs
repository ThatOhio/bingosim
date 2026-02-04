using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Executes a single simulation run: load run + snapshot, run simulation, persist results, update run and batch status.
/// </summary>
public interface ISimulationRunExecutor
{
    /// <summary>
    /// Executes a run: load, claim, snapshot, simulate, persist. Use skipClaim=true when run was already claimed (batch consumer).
    /// </summary>
    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default, bool skipClaim = false);

    /// <summary>
    /// Executes a run with preloaded run and snapshot. Skips DB fetch and claim; for local perf path only.
    /// </summary>
    Task ExecuteAsync(SimulationRun run, EventSnapshotDto snapshot, CancellationToken cancellationToken = default);
}
