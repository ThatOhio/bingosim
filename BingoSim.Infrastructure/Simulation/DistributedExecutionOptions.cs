namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Options for distributed execution (batch message publishing).
/// </summary>
public class DistributedExecutionOptions
{
    public const string SectionName = "DistributedExecution";

    /// <summary>
    /// Number of run IDs per ExecuteSimulationRunBatch message. Default 20.
    /// Override via appsettings DistributedExecution:BatchSize or env DISTRIBUTED_BATCH_SIZE.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// Expected number of workers for batch partitioning. Default 3.
    /// Used to calculate WorkerIndex for each batch (batchNumber % WorkerCount).
    /// Override via appsettings DistributedExecution:WorkerCount or env DISTRIBUTED_WORKER_COUNT.
    /// </summary>
    public int WorkerCount { get; set; } = 3;

    /// <summary>
    /// Number of batches to publish in parallel. Default 100.
    /// Higher values increase throughput but may overwhelm RabbitMQ. Tune based on broker capacity.
    /// Override via appsettings DistributedExecution:PublishChunkSize or env DISTRIBUTED_PUBLISH_CHUNK_SIZE.
    /// </summary>
    public int PublishChunkSize { get; set; } = 100;
}
