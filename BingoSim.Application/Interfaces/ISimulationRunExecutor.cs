namespace BingoSim.Application.Interfaces;

/// <summary>
/// Executes a single simulation run: load run + snapshot, run simulation, persist results, update run and batch status.
/// </summary>
public interface ISimulationRunExecutor
{
    Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default);
}
