namespace BingoSim.Application.Simulation.Snapshot;

public sealed class TeamSnapshotDto
{
    public required Guid TeamId { get; init; }
    public required string TeamName { get; init; }
    public required string StrategyKey { get; init; }
    public string? ParamsJson { get; init; }
    public required List<PlayerSnapshotDto> Players { get; init; }
}
