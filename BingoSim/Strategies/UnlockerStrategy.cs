using BingoSim.Models;

namespace BingoSim.Strategies;

// Strategy focused on unlocking the board as fast as possible
// - Only targets tiles from the furthest row currently unlocked
// - Prefers tiles whose activity has no other locked tiles elsewhere (to avoid wasting off-target drops)
public class UnlockerStrategy : IStrategy
{
    public string Name => "unlocker";

    public List<Tile> ChooseTargets(Board board)
    {
        // Find the furthest (max index) row that is currently unlocked
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        if (unlockedRowIndices.Count == 0) return new List<Tile>();
        var furthestUnlocked = unlockedRowIndices.Max();

        // Consider only incomplete tiles on that row (and unlocked by definition)
        var rowTiles = board.Rows.First(r => r.Index == furthestUnlocked).Tiles
            .Where(t => !t.Completed && board.IsTileUnlocked(t))
            .ToList();

        // Pre-compute locked tiles grouped by activity to compute penalty
        var lockedByActivity = board.AllTiles()
            .Where(t => !t.Completed && !board.IsTileUnlocked(t))
            .GroupBy(t => t.ActivityId)
            .ToDictionary(g => g.Key, g => g.Count());

        // If the furthest unlocked row is cleared, fall back to best unlocked tile anywhere
        if (rowTiles.Count == 0)
        {
            var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
            if (unlocked.Count == 0) return new List<Tile>();

            double TileScore(Tile t)
            {
                double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
                var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
                var expectedAttempts = remaining / expectedUnitsPerAttempt;
                var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
                var baseScore = t.Points / Math.Max(expectedTime, 1e-9);
                lockedByActivity.TryGetValue(t.ActivityId, out int lockedOverlap);
                double penalty = 1.0 / (1 + Math.Max(0, lockedOverlap));
                return baseScore * penalty;
            }

            var bestTile = unlocked
                .OrderByDescending(TileScore)
                .ThenBy(t => t.RowIndex)
                .First();
            return new List<Tile> { bestTile };
        }

        // Group by activity, but only keep activities that have at least one tile on this row
        var activitiesOnRow = rowTiles
            .GroupBy(t => t.ActivityId)
            .ToDictionary(g => g.Key, g => g.ToList());
        if (activitiesOnRow.Count == 0) return new List<Tile>();

        // Score function for an activity based on its tiles on the furthest row only
        double ScoreActivity(string activityId, List<Tile> tilesOnRow)
        {
            // Sum efficiency across tiles on this row (we will target only these tiles)
            double sumScore = 0.0;
            foreach (var t in tilesOnRow)
            {
                double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
                var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
                var expectedAttempts = remaining / expectedUnitsPerAttempt;
                var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
                var baseScore = t.Points / Math.Max(expectedTime, 1e-9);
                sumScore += baseScore;
            }

            lockedByActivity.TryGetValue(activityId, out int lockedOverlap);
            double penalty = 1.0 / (1 + Math.Max(0, lockedOverlap));
            return sumScore * penalty;
        }

        // Choose best activity among those that have tiles on the furthest row
        var bestActivityId = activitiesOnRow
            .OrderByDescending(kv => ScoreActivity(kv.Key, kv.Value))
            .First().Key;

        // Return only tiles from the best activity AND only from the furthest unlocked row
        return activitiesOnRow[bestActivityId];
    }
}