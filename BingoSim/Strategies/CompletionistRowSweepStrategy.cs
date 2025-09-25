using BingoSim.Models;

namespace BingoSim.Strategies;

// Completionist-First (Row Sweep) strategy
// Within the lowest unlocked row that still has any incomplete tiles,
// finish the easiest tiles to completion in order of expected minutes,
// ignoring activity overlaps. Always selects a single tile: the one with
// the least expected remaining time on that row.
public class CompletionistRowSweepStrategy : IStrategy
{
    public string Name => "row-sweep";

    public List<Tile> ChooseTargets(Board board)
    {
        // Find the lowest-index row that is currently unlocked and still has incomplete tiles
        var candidateRows = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t) && !t.Completed))
            .OrderBy(r => r.Index)
            .ToList();
        if (candidateRows.Count == 0) return new List<Tile>();

        var targetRow = candidateRows.First();
        // Among tiles on this row, pick the one with smallest expected remaining time
        var rowTiles = targetRow.Tiles.Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (rowTiles.Count == 0) return new List<Tile>();

        Tile easiest = rowTiles
            .OrderBy(t => ExpectedTimeToFinish(t))
            .ThenBy(t => t.Points) // prefer lower-point if same time (often easier)
            .First();

        return new List<Tile> { easiest };
    }

    private static double ExpectedTimeToFinish(Tile t)
    {
        double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
        double unitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
        double attempts = remaining / unitsPerAttempt;
        return attempts * t.AvgTimePerAttemptSeconds;
    }
}
