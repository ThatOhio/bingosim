using BingoSim.Shared.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BingoSim.Worker.Filters;

/// <summary>
/// Filters ExecuteSimulationRunBatch messages by WorkerIndex.
/// Only allows messages where message.WorkerIndex matches this worker's assigned index.
/// </summary>
public class WorkerIndexFilter : IFilter<ConsumeContext<ExecuteSimulationRunBatch>>
{
    private readonly int? _workerIndex;
    private readonly int _workerCount;
    private readonly ILogger<WorkerIndexFilter> _logger;

    public WorkerIndexFilter(int? workerIndex, int workerCount, ILogger<WorkerIndexFilter> logger)
    {
        _workerIndex = workerIndex;
        _workerCount = workerCount;
        _logger = logger;

        if (_workerIndex.HasValue)
        {
            _logger.LogInformation(
                "WorkerIndexFilter initialized: WorkerIndex={WorkerIndex}, WorkerCount={WorkerCount}",
                _workerIndex, _workerCount);
        }
        else
        {
            _logger.LogInformation("WorkerIndexFilter disabled: no WorkerIndex configured");
        }
    }

    public void Probe(ProbeContext context)
    {
        var scope = context.CreateFilterScope("workerIndexFilter");
        scope.Add("workerIndex", _workerIndex ?? -1);
        scope.Add("workerCount", _workerCount);
    }

    public async Task Send(ConsumeContext<ExecuteSimulationRunBatch> context, IPipe<ConsumeContext<ExecuteSimulationRunBatch>> next)
    {
        var messageWorkerIndex = context.Message.WorkerIndex;

        // If no partitioning configured, process all messages
        if (!_workerIndex.HasValue || !messageWorkerIndex.HasValue)
        {
            await next.Send(context);
            return;
        }

        // Validate worker index is within bounds
        if (_workerIndex.Value < 0 || _workerIndex.Value >= _workerCount)
        {
            _logger.LogWarning(
                "Invalid WorkerIndex configuration: {WorkerIndex} not in range [0, {WorkerCount})",
                _workerIndex.Value, _workerCount);
            await next.Send(context); // Process anyway to avoid message loss
            return;
        }

        // Only process if message is assigned to this worker
        if (messageWorkerIndex.Value == _workerIndex.Value)
        {
            _logger.LogDebug(
                "Processing batch assigned to WorkerIndex={WorkerIndex} (message has {MessageCount} runs)",
                _workerIndex.Value, context.Message.SimulationRunIds.Count);
            await next.Send(context);
        }
        else
        {
            _logger.LogDebug(
                "Skipping batch assigned to WorkerIndex={MessageWorkerIndex} (this worker is {ThisWorkerIndex})",
                messageWorkerIndex.Value, _workerIndex.Value);
            // Mark as consumed to avoid skipped queue; message is acked and removed.
            // With round-robin publishing order, workers typically receive their assigned messages.
            // Mismatches (e.g. after restart) result in message loss; consider separate queues for production.
            await context.NotifyConsumed(context, TimeSpan.Zero, "WorkerIndexFilter");
        }
    }
}
