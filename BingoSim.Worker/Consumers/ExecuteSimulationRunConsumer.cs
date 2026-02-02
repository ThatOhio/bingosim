using BingoSim.Application.Interfaces;
using BingoSim.Shared.Messages;
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
    ILogger<ExecuteSimulationRunConsumer> logger) : IConsumer<ExecuteSimulationRun>
{
    public async Task Consume(ConsumeContext<ExecuteSimulationRun> context)
    {
        var runId = context.Message.SimulationRunId;
        var delayMs = options.Value.SimulationDelayMs;
        if (delayMs > 0)
            await Task.Delay(delayMs, context.CancellationToken);

        logger.LogInformation("Consuming ExecuteSimulationRun for run {RunId}", runId);

        try
        {
            await executor.ExecuteAsync(runId, context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ExecuteSimulationRun failed for run {RunId}", runId);
            throw;
        }
    }
}

/// <summary>
/// Worker simulation options (throttle knob for parallelism validation).
/// </summary>
public class WorkerSimulationOptions
{
    public const string SectionName = "WorkerSimulation";

    public int SimulationDelayMs { get; set; } = 0;
}
