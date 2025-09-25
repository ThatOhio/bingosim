using BingoSim.Models;

namespace BingoSim.Strategies;

// Points-Per-Minute with Row-Weighted Bonus strategy
// Base score = points per expected minute like greedy; add a row-progress bonus
// multiplier that scales up as you approach 5 points in the current (frontier) row.
// This smoothly balances completion value and unlock urgency.
public class RowWeightedBonusStrategy : IStrategy
{
    public string Name => "ppm-row-bonus";

    public List<Tile> ChooseTargets(Board board)
    {
        // Candidates: unlocked & incomplete tiles
        var candidates = board.AllTiles()
            .Where(t => !t.Completed && board.IsTileUnlocked(t))
            .ToList();
        if (candidates.Count == 0) return new List<Tile>();

        // Determine the frontier (highest unlocked row index)
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        int frontier = unlockedRowIndices.Count > 0 ? unlockedRowIndices.Max() : 0;
        var frontierRow = board.Rows.First(r => r.Index == frontier);
        int pointsInFrontier = frontierRow.PointsCompleted;
        const int Threshold = 5;

        Tile best = candidates
            .OrderByDescending(t => ScoreWithBonus(t))
            .ThenBy(t => t.RowIndex)
            .First();

        return new List<Tile> { best };

        double ScoreWithBonus(Tile t)
        {
            double baseScore = BaseScore(t);
            double bonus = 1.0;
            if (t.RowIndex == frontier)
            {
                // Progress proximity to unlock threshold, smoothed and factoring this tile's points
                double pre = Math.Clamp(pointsInFrontier / (double)Threshold, 0.0, 1.0);
                double post = Math.Clamp(Math.Min(Threshold, pointsInFrontier + t.Points) / (double)Threshold, 0.0, 1.0);
                double proximity = 0.5 * pre + 0.5 * post; // gently prefer tiles that push us over the line

                const double BonusWeight = 0.8; // tuning knob; 0.8 gives a noticeable but not overwhelming bias
                bonus = 1.0 + BonusWeight * proximity;
            }
            return baseScore * bonus;
        }

        static double BaseScore(Tile t)
        {
            double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
            var expectedUnitsPerAttempt = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
            var expectedAttempts = remaining / expectedUnitsPerAttempt;
            var expectedTime = expectedAttempts * t.AvgTimePerAttemptSeconds;
            return t.Points / Math.Max(expectedTime, 1e-9);
        }
    }
}
