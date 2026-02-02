namespace BingoSim.Application.Interfaces;

/// <summary>
/// Queue for simulation run work items. Web enqueues run ids when starting a local batch; hosted service dequeues and executes.
/// </summary>
public interface ISimulationRunQueue
{
    ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default);
    ValueTask<Guid?> DequeueAsync(CancellationToken cancellationToken = default);
}
