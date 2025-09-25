using BingoSim.Models;

namespace BingoSim.Strategies;

// Row Threshold strategy:
// - Identify the current frontier (highest index row that is unlocked).
// - If that frontier row has < 5 points completed, focus on pushing that row to 5 as efficiently as possible.
//   Do this by selecting the activity with the best aggregate points-per-expected-time among tiles on that row
//   (with a penalty if that activity has many locked tiles elsewhere to avoid wasted drops).
// - Once the threshold is met (>=5 points on the frontier row), immediately switch to best efficiency across
//   the entire currently unlocked window (all unlocked rows), choosing the single best activity among unlocked tiles.
public class RowThresholdStrategy : IStrategy
{
    public string Name => "row-threshold";

    public List<Tile> ChooseTargets(Board board)
    {
        // Determine the highest currently unlocked row (frontier)
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        if (unlockedRowIndices.Count == 0) return new List<Tile>();
        int frontier = unlockedRowIndices.Max();

        var frontierRow = board.Rows.First(r => r.Index == frontier);
        bool frontierMeetsThreshold = frontierRow.PointsCompleted >= 5;

        if (!frontierMeetsThreshold)
        {
            // Phase 1: push frontier row to 5 points as fast as possible.
            // Consider only incomplete & unlocked tiles on the frontier row.
            var rowTiles = frontierRow.Tiles
                .Where(t => !t.Completed && board.IsTileUnlocked(t))
                .ToList();
            if (rowTiles.Count == 0)
            {
                // Fallback to best unlocked tile anywhere
                return BestUnlockedTilesAny(board);
            }

            // Pre-compute locked tiles grouped by activity to apply a penalty for wasted off-target drops
            var lockedByActivity = board.AllTiles()
                .Where(t => !t.Completed && !board.IsTileUnlocked(t))
                .GroupBy(t => t.ActivityId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by activity present on the frontier row
            var activitiesOnRow = rowTiles
                .GroupBy(t => t.ActivityId)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (activitiesOnRow.Count == 0) return new List<Tile>();

            double ScoreActivity(string activityId, List<Tile> tilesOnRow)
            {
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

            var bestActivityId = activitiesOnRow
                .OrderByDescending(kv => ScoreActivity(kv.Key, kv.Value))
                .First().Key;

            // Return only frontier-row tiles for the chosen activity
            return activitiesOnRow[bestActivityId];
        }
        else
        {
            // Phase 2: threshold met. Choose best efficiency across the whole unlocked window.
            // Consider all unlocked & incomplete tiles anywhere.
            var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
            if (unlocked.Count == 0) return new List<Tile>();

            // Group by activity; choose the activity with the highest total points per expected time across unlocked tiles
            var byActivity = unlocked.GroupBy(t => t.ActivityId).ToDictionary(g => g.Key, g => g.ToList());

            double ScoreActivityAll(List<Tile> tiles)
            {
                double sumScore = 0.0;
                foreach (var t in tiles)
                {
                    double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
                    var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
                    var expectedAttempts = remaining / expectedUnitsPerAttempt;
                    var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
                    var baseScore = t.Points / Math.Max(expectedTime, 1e-9);
                    sumScore += baseScore;
                }
                return sumScore;
            }

            var bestActivityId = byActivity
                .OrderByDescending(kv => ScoreActivityAll(kv.Value))
                .First().Key;

            return byActivity[bestActivityId];
        }
    }

    private static List<Tile> BestUnlockedTilesAny(Board board)
    {
        var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (unlocked.Count == 0) return new List<Tile>();
        var byActivity = unlocked.GroupBy(t => t.ActivityId).ToDictionary(g => g.Key, g => g.ToList());

        double ScoreActivityAll(List<Tile> tiles)
        {
            double sumScore = 0.0;
            foreach (var t in tiles)
            {
                double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
                var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
                var expectedAttempts = remaining / expectedUnitsPerAttempt;
                var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
                var baseScore = t.Points / Math.Max(expectedTime, 1e-9);
                sumScore += baseScore;
            }
            return sumScore;
        }

        var bestActivityId = byActivity
            .OrderByDescending(kv => ScoreActivityAll(kv.Value))
            .First().Key;
        return byActivity[bestActivityId];
    }
}