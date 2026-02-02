namespace BingoSim.Application.Simulation.Snapshot;

public sealed class OutcomeSnapshotDto
{
    public required int WeightNumerator { get; init; }
    public required int WeightDenominator { get; init; }
    public required List<ProgressGrantSnapshotDto> Grants { get; init; }
}
