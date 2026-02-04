using MassTransit;
using Microsoft.Extensions.Options;

namespace BingoSim.Worker.Consumers;

/// <summary>
/// Configures ExecuteSimulationRunBatch consumer endpoint: ConcurrentMessageLimit and PrefetchCount from WorkerSimulationOptions.
/// </summary>
public class ExecuteSimulationRunBatchConsumerDefinition(
    IOptions<WorkerSimulationOptions> options) : ConsumerDefinition<ExecuteSimulationRunBatchConsumer>
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
    }

    private static int ResolveMaxConcurrentRuns(int configured)
    {
        if (configured > 0)
            return configured;
        return Math.Min(Environment.ProcessorCount, 4);
    }
}
