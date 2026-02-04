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
        // Retries: no WorkerIndex so any worker can process (fault tolerance)
        await publishEndpoint.Publish(
            new ExecuteSimulationRunBatch { SimulationRunIds = [runId], WorkerIndex = null },
            cancellationToken);
    }

    public async ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return;

        var batches = runIds.Chunk(_batchSize).ToList();
        var workerCount = options.Value.WorkerCount;

        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var workerIndex = workerCount > 1 ? i % workerCount : (int?)null;
            await publishEndpoint.Publish(
                new ExecuteSimulationRunBatch { SimulationRunIds = batch.ToList(), WorkerIndex = workerIndex },
                cancellationToken);
        }
    }
}
