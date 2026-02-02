namespace BingoSim.Application.Simulation.Snapshot;

public sealed class RowSnapshotDto
{
    public required int Index { get; init; }
    public required List<TileSnapshotDto> Tiles { get; init; }
}
