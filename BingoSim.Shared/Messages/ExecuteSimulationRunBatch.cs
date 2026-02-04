namespace BingoSim.Shared.Messages;

/// <summary>
/// Message to execute a batch of simulation runs. Worker claims all runs in one DB round-trip, then processes each.
/// Replaces ExecuteSimulationRun for distributed execution (Phase 3).
/// </summary>
public record ExecuteSimulationRunBatch
{
    public required IReadOnlyList<Guid> SimulationRunIds { get; init; }

    /// <summary>
    /// Worker index assigned to this batch (0-based). Used for work partitioning.
    /// Null indicates no partitioning (any worker can process).
    /// </summary>
    public int? WorkerIndex { get; init; }
}
