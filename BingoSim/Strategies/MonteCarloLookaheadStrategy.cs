using BingoSim.Models;
using BingoSim.Util;

namespace BingoSim.Strategies;

// Monte Carlo Lookahead strategy
// For each candidate next activity (based on unlocked, incomplete tiles), perform short rollouts
// (e.g., 10–30 simulated attempts) using RNG to estimate the likelihood and expected time to the
// next row unlock, along with net points gained. Choose the activity with best outlook.
public class MonteCarloLookaheadStrategy : IStrategy
{
    public string Name => "monte-carlo";

    private readonly int _playouts;
    private readonly int _steps;
    private readonly int _seedJitter;
    private readonly bool _parallelPlayouts;

    public MonteCarloLookaheadStrategy(int playouts = 32, int steps = 15, int seedJitter = 1337, bool parallelPlayouts = true)
    {
        _playouts = Math.Max(1, playouts);
        _steps = Math.Max(1, steps);
        _seedJitter = seedJitter;
        _parallelPlayouts = parallelPlayouts;
    }

    public List<Tile> ChooseTargets(Board board)
    {
        // Gather candidate activities from unlocked, incomplete tiles
        var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (unlocked.Count == 0) return new List<Tile>();

        var candidatesByActivity = unlocked
            .GroupBy(t => t.ActivityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // If only one candidate activity, just return its tiles (fast path)
        if (candidatesByActivity.Count == 1)
        {
            var only = candidatesByActivity.First();
            return only.Value;
        }

        // Determine current frontier and next row to unlock (for scoring)
        int frontier = CurrentFrontier(board);
        int nextRowIndex = frontier + 1;
        bool hasNextRow = board.Rows.Any(r => r.Index == nextRowIndex);

        // Score each candidate activity via rollouts
        string bestActivity = string.Empty;
        (double unlockRate, double avgUnlockTime, double avgPoints, double fallbackGreedyScore) bestScore =
            (double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

        foreach (var kv in candidatesByActivity)
        {
            string activityId = kv.Key;
            var tilesForActivity = kv.Value;

            var rngSeedBase = HashSeed(activityId) ^ _seedJitter;

            var unlockFlags = new bool[_playouts];
            var unlockTimes = new double[_playouts];
            var pointsGains = new int[_playouts];

            // Run playouts (optionally in parallel)
            Action<int> runPlayout = p =>
            {
                var simBoard = DeepCloneWithState(board);
                var rng = new Rng(rngSeedBase + p * 7919);

                // Baseline points
                int pointsStart = 0;
                foreach (var row in simBoard.Rows) pointsStart += row.PointsCompleted;

                double time = 0.0;
                bool unlockedNext = false;
                double unlockTime = 0.0;

                for (int step = 0; step < _steps; step++)
                {
                    string stepActivityId;
                    // Build target tiles without LINQ
                    List<Tile> targetTiles = null!;
                    if (step == 0)
                    {
                        stepActivityId = activityId;
                        // Collect unlocked & incomplete tiles for this activity
                        var buffer = new List<Tile>(4);
                        foreach (var row in simBoard.Rows)
                        {
                            foreach (var t in row.Tiles)
                            {
                                if (!t.Completed && t.ActivityId == stepActivityId && simBoard.IsTileUnlocked(t))
                                {
                                    buffer.Add(t);
                                }
                            }
                        }
                        if (buffer.Count == 0) break; // nothing to do
                        targetTiles = buffer;
                    }
                    else
                    {
                        var greedy = GreedyPick(simBoard);
                        if (greedy == null) break;
                        stepActivityId = greedy.ActivityId;
                        targetTiles = new List<Tile>(1) { greedy };
                    }

                    // Compute attempt time (max among targets)
                    double attemptTime = 0.0;
                    for (int i = 0; i < targetTiles.Count; i++)
                    {
                        var tt = targetTiles[i];
                        if (tt.AvgTimePerAttemptMinutes > attemptTime) attemptTime = tt.AvgTimePerAttemptMinutes;
                    }
                    time += attemptTime;

                    // Roll across all incomplete tiles tied to this activity
                    Tile? bestTile = null;
                    int bestQty = 0;
                    int bestRemaining = int.MaxValue;

                    // enumerate tiles to roll
                    foreach (var row in simBoard.Rows)
                    {
                        foreach (var tile in row.Tiles)
                        {
                            if (tile.Completed || tile.ActivityId != stepActivityId) continue;

                            bool unlockedTile = simBoard.IsTileUnlocked(tile);

                            if (tile.Sources.Count > 0)
                            {
                                for (int si = 0; si < tile.Sources.Count; si++)
                                {
                                    var src = tile.Sources[si];
                                    int rolls = src.RollsPerAttempt;
                                    if (rolls <= 0) continue;
                                    double chance = src.ChancePerRoll;
                                    int qty = src.QuantityPerSuccess;
                                    for (int i = 0; i < rolls; i++)
                                    {
                                        if (rng.Chance(chance))
                                        {
                                            if (unlockedTile)
                                            {
                                                int remaining = tile.ItemsNeeded - tile.ItemsObtained - qty;
                                                if (remaining < 0) remaining = 0;
                                                if (remaining < bestRemaining)
                                                {
                                                    bestRemaining = remaining;
                                                    bestTile = tile;
                                                    bestQty = qty;
                                                }
                                            }
                                            // if locked, it's wasted; ignore
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Legacy model: single Bernoulli
                                if (rng.Chance(tile.DropChancePerAttempt) && unlockedTile)
                                {
                                    int remaining = tile.ItemsNeeded - tile.ItemsObtained - 1;
                                    if (remaining < 0) remaining = 0;
                                    if (remaining < bestRemaining)
                                    {
                                        bestRemaining = remaining;
                                        bestTile = tile;
                                        bestQty = 1;
                                    }
                                }
                            }
                        }
                    }

                    if (bestTile != null)
                    {
                        ApplyProgress(simBoard, bestTile, bestQty);
                    }

                    // Check unlock
                    if (!unlockedNext && hasNextRow && RowUnlocked(simBoard, nextRowIndex))
                    {
                        unlockedNext = true;
                        unlockTime = time;
                    }

                    // Early stop if finished
                    bool anyIncomplete = false;
                    foreach (var row in simBoard.Rows)
                    {
                        foreach (var t in row.Tiles)
                        {
                            if (!t.Completed) { anyIncomplete = true; break; }
                        }
                        if (anyIncomplete) break;
                    }
                    if (!anyIncomplete) break;
                }

                if (unlockedNext)
                {
                    unlockFlags[p] = true;
                    unlockTimes[p] = unlockTime;
                }

                int pointsEnd = 0;
                foreach (var row in simBoard.Rows) pointsEnd += row.PointsCompleted;
                pointsGains[p] = Math.Max(0, pointsEnd - pointsStart);
            };

            if (_parallelPlayouts && _playouts >= 4)
            {
                System.Threading.Tasks.Parallel.For(0, _playouts, runPlayout);
            }
            else
            {
                for (int p = 0; p < _playouts; p++) runPlayout(p);
            }

            double unlocks = 0, sumUnlockTime = 0, sumPointsGain = 0;
            for (int p = 0; p < _playouts; p++)
            {
                if (unlockFlags[p]) { unlocks += 1; sumUnlockTime += unlockTimes[p]; }
                sumPointsGain += pointsGains[p];
            }

            double unlockRate = unlocks / _playouts;
            double avgUnlock = unlocks > 0 ? sumUnlockTime / unlocks : double.PositiveInfinity;
            double avgPoints = sumPointsGain / _playouts;
            double fallbackGreedy = 0.0;
            for (int i = 0; i < tilesForActivity.Count; i++)
            {
                double s = GreedyScore(tilesForActivity[i]);
                if (s > fallbackGreedy) fallbackGreedy = s;
            }

            var scoreTuple = (unlockRate, -avgUnlock, avgPoints, fallbackGreedy);
            var bestTuple = (bestScore.unlockRate, -bestScore.avgUnlockTime, bestScore.avgPoints, bestScore.fallbackGreedyScore);
            if (TupleCompare(scoreTuple, bestTuple) > 0)
            {
                bestScore = (unlockRate, avgUnlock, avgPoints, fallbackGreedy);
                bestActivity = activityId;
            }
        }

        if (string.IsNullOrEmpty(bestActivity))
        {
            // Fallback: greedy single best tile
            var greedy = GreedyPick(board);
            return greedy == null ? new List<Tile>() : new List<Tile> { greedy };
        }

        // Return all unlocked tiles for the chosen activity (grouped action), matching simulator behavior
        return candidatesByActivity[bestActivity];
    }

    private static int TupleCompare((double a, double b, double c, double d) x, (double a, double b, double c, double d) y)
    {
        if (x.a != y.a) return x.a.CompareTo(y.a);
        if (x.b != y.b) return x.b.CompareTo(y.b);
        if (x.c != y.c) return x.c.CompareTo(y.c);
        return x.d.CompareTo(y.d);
    }

    private static int CurrentFrontier(Board board)
    {
        var unlockedRowIndices = board.Rows
            .Where(r => r.Tiles.Any(t => board.IsTileUnlocked(t)))
            .Select(r => r.Index)
            .ToList();
        return unlockedRowIndices.Count > 0 ? unlockedRowIndices.Max() : 0;
    }

    private static bool RowUnlocked(Board board, int rowIndex)
    {
        if (!board.Rows.Any(r => r.Index == rowIndex)) return false;
        if (rowIndex == 0) return true;
        var prev = board.Rows.First(r => r.Index == rowIndex - 1);
        return prev.PointsCompleted >= 5;
    }

    private static Board DeepCloneWithState(Board src)
    {
        var clone = new Board
        {
            Activities = src.Activities,
            Rows = src.Rows.Select(r => new Row
            {
                Index = r.Index,
                Tiles = r.Tiles.Select(t => new Tile
                {
                    Id = t.Id,
                    RowIndex = t.RowIndex,
                    Difficulty = t.Difficulty,
                    ActivityId = t.ActivityId,
                    ItemsNeeded = t.ItemsNeeded,
                    DropChancePerAttempt = t.DropChancePerAttempt,
                    AvgTimePerAttemptMinutes = t.AvgTimePerAttemptMinutes,
                    Sources = t.Sources.Select(s => new ProgressSource
                    {
                        Name = s.Name,
                        RollsPerAttempt = s.RollsPerAttempt,
                        ChancePerRoll = s.ChancePerRoll,
                        QuantityPerSuccess = s.QuantityPerSuccess
                    }).ToList(),
                    Completed = t.Completed,
                    ItemsObtained = t.ItemsObtained
                }).ToList()
            }).ToList()
        };
        return clone;
    }

    private static void ApplyProgress(Board board, Tile tile, int quantity)
    {
        tile.ItemsObtained += Math.Max(0, quantity);
        if (tile.ItemsObtained >= tile.ItemsNeeded && !tile.Completed)
        {
            tile.Completed = true;
        }
    }

    private static Tile? GreedyPick(Board board)
    {
        var candidates = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (candidates.Count == 0) return null;
        return candidates
            .OrderByDescending(t => GreedyScore(t))
            .ThenBy(t => t.RowIndex)
            .First();
    }

    private static double GreedyScore(Tile t)
    {
        double remaining = Math.Max(0, t.ItemsNeeded - t.ItemsObtained);
        double units = Math.Max(t.ExpectedUnitsPerAttempt(), 1e-9);
        double attempts = remaining / units;
        double time = attempts * t.AvgTimePerAttemptMinutes;
        return t.Points / Math.Max(time, 1e-9);
    }

    private static int HashSeed(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var ch in s)
                h = h * 31 + ch;
            return h;
        }
    }
}
