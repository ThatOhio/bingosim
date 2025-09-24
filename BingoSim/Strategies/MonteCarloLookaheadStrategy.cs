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

    public MonteCarloLookaheadStrategy(int playouts = 16, int steps = 15, int seedJitter = 1337)
    {
        _playouts = Math.Max(1, playouts);
        _steps = Math.Max(1, steps);
        _seedJitter = seedJitter;
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

            double unlocks = 0;
            double sumUnlockTime = 0;
            double sumPointsGain = 0;

            for (int p = 0; p < _playouts; p++)
            {
                // Clone board with current state
                var simBoard = DeepCloneWithState(board);
                var rng = new Rng(rngSeedBase + p * 7919);

                // Record baseline completed points at start
                int pointsStart = simBoard.Rows.Sum(r => r.PointsCompleted);

                double time = 0.0;
                bool unlockedNext = false;
                double unlockTime = 0.0;

                for (int step = 0; step < _steps; step++)
                {
                    // Determine action: first step forced to the candidate activity; afterwards use a simple greedy baseline
                    string stepActivityId;
                    List<Tile> targetTiles;
                    if (step == 0)
                    {
                        stepActivityId = activityId;
                        // Target all unlocked & incomplete tiles for this activity
                        targetTiles = simBoard.AllTiles()
                            .Where(t => !t.Completed && simBoard.IsTileUnlocked(t) && t.ActivityId == stepActivityId)
                            .ToList();
                        if (targetTiles.Count == 0)
                        {
                            // Nothing to do for this activity; break
                            break;
                        }
                    }
                    else
                    {
                        var greedy = GreedyPick(simBoard);
                        if (greedy == null) break;
                        stepActivityId = greedy.ActivityId;
                        targetTiles = new List<Tile> { greedy };
                    }

                    // Perform one attempt for the chosen activity
                    double attemptTime = targetTiles.Max(t => t.AvgTimePerAttemptMinutes);
                    time += attemptTime;

                    // Roll for all incomplete tiles tied to this activity
                    var rollTiles = simBoard.AllTiles().Where(t => t.ActivityId == stepActivityId && !t.Completed).ToList();

                    var successEvents = new List<(Tile tile, int qty)>();
                    foreach (var tile in rollTiles)
                    {
                        foreach (var src in tile.Sources)
                        {
                            int rolls = Math.Max(0, src.RollsPerAttempt);
                            for (int i = 0; i < rolls; i++)
                            {
                                if (rng.Chance(src.ChancePerRoll))
                                {
                                    successEvents.Add((tile, Math.Max(0, src.QuantityPerSuccess)));
                                }
                            }
                        }
                        // Legacy fallback if no sources configured
                        if (tile.Sources.Count == 0)
                        {
                            if (rng.Chance(tile.DropChancePerAttempt))
                            {
                                successEvents.Add((tile, 1));
                            }
                        }
                    }

                    if (successEvents.Count > 0)
                    {
                        // Prefer unlocked outcomes; pick the one that minimizes remaining after applying qty
                        var unlockedEvents = successEvents.Where(e => simBoard.IsTileUnlocked(e.tile)).ToList();
                        if (unlockedEvents.Count > 0)
                        {
                            var chosen = unlockedEvents
                                .OrderBy(e => Math.Max(0, e.tile.ItemsNeeded - e.tile.ItemsObtained - e.qty))
                                .First();
                            ApplyProgress(simBoard, chosen.tile, chosen.qty);
                        }
                    }

                    // Check if next row unlocked for the first time
                    if (!unlockedNext && hasNextRow && RowUnlocked(simBoard, nextRowIndex))
                    {
                        unlockedNext = true;
                        unlockTime = time;
                    }

                    // Early stop if board finished
                    if (!simBoard.AllTiles().Any(t => !t.Completed))
                        break;
                }

                if (unlockedNext)
                {
                    unlocks += 1;
                    sumUnlockTime += unlockTime;
                }

                int pointsEnd = simBoard.Rows.Sum(r => r.PointsCompleted);
                sumPointsGain += Math.Max(0, pointsEnd - pointsStart);
            }

            double unlockRate = unlocks / _playouts;
            double avgUnlock = unlocks > 0 ? sumUnlockTime / unlocks : double.PositiveInfinity;
            double avgPoints = sumPointsGain / _playouts;
            double fallbackGreedy = tilesForActivity
                .Select(t => GreedyScore(t))
                .DefaultIfEmpty(0.0)
                .Max();

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
