using BingoSim.Models;

namespace BingoSim.Strategies;

public class GreedyStrategy : IStrategy
{
    public string Name => "greedy";

    public List<Tile> ChooseTargets(Board board)
    {
        // pick single best unlocked, incomplete tile by points per expected time
        var candidates = board.AllTiles()
            .Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (candidates.Count == 0) return new List<Tile>();

        Tile best = candidates
            .OrderByDescending(t => Score(t))
            .ThenBy(t => t.RowIndex)
            .First();

        return new List<Tile> { best };

        static double Score(Tile t)
        {
            // Use expected progress units per attempt from all sources
            double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
            var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
            var expectedAttempts = remaining / expectedUnitsPerAttempt;
            var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
            return t.Points / Math.Max(expectedTime, 1e-9);
        }
    }
}
