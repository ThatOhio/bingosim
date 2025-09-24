using BingoSim.Models;
using BingoSim.Strategies;
using BingoSim.Util;

namespace BingoSim.Simulation;

public class Simulator
{
    private readonly Board _board;
    private readonly IStrategy _strategy;
    private readonly Rng _rng;

    public Simulator(Board board, IStrategy strategy, int? seed = null)
    {
        _board = board;
        _strategy = strategy;
        _rng = new Rng(seed);
    }

    public RunResult Run()
    {
        // Reset state
        foreach (var t in _board.AllTiles()) t.ResetProgress();

        double time = 0.0;
        int totalPoints = 0;
        var result = new RunResult();
        var rowUnlockRecorded = new HashSet<int> { 0 }; // row 0 unlocked at t=0
        result.RowUnlockTimesMinutes[0] = 0.0;

        // Continue until all tiles complete
        while (_board.AllTiles().Any(t => !t.Completed))
        {
            var targets = _strategy.ChooseTargets(_board);
            if (targets.Count == 0)
            {
                // No unlocked targets left, but some tiles not completed => need to unlock next row
                // In this case, do nothing but break to avoid infinite loop
                break;
            }

            // All targets should share same activity
            var activityId = targets[0].ActivityId;
            var activity = _board.GetActivity(activityId) ?? new Activity { Id = activityId, Name = activityId };

            // Perform a single attempt for this activity group
            var attemptTime = targets.Max(t => t.AvgTimePerAttemptMinutes); // use max time among grouped tiles
            time += attemptTime;

            // Determine tiles to roll for this attempt: always roll across all incomplete tiles tied to this activity
            // Progress is only applied to unlocked tiles; successes for locked tiles are wasted
            var rollTiles = _board.AllTiles().Where(t => t.ActivityId == activityId && !t.Completed).ToList();

            // Roll drops across all progress sources
            var successEvents = new List<(Tile tile, int qty)>();
            foreach (var tile in rollTiles)
            {
                foreach (var src in tile.Sources)
                {
                    int rolls = Math.Max(0, src.RollsPerAttempt);
                    for (int i = 0; i < rolls; i++)
                    {
                        if (_rng.Chance(src.ChancePerRoll))
                        {
                            successEvents.Add((tile, Math.Max(0, src.QuantityPerSuccess)));
                        }
                    }
                }
            }

            if (successEvents.Count > 0)
            {
                // Only one event can be applied per attempt. Prefer unlocked outcomes that best reduce remaining.
                var unlockedEvents = successEvents.Where(e => _board.IsTileUnlocked(e.tile)).ToList();
                if (unlockedEvents.Count > 0)
                {
                    // Choose event that minimizes remaining items after applying qty; tie-break randomly
                    int BestIndex(List<(Tile tile, int qty)> events)
                    {
                        int bestIdx = 0;
                        int bestRemaining = int.MaxValue;
                        for (int i = 0; i < events.Count; i++)
                        {
                            var e = events[i];
                            int remaining = Math.Max(0, e.tile.ItemsNeeded - e.tile.ItemsObtained - e.qty);
                            if (remaining < bestRemaining)
                            {
                                bestRemaining = remaining;
                                bestIdx = i;
                            }
                        }
                        return bestIdx;
                    }

                    var chosenIdx = BestIndex(unlockedEvents);
                    var chosenEvent = unlockedEvents[chosenIdx];
                    ApplyProgress(chosenEvent.tile, chosenEvent.qty, time, result, ref totalPoints, rowUnlockRecorded);
                }
                // else: all successes were on locked tiles; drop is wasted
            }
        }

        result.TotalTimeMinutes = time;
        result.TotalPoints = totalPoints;
        return result;
    }

    private void ApplyProgress(Tile tile, int quantity, double time, RunResult result, ref int totalPoints, HashSet<int> rowUnlockRecorded)
    {
        tile.ItemsObtained += Math.Max(0, quantity);
        if (tile.ItemsObtained >= tile.ItemsNeeded && !tile.Completed)
        {
            tile.Completed = true;
            totalPoints += tile.Points;
            result.CompletionOrder.Add(new TileCompletion
            {
                TileId = tile.Id,
                RowIndex = tile.RowIndex,
                Points = tile.Points,
                CompletionTimeMinutes = time
            });

            // Check unlock of next row
            var row = _board.Rows.First(r => r.Index == tile.RowIndex);
            if (row.PointsCompleted >= 5)
            {
                int nextRowIndex = row.Index + 1;
                if (_board.Rows.Any(r => r.Index == nextRowIndex) && !rowUnlockRecorded.Contains(nextRowIndex))
                {
                    rowUnlockRecorded.Add(nextRowIndex);
                    result.RowUnlockTimesMinutes[nextRowIndex] = time;
                }
            }
        }
    }
}
