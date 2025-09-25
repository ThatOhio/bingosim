using BingoSim.Models;

namespace BingoSim.Strategies;

// Risk-averse strategy:
// Strongly penalize activities that have many high-value locked tiles (especially deeper future rows).
// Chooses activities that minimize expected waste on locked tiles across the whole board,
// even if that delays unlocking the next rows slightly.
public class RiskAverseStrategy : IStrategy
{
    public string Name => "risk-averse";

    public List<Tile> ChooseTargets(Board board)
    {
        // Consider all unlocked & incomplete tiles anywhere
        var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (unlocked.Count == 0) return new List<Tile>();

        // Group unlocked tiles by activity
        var unlockedByActivity = unlocked.GroupBy(t => t.ActivityId).ToDictionary(g => g.Key, g => g.ToList());

        // Precompute locked tiles by activity with value scoring
        var lockedByActivity = board.AllTiles()
            .Where(t => !t.Completed && !board.IsTileUnlocked(t))
            .GroupBy(t => t.ActivityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Determine current highest unlocked row to weight deeper rows heavier
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        int frontier = unlockedRowIndices.Count > 0 ? unlockedRowIndices.Max() : 0;

        string bestActivityId = string.Empty;
        double bestScore = double.NegativeInfinity;

        foreach (var kv in unlockedByActivity)
        {
            string activityId = kv.Key;
            var unlockedTiles = kv.Value;

            double benefit = SumEfficiency(unlockedTiles);
            double penalty = 0.0;
            if (lockedByActivity.TryGetValue(activityId, out var lockedTiles) && lockedTiles.Count > 0)
            {
                // Strong penalty based on locked tiles' value and depth.
                foreach (var t in lockedTiles)
                {
                    double value = TileValue(t); // points per expected time
                    int depth = Math.Max(0, t.RowIndex - frontier); // rows ahead of frontier
                    double depthMultiplier = 1.0 + 0.6 * depth; // deeper future tiles hurt more
                    penalty += value * depthMultiplier;
                }

                // Global weight to make the strategy strongly risk-averse
                const double PenaltyWeight = 4.0; // tuned to be strong; can be adjusted by user later
                penalty *= PenaltyWeight;
            }

            double score = benefit - penalty;
            if (score > bestScore)
            {
                bestScore = score;
                bestActivityId = activityId;
            }
        }

        // Return all unlocked tiles tied to the chosen activity (grouped action)
        return string.IsNullOrEmpty(bestActivityId) ? new List<Tile>() : unlockedByActivity[bestActivityId];
    }

    private static double SumEfficiency(List<Tile> tiles)
    {
        double sum = 0.0;
        foreach (var t in tiles)
        {
            sum += TileValue(t);
        }
        return sum;
    }

    // Value proxy: points per expected time to complete remaining progress
    private static double TileValue(Tile t)
    {
        double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
        var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
        var expectedAttempts = remaining / expectedUnitsPerAttempt;
        var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
        return t.Points / Math.Max(expectedTime, 1e-9);
    }
}
