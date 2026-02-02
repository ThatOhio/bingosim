namespace BingoSim.Application.Simulation.Snapshot;

public sealed class GroupSizeBandSnapshotDto
{
    public required int MinSize { get; init; }
    public required int MaxSize { get; init; }
    public required decimal TimeMultiplier { get; init; }
    public required decimal ProbabilityMultiplier { get; init; }
}
