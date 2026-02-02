namespace BingoSim.Application.Simulation.Snapshot;

public sealed class PlayerSnapshotDto
{
    public required Guid PlayerId { get; init; }
    public required string Name { get; init; }
    public required decimal SkillTimeMultiplier { get; init; }
    public required List<string> CapabilityKeys { get; init; }
}
