namespace BingoSim.Application.Simulation.Snapshot;

public sealed class AttemptSnapshotDto
{
    public required string Key { get; init; }
    public required int RollScope { get; init; } // 0 = PerPlayer, 1 = PerGroup
    public required int BaselineTimeSeconds { get; init; }
    /// <summary>Variance in seconds; 0 = no variance. Normalized at build time.</summary>
    public required int VarianceSeconds { get; init; }
    public required List<OutcomeSnapshotDto> Outcomes { get; init; }
}
