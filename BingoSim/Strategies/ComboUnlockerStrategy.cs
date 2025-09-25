using BingoSim.Models;

namespace BingoSim.Strategies;

// Combo Frontier strategy
// Phase 1 (until last row is unlocked):
//   - Only consider tiles on the furthest unlocked row (frontier).
//   - Choose among the 3 two-tile combinations that yield >= 5 points with two tiles:
//       (Easy + Elite), (Medium + Hard), (Medium + Elite).
//   - Score each combo by speed (inverse of total expected time to finish both tiles)
//     and apply a penalty proportional to how many locked tiles elsewhere share the
//     activities of the combo's tiles to avoid wasting overlap opportunities later.
//   - For each step, return tiles from a single activity: choose the activity of the
//     tile within the chosen combo that has the smaller expected remaining time; and
//     return all tiles on the frontier row sharing that activity (to satisfy simulator grouping).
// Phase 2 (once all rows are unlocked):
//   - Choose the activity that currently has the largest number of incomplete tiles
//     (maximize potential overlap); tie-break by summed efficiency. Return all tiles
//     of that activity.
public class ComboUnlockerStrategy : IStrategy
{
    public string Name => "combo-unlocker";

    public List<Tile> ChooseTargets(Board board)
    {
        // Determine frontier row (highest index with any tile unlocked)
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        if (unlockedRowIndices.Count == 0) return new List<Tile>();
        int frontier = unlockedRowIndices.Max();
        int maxRowIndex = board.Rows.Max(r => r.Index);

        bool allRowsUnlocked = frontier >= maxRowIndex;

        if (!allRowsUnlocked)
        {
            // Phase 1: only work on frontier row
            var row = board.Rows.First(r => r.Index == frontier);
            var rowTiles = row.Tiles.Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
            if (rowTiles.Count == 0)
            {
                return new List<Tile>();
            }

            // Build map by difficulty
            Tile? GetByDiff(TileDifficulty d) => rowTiles.FirstOrDefault(t => t.Difficulty == d);
            var easy = GetByDiff(TileDifficulty.Easy);
            var med = GetByDiff(TileDifficulty.Medium);
            var hard = GetByDiff(TileDifficulty.Hard);
            var elite = GetByDiff(TileDifficulty.Elite);

            var combos = new List<(Tile a, Tile b)?>();
            if (easy != null && elite != null) combos.Add((easy, elite));
            if (med != null && hard != null) combos.Add((med, hard));
            if (med != null && elite != null) combos.Add((med, elite));

            // If no valid combos (e.g., tiles completed), fallback: pick best single tile on frontier by efficiency with penalty
            if (combos.Count == 0)
            {
                return BestSingleOnFrontierWithPenalty(board, rowTiles);
            }

            // Pre-compute locked tiles count by activity
            var lockedByActivity = board.AllTiles()
                .Where(t => !t.Completed && !board.IsTileUnlocked(t))
                .GroupBy(t => t.ActivityId)
                .ToDictionary(g => g.Key, g => g.Count());

            double ComboScore((Tile a, Tile b) combo)
            {
                double timeA = ExpectedTimeToFinish(combo.a);
                double timeB = ExpectedTimeToFinish(combo.b);
                double baseSpeed = 1.0 / Math.Max(timeA + timeB, 1e-9); // faster total time => higher score

                int overlapCount = 0;
                if (lockedByActivity.TryGetValue(combo.a.ActivityId, out int ca)) overlapCount += Math.Max(0, ca);
                if (lockedByActivity.TryGetValue(combo.b.ActivityId, out int cb)) overlapCount += Math.Max(0, cb);
                // Penalty weight; modest so we still choose among penalized combos if needed
                const double Alpha = 1.0;
                double penalty = 1.0 / (1.0 + Alpha * overlapCount);
                return baseSpeed * penalty;
            }

            var bestCombo = combos!
                .Select(c => c!.Value)
                .OrderByDescending(ComboScore)
                .First();

            // Choose which activity to push this step: take the tile in combo with smaller remaining expected time
            var primary = ExpectedTimeToFinish(bestCombo.a) <= ExpectedTimeToFinish(bestCombo.b) ? bestCombo.a : bestCombo.b;
            // Return all tiles on frontier row that share that activity (grouped execution)
            var activityTilesOnRow = rowTiles.Where(t => t.ActivityId == primary.ActivityId).ToList();
            if (activityTilesOnRow.Count == 0)
            {
                // Safety fallback: return the primary alone
                return new List<Tile> { primary };
            }
            return activityTilesOnRow;
        }
        else
        {
            // Phase 2: all rows unlocked -> choose activity with the largest number of incomplete tiles
            var incomplete = board.AllTiles().Where(t => !t.Completed).ToList();
            if (incomplete.Count == 0) return new List<Tile>();
            var byActivity = incomplete.GroupBy(t => t.ActivityId).ToDictionary(g => g.Key, g => g.ToList());

            string bestActivityId = string.Empty;
            int bestCount = -1;
            double bestTieScore = double.NegativeInfinity;

            foreach (var kv in byActivity)
            {
                var tiles = kv.Value;
                int count = tiles.Count;
                double tieScore = SumEfficiency(tiles); // prefer activities that are efficient if counts tie
                if (count > bestCount || (count == bestCount && tieScore > bestTieScore))
                {
                    bestCount = count;
                    bestTieScore = tieScore;
                    bestActivityId = kv.Key;
                }
            }

            return string.IsNullOrEmpty(bestActivityId) ? new List<Tile>() : byActivity[bestActivityId];
        }
    }

    private static List<Tile> BestSingleOnFrontierWithPenalty(Board board, List<Tile> rowTiles)
    {
        var lockedByActivity = board.AllTiles()
            .Where(t => !t.Completed && !board.IsTileUnlocked(t))
            .GroupBy(t => t.ActivityId)
            .ToDictionary(g => g.Key, g => g.Count());

        double Score(Tile t)
        {
            double remainingTime = ExpectedTimeToFinish(t);
            double baseScore = 1.0 / Math.Max(remainingTime, 1e-9);
            lockedByActivity.TryGetValue(t.ActivityId, out int overlap);
            double penalty = 1.0 / (1.0 + Math.Max(0, overlap));
            return baseScore * penalty;
        }

        var best = rowTiles
            .OrderByDescending(Score)
            .First();
        return rowTiles.Where(t => t.ActivityId == best.ActivityId).ToList();
    }

    private static double ExpectedTimeToFinish(Tile t)
    {
        double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
        double unitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
        double attempts = remaining / unitsPerAttempt;
        return attempts * t.AvgTimePerAttemptSeconds;
    }

    private static double SumEfficiency(List<Tile> tiles)
    {
        double sum = 0.0;
        foreach (var t in tiles)
        {
            double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
            double units = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
            double attempts = remaining / units;
            double time = attempts * t.AvgTimePerAttemptSeconds;
            sum += t.Points / Math.Max(time, 1e-9);
        }
        return sum;
    }
}