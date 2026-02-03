using BingoSim.Application.Interfaces;
using BingoSim.Shared.Messages;
using MassTransit;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Publishes simulation run work via MassTransit. Used by Web (distributed start) and Worker (retry).
/// </summary>
public sealed class MassTransitRunWorkPublisher(IPublishEndpoint publishEndpoint) : ISimulationRunWorkPublisher
{
    public async ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(new ExecuteSimulationRun { SimulationRunId = runId }, cancellationToken);
    }

    public async ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return;
        var messages = runIds.Select(id => new ExecuteSimulationRun { SimulationRunId = id });
        await publishEndpoint.PublishBatch(messages, cancellationToken);
    }
}
