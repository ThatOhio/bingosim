using BingoSim.Application.Interfaces;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Routes run work to queue (local) or MassTransit (distributed) based on batch execution mode.
/// Used by Web when both local and distributed modes are supported.
/// </summary>
public sealed class RoutingSimulationRunWorkPublisher(
    ISimulationRunRepository runRepo,
    ISimulationBatchRepository batchRepo,
    ISimulationRunQueue runQueue,
    ISimulationRunWorkPublisher distributedPublisher) : ISimulationRunWorkPublisher
{
    public async ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await runRepo.GetByIdAsync(runId, cancellationToken);
        if (run is null)
            return;

        var batch = await batchRepo.GetByIdAsync(run.SimulationBatchId, cancellationToken);
        if (batch is null)
            return;

        if (batch.ExecutionMode == ExecutionMode.Local)
            await runQueue.EnqueueAsync(runId, cancellationToken);
        else
            await distributedPublisher.PublishRunWorkAsync(runId, cancellationToken);
    }
}
