namespace BingoSim.Application.Simulation.Snapshot;

public sealed class AttemptSnapshotDto
{
    public required string Key { get; init; }
    public required int RollScope { get; init; } // 0 = PerPlayer, 1 = PerGroup
    public required int BaselineTimeSeconds { get; init; }
    public required int? VarianceSeconds { get; init; }
    public required List<OutcomeSnapshotDto> Outcomes { get; init; }
}
