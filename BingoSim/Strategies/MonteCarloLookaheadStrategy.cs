using BingoSim.Models;
using BingoSim.Util;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

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

    public MonteCarloLookaheadStrategy(int playouts = 16, int steps = 12, int seedJitter = 1337, bool parallelPlayouts = true)
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

                // Precompute per-playout structures
                int rowCount = simBoard.Rows.Count;
                var rowPoints = new int[rowCount];
                for (int ri = 0; ri < rowCount; ri++)
                {
                    int sum = 0;
                    var r = simBoard.Rows[ri];
                    for (int tj = 0; tj < r.Tiles.Count; tj++)
                    {
                        var t = r.Tiles[tj];
                        if (t.Completed) sum += t.Points;
                    }
                    rowPoints[ri] = sum;
                }
                var rowUnlockedBuf = new bool[rowCount];
                ComputeRowUnlockedFast(rowPoints, rowUnlockedBuf);

                var activityTiles = new Dictionary<string, List<Tile>>(16);
                foreach (var r in simBoard.Rows)
                {
                    for (int tj = 0; tj < r.Tiles.Count; tj++)
                    {
                        var t = r.Tiles[tj];
                        if (!activityTiles.TryGetValue(t.ActivityId, out var list))
                        {
                            list = new List<Tile>(4);
                            activityTiles[t.ActivityId] = list;
                        }
                        list.Add(t);
                    }
                }

                int pointsGained = 0;
                var targetBuffer = new List<Tile>(8);

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
                    }
                    else
                    {
                        var nextAct = BestActivityIdGreedy(activityTiles, rowUnlockedBuf);
                        if (string.IsNullOrEmpty(nextAct)) break;
                        stepActivityId = nextAct;
                    }

                    // Build target tiles for chosen activity into reusable buffer (unlocked & incomplete only)
                    targetBuffer.Clear();
                    if (activityTiles.TryGetValue(stepActivityId, out var actList))
                    {
                        for (int ti = 0; ti < actList.Count; ti++)
                        {
                            var t = actList[ti];
                            if (!t.Completed && rowUnlockedBuf[t.RowIndex]) targetBuffer.Add(t);
                        }
                    }
                    if (targetBuffer.Count == 0) break; // nothing to do
                    targetTiles = targetBuffer;

                    // Compute attempt time (max among targets)
                    double attemptTime = 0.0;
                    for (int i = 0; i < targetTiles.Count; i++)
                    {
                        var tt = targetTiles[i];
                        if (tt.AvgTimePerAttemptSeconds > attemptTime) attemptTime = tt.AvgTimePerAttemptSeconds;
                    }
                    time += attemptTime;

                    // Roll across all incomplete tiles tied to this activity
                    Tile? bestTile = null;
                    int bestQty = 0;
                    int bestRemaining = int.MaxValue;

                    if (activityTiles.TryGetValue(stepActivityId, out var tilesForAct))
                    {
                        for (int idx = 0; idx < tilesForAct.Count; idx++)
                        {
                            var tile = tilesForAct[idx];
                            if (tile.Completed) continue;
                            bool unlockedTile = rowUnlockedBuf[tile.RowIndex];

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
                        bool wasCompleted = bestTile.Completed;
                        ApplyProgress(simBoard, bestTile, bestQty);
                        if (!wasCompleted && bestTile.Completed)
                        {
                            pointsGained += bestTile.Points;
                            int ri2 = bestTile.RowIndex;
                            if ((uint)ri2 < (uint)rowPoints.Length)
                            {
                                rowPoints[ri2] += bestTile.Points;
                                ComputeRowUnlockedFast(rowPoints, rowUnlockedBuf);
                                if (!unlockedNext && hasNextRow && nextRowIndex < rowUnlockedBuf.Length && rowUnlockedBuf[nextRowIndex])
                                {
                                    unlockedNext = true;
                                    unlockTime = time;
                                }
                            }
                        }
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

                pointsGains[p] = pointsGained;
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
                double s = GreedyScoreCached(board, tilesForActivity[i]);
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
                    AvgTimePerAttemptSeconds = t.AvgTimePerAttemptSeconds,
                    // Share immutable Sources list to reduce allocations
                    Sources = t.Sources, 
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

    // Compute unlocked status per row based on current completion points
    private static bool[] ComputeRowUnlocked(Board board)
    {
        var rows = board.Rows;
        int n = rows.Count;
        var arr = new bool[n];
        if (n == 0) return arr;
        arr[0] = true;
        for (int i = 1; i < n; i++)
        {
            arr[i] = rows[i - 1].PointsCompleted >= 5;
        }
        return arr;
    }

    // Heavy-cached greedy picker: avoids LINQ, uses global cached constants keyed by tile ID, and precomputes row unlocks per call
    private static Tile? GreedyPick(Board board)
    {
        var rows = board.Rows;
        int rowCount = rows.Count;
        if (rowCount == 0) return null;

        // Compute points completed per row and unlocked rows (avoid Board.IsTileUnlocked per tile)
        var pointsCompleted = new int[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            int sum = 0;
            var r = rows[i];
            var tiles = r.Tiles;
            for (int j = 0; j < tiles.Count; j++)
            {
                var t = tiles[j];
                if (t.Completed) sum += t.Points;
            }
            pointsCompleted[i] = sum;
        }
        var rowUnlocked = new bool[rowCount];
        rowUnlocked[0] = true;
        for (int i = 1; i < rowCount; i++) rowUnlocked[i] = pointsCompleted[i - 1] >= 5;

        Tile? best = null;
        double bestScore = double.NegativeInfinity;
        int bestRowIndex = int.MaxValue;

        for (int ri = 0; ri < rowCount; ri++)
        {
            if (!rowUnlocked[ri]) continue;
            var tiles = rows[ri].Tiles;
            for (int j = 0; j < tiles.Count; j++)
            {
                var t = tiles[j];
                if (t.Completed) continue;
                double k = GreedyK(t);
                int remaining = t.ItemsNeeded - t.ItemsObtained;
                if (remaining <= 0) continue;
                double score = k / remaining;
                if (score > bestScore || (score == bestScore && t.RowIndex < bestRowIndex))
                {
                    bestScore = score;
                    best = t;
                    bestRowIndex = t.RowIndex;
                }
            }
        }

        return best;
    }

    private static double GreedyScoreCached(Board board, Tile t)
    {
        double k = GreedyK(t);
        int remaining = Math.Max(1, t.ItemsNeeded - t.ItemsObtained);
        return k / remaining;
    }

    // Global cache for greedy scoring constants (thread-safe)
    private static readonly ConcurrentDictionary<string, double> s_kByTileId = new();

    private static double GreedyK(Tile t)
    {
        return s_kByTileId.GetOrAdd(t.Id, _ =>
        {
            double units = 0.0;
            if (t.Sources != null && t.Sources.Count > 0)
            {
                for (int i = 0; i < t.Sources.Count; i++)
                {
                    var s = t.Sources[i];
                    units += Math.Max(0, s.RollsPerAttempt) * Math.Max(0.0, s.ChancePerRoll) * Math.Max(0, s.QuantityPerSuccess);
                }
            }
            else
            {
                units = Math.Max(0.0, t.DropChancePerAttempt);
            }
            units = Math.Max(units, 1e-9);
            double time = Math.Max(t.AvgTimePerAttemptSeconds, 1e-9);
            return (t.Points * units) / time;
        });
    }

    private static void ComputeRowUnlockedFast(int[] rowPoints, bool[] rowUnlocked)
        {
            int n = rowUnlocked.Length;
            if (n == 0) return;
            rowUnlocked[0] = true;
            for (int i = 1; i < n; i++)
            {
                rowUnlocked[i] = rowPoints[i - 1] >= 5;
            }
        }

        private static string BestActivityIdGreedy(Dictionary<string, List<Tile>> activityTiles, bool[] rowUnlocked)
        {
            string bestAct = string.Empty;
            double bestScore = double.NegativeInfinity;
            foreach (var kv in activityTiles)
            {
                var list = kv.Value;
                double sum = 0.0;
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t.Completed) continue;
                    if (!rowUnlocked[t.RowIndex]) continue;
                    int remaining = t.ItemsNeeded - t.ItemsObtained;
                    if (remaining <= 0) continue;
                    double k = GreedyK(t);
                    sum += k / remaining;
                }
                if (sum > bestScore)
                {
                    bestScore = sum;
                    bestAct = kv.Key;
                }
            }
            return bestAct;
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


