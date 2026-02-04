using BingoSim.Application.Interfaces;
using BingoSim.Core.Interfaces;
using BingoSim.Shared.Messages;
using BingoSim.Worker.Services;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Worker.Consumers;

/// <summary>
/// Consumes ExecuteSimulationRunBatch messages. Claims batch in one DB round-trip, then executes each claimed run.
/// Replaces ExecuteSimulationRunConsumer for distributed execution (Phase 3).
/// </summary>
public class ExecuteSimulationRunBatchConsumer(
    ISimulationRunRepository runRepo,
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerSimulationOptions> options,
    IWorkerRunThroughputRecorder throughputRecorder,
    ILogger<ExecuteSimulationRunBatchConsumer> logger,
    IWorkerConcurrencyObserver? concurrencyObserver = null) : IConsumer<ExecuteSimulationRunBatch>
{
    public async Task Consume(ConsumeContext<ExecuteSimulationRunBatch> context)
    {
        var runIds = context.Message.SimulationRunIds;
        if (runIds.Count == 0)
            return;

        concurrencyObserver?.OnConsumeStarted();
        try
        {
            var delayMs = options.Value.SimulationDelayMs;
            if (delayMs > 0)
                await Task.Delay(delayMs, context.CancellationToken);

            logger.LogDebug("Consuming ExecuteSimulationRunBatch with {Count} run IDs", runIds.Count);

            var startedAt = DateTimeOffset.UtcNow;
            var claimSw = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<Guid> claimedIds;
            try
            {
                claimedIds = await runRepo.ClaimBatchAsync(
                    runIds.ToList(),
                    startedAt,
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ClaimDbError] ClaimBatchAsync failed for batch of {Count} runs", runIds.Count);
                throw;
            }
            claimSw.Stop();

            await using var scope = scopeFactory.CreateAsyncScope();
            var perfRecorder = scope.ServiceProvider.GetService<IPerfRecorder>();
            perfRecorder?.Record("claim", claimSw.ElapsedMilliseconds, 1);
            perfRecorder?.Record("runs_claimed", 0, claimedIds.Count);

            if (claimedIds.Count == 0)
            {
                logger.LogDebug("No runs claimed from batch of {Count} (all already claimed elsewhere)", runIds.Count);
                return;
            }

            var executor = scope.ServiceProvider.GetRequiredService<ISimulationRunExecutor>();

            foreach (var runId in claimedIds)
            {
                try
                {
                    await executor.ExecuteAsync(runId, context.CancellationToken, skipClaim: true);
                    throughputRecorder.RecordRunCompleted();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ExecuteSimulationRunBatch: run {RunId} failed", runId);
                    throw;
                }
            }

            logger.LogDebug("ExecuteSimulationRunBatch completed: {Claimed} runs processed", claimedIds.Count);
        }
        finally
        {
            concurrencyObserver?.OnConsumeEnded();
        }
    }
}
