namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Snapshot DTO for activity mode support (solo vs group, min/max group size).
/// Used for group formation during simulation.
/// </summary>
public sealed class ActivityModeSupportSnapshotDto
{
    public bool SupportsSolo { get; init; } = true;
    public bool SupportsGroup { get; init; } = false;
    public int? MinGroupSize { get; init; }
    public int? MaxGroupSize { get; init; }
}
