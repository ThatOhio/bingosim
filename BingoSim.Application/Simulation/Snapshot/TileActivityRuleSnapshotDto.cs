namespace BingoSim.Application.Simulation.Snapshot;

public sealed class TileActivityRuleSnapshotDto
{
    public required Guid ActivityDefinitionId { get; init; }
    public required string ActivityKey { get; init; }
    public required List<string> AcceptedDropKeys { get; init; }
    public required List<string> RequirementKeys { get; init; }
    /// <summary>Capability-based modifiers. Required; use empty list for no modifiers.</summary>
    public required List<ActivityModifierRuleSnapshotDto> Modifiers { get; init; }
}
