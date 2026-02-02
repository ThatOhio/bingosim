namespace BingoSim.Application.Simulation.Snapshot;

public sealed class ActivitySnapshotDto
{
    public required Guid Id { get; init; }
    public required string Key { get; init; }
    public required List<AttemptSnapshotDto> Attempts { get; init; }
    public required List<GroupSizeBandSnapshotDto> GroupScalingBands { get; init; }
}
