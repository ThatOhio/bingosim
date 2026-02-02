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
}
