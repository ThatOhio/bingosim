namespace BingoSim.Application.Simulation.Snapshot;

public sealed class TileSnapshotDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int Points { get; init; }
    public required int RequiredCount { get; init; }
    public required List<TileActivityRuleSnapshotDto> AllowedActivities { get; init; }
}
