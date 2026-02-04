namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Represents a combination of tiles that meets the row unlock threshold.
/// Used by RowUnlockingStrategy to prioritize which tiles to complete for unlocking the next row.
/// </summary>
public sealed class TileCombination
{
    /// <summary>Tile keys in this combination (order preserved from row).</summary>
    public required IReadOnlyList<string> TileKeys { get; init; }

    /// <summary>Sum of tile points in this combination.</summary>
    public required int TotalPoints { get; init; }

    /// <summary>Estimated total completion time for this combination. Populated in task selection phase.</summary>
    public double EstimatedCompletionTime { get; set; }
}
