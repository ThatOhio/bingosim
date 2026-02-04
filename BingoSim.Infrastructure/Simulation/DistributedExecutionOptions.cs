namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Options for distributed execution (batch message publishing).
/// </summary>
public class DistributedExecutionOptions
{
    public const string SectionName = "DistributedExecution";

    /// <summary>
    /// Number of run IDs per ExecuteSimulationRunBatch message. Default 10.
    /// Override via appsettings DistributedExecution:BatchSize or env DISTRIBUTED_BATCH_SIZE.
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
