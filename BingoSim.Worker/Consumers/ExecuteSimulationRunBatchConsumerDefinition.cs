using BingoSim.Shared.Messages;
using BingoSim.Worker.Filters;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Worker.Consumers;

/// <summary>
/// Configures ExecuteSimulationRunBatch consumer endpoint: ConcurrentMessageLimit, PrefetchCount, and WorkerIndexFilter.
/// </summary>
public class ExecuteSimulationRunBatchConsumerDefinition(
    IOptions<WorkerSimulationOptions> options,
    ILogger<WorkerIndexFilter> filterLogger) : ConsumerDefinition<ExecuteSimulationRunBatchConsumer>
{
    [Obsolete("Overrides obsolete base member; required for MassTransit ConsumerDefinition")]
    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ExecuteSimulationRunBatchConsumer> consumerConfigurator)
    {
        var opts = options.Value;
        var maxConcurrent = ResolveMaxConcurrentRuns(opts.MaxConcurrentRuns);
        var prefetch = opts.PrefetchCount > 0 ? opts.PrefetchCount : maxConcurrent * 2;

        ConcurrentMessageLimit = maxConcurrent;

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbit)
            rabbit.PrefetchCount = (ushort)Math.Min(prefetch, ushort.MaxValue);

        var workerIndex = opts.WorkerIndex;
        var workerCount = opts.WorkerCount;

        if (workerIndex.HasValue && workerIndex.Value >= 0)
        {
            consumerConfigurator.Message<ExecuteSimulationRunBatch>(m =>
                m.UseFilter(new WorkerIndexFilter(workerIndex, workerCount, filterLogger)));
        }
    }

    private static int ResolveMaxConcurrentRuns(int configured)
    {
        if (configured > 0)
            return configured;
        return Math.Min(Environment.ProcessorCount, 4);
    }
}
