using System.Diagnostics;
using BingoSim.Application.Interfaces;
using BingoSim.Shared.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Publishes simulation run work via MassTransit. Used by Web (distributed start) and Worker (retry).
/// Publishes ExecuteSimulationRunBatch messages (Phase 3); batch size controlled by DistributedExecution:BatchSize.
/// Phase 4I: Publishes batches in parallel chunks to increase message broker throughput.
/// </summary>
public sealed class MassTransitRunWorkPublisher(
    IPublishEndpoint publishEndpoint,
    IOptions<DistributedExecutionOptions> options,
    ILogger<MassTransitRunWorkPublisher> logger) : ISimulationRunWorkPublisher
{
    private readonly int _batchSize = Math.Max(1, options.Value.BatchSize);
    private readonly int _publishChunkSize = Math.Max(1, options.Value.PublishChunkSize);

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

        var sw = Stopwatch.StartNew();

        for (var chunkStart = 0; chunkStart < batches.Count; chunkStart += _publishChunkSize)
        {
            var chunkEnd = Math.Min(chunkStart + _publishChunkSize, batches.Count);
            var publishTasks = new List<Task>(chunkEnd - chunkStart);

            for (var i = chunkStart; i < chunkEnd; i++)
            {
                var batch = batches[i];
                var batchIndex = i;
                var workerIndex = workerCount > 1 ? batchIndex % workerCount : (int?)null;

                var publishTask = publishEndpoint.Publish(
                    new ExecuteSimulationRunBatch { SimulationRunIds = batch.ToList(), WorkerIndex = workerIndex },
                    cancellationToken);

                publishTasks.Add(publishTask);
            }

            await Task.WhenAll(publishTasks);
        }

        sw.Stop();
        var avgMsPerBatch = batches.Count > 0 ? (double)sw.ElapsedMilliseconds / batches.Count : 0;
        logger.LogInformation(
            "Published {BatchCount} batches ({RunCount} runs) in {ElapsedMs}ms ({AvgMs:F2}ms per batch) [CHUNKED PARALLEL]",
            batches.Count, runIds.Count, sw.ElapsedMilliseconds, avgMsPerBatch);
    }
}
