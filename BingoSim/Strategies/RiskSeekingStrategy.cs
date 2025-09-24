using BingoSim.Models;

namespace BingoSim.Strategies;

// Risk-seeking strategy:
// Favors activities with large, lumpy sources (high-quantity successes or uniques that count big).
// Intuition: prefer chances at big chunk progress even if rarer, de-emphasize probability a bit
// so that rare-but-huge outcomes look attractive. We still normalize by time.
public class RiskSeekingStrategy : IStrategy
{
    public string Name => "risk-seeking";

    public List<Tile> ChooseTargets(Board board)
    {
        // Consider all unlocked & incomplete tiles
        var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (unlocked.Count == 0) return new List<Tile>();

        // Group by activity and score by summed "lumpiness" across its unlocked tiles
        var byActivity = unlocked.GroupBy(t => t.ActivityId).ToDictionary(g => g.Key, g => g.ToList());

        string bestActivityId = string.Empty;
        double bestScore = double.NegativeInfinity;

        foreach (var kv in byActivity)
        {
            double score = 0.0;
            foreach (var t in kv.Value)
            {
                score += LumpinessIndex(t);
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestActivityId = kv.Key;
            }
        }

        return string.IsNullOrEmpty(bestActivityId) ? new List<Tile>() : byActivity[bestActivityId];
    }

    // Measure of how "lumpy" progress is for a tile, normalized by time.
    // We take the best single-source burst per attempt: quantity per success multiplied by sqrt(probability)
    // (de-emphasize probability so rare-but-huge stays attractive), scaled by tile value density (points per item needed),
    // divided by attempt time. If a tile has no explicit sources (fallback model), we treat it as smooth (low lumpiness).
    private static double LumpinessIndex(Tile t)
    {
        double remaining = Math.Max(1, t.ItemsNeeded - t.ItemsObtained);
        double pointsPerItem = t.Points / Math.Max(1.0, t.ItemsNeeded);
        double time = Math.Max(1e-9, t.AvgTimePerAttemptMinutes);

        // If no sources listed (pure Bernoulli dropChance), estimate pseudo-lumpiness as small.
        if (t.Sources.Count == 0)
        {
            // Smooth progress gets tiny weight to avoid zeroing out
            double pseudoQty = Math.Max(1.0, t.ExpectedUnitsPerAttempt());
            double pseudoP = Math.Min(1.0, t.DropChancePerAttempt);
            double burst = pseudoQty * Math.Sqrt(Math.Max(0.0, pseudoP));
            return (burst * pointsPerItem) / time;
        }

        double bestBurst = 0.0;
        foreach (var s in t.Sources)
        {
            int rolls = Math.Max(0, s.RollsPerAttempt);
            if (rolls == 0) continue;
            double pAny = 1.0 - Math.Pow(1.0 - Math.Clamp(s.ChancePerRoll, 0.0, 1.0), rolls);
            // Use min(quantity, remaining) so overkill is not over-rewarded
            double qty = Math.Min(s.QuantityPerSuccess, (int)remaining);
            double burst = qty * Math.Sqrt(pAny); // risk-seeking: sqrt to under-penalize rarity
            if (burst > bestBurst) bestBurst = burst;
        }

        return (bestBurst * pointsPerItem) / time;
    }
}
