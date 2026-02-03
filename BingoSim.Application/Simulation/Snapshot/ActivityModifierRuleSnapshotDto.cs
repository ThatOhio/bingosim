namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Snapshot DTO for a capability-based modifier (time/probability multipliers).
/// Used in EventSnapshot JSON; simulation applies when player has the capability.
/// </summary>
public sealed class ActivityModifierRuleSnapshotDto
{
    public required string CapabilityKey { get; init; }
    public decimal? TimeMultiplier { get; init; }
    public decimal? ProbabilityMultiplier { get; init; }
}
