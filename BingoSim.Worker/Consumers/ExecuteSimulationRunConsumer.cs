using BingoSim.Application.Interfaces;
using BingoSim.Shared.Messages;
using BingoSim.Worker.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Worker.Consumers;

/// <summary>
/// Consumes ExecuteSimulationRun messages, claims run atomically, executes via Application, re-publishes on retry.
/// </summary>
public class ExecuteSimulationRunConsumer(
    ISimulationRunExecutor executor,
    IOptions<WorkerSimulationOptions> options,
    IWorkerRunThroughputRecorder throughputRecorder,
    ILogger<ExecuteSimulationRunConsumer> logger,
    IWorkerConcurrencyObserver? concurrencyObserver = null) : IConsumer<ExecuteSimulationRun>
{
    public async Task Consume(ConsumeContext<ExecuteSimulationRun> context)
    {
        var runId = context.Message.SimulationRunId;
        concurrencyObserver?.OnConsumeStarted();
        try
        {
            var delayMs = options.Value.SimulationDelayMs;
            if (delayMs > 0)
                await Task.Delay(delayMs, context.CancellationToken);

            logger.LogDebug("Consuming ExecuteSimulationRun for run {RunId}", runId);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await executor.ExecuteAsync(runId, context.CancellationToken);
                sw.Stop();
                throughputRecorder.RecordRunCompleted();
                logger.LogDebug("Run {RunId} completed in {ElapsedMs}ms", runId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogError(ex, "ExecuteSimulationRun failed for run {RunId} (after {ElapsedMs}ms)", runId, sw.ElapsedMilliseconds);
                throw;
            }
        }
        finally
        {
            concurrencyObserver?.OnConsumeEnded();
        }
    }
}

/// <summary>
/// Worker simulation options (concurrency, prefetch, throttle).
/// </summary>
public class WorkerSimulationOptions
{
    public const string SectionName = "WorkerSimulation";

    /// <summary>
    /// Artificial delay (ms) per run for throttle/validation. Default 0.
    /// </summary>
    public int SimulationDelayMs { get; set; } = 0;

    /// <summary>
    /// Max concurrent simulation runs per worker. Default 0 = CPU-aware (min(ProcessorCount, 4)).
    /// Override via appsettings WorkerSimulation:MaxConcurrentRuns or env WORKER_MAX_CONCURRENT_RUNS.
    /// </summary>
    public int MaxConcurrentRuns { get; set; } = 0;

    /// <summary>
    /// RabbitMQ prefetch count. Default 0 = MaxConcurrentRuns * 2.
    /// Override via appsettings WorkerSimulation:PrefetchCount or env WORKER_PREFETCH_COUNT.
    /// </summary>
    public int PrefetchCount { get; set; } = 0;
}
