using BingoSim.Application.Interfaces;
using BingoSim.Shared.Messages;
using MassTransit;
using Microsoft.Extensions.Options;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Publishes simulation run work via MassTransit. Used by Web (distributed start) and Worker (retry).
/// Publishes ExecuteSimulationRunBatch messages (Phase 3); batch size controlled by DistributedExecution:BatchSize.
/// </summary>
public sealed class MassTransitRunWorkPublisher(
    IPublishEndpoint publishEndpoint,
    IOptions<DistributedExecutionOptions> options) : ISimulationRunWorkPublisher
{
    private readonly int _batchSize = Math.Max(1, options.Value.BatchSize);

    public async ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(
            new ExecuteSimulationRunBatch { SimulationRunIds = [runId] },
            cancellationToken);
    }

    public async ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return;

        for (var i = 0; i < runIds.Count; i += _batchSize)
        {
            var chunk = runIds.Skip(i).Take(_batchSize).ToList();
            await publishEndpoint.Publish(
                new ExecuteSimulationRunBatch { SimulationRunIds = chunk },
                cancellationToken);
        }
    }
}
