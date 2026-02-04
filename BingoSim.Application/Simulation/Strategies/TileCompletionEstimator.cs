using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Estimates the time required to complete tiles based on activity configurations.
/// Uses baseline attempt times and expected progress from outcome weights.
/// Ignores variance, skill multipliers, and group size effects for simplicity.
/// </summary>
public static class TileCompletionEstimator
{
    /// <summary>
    /// Calculates the estimated time (in seconds) to complete a tile.
    /// For each activity rule on the tile, computes expected progress per run and time per run;
    /// returns the minimum over all valid rules (fastest activity).
    /// </summary>
    /// <param name="tile">The tile to estimate.</param>
    /// <param name="snapshot">Event snapshot containing activity definitions.</param>
    /// <returns>Estimated completion time in seconds, or double.MaxValue if tile cannot be completed.</returns>
    public static double EstimateCompletionTime(
        TileSnapshotDto tile,
        EventSnapshotDto snapshot)
    {
        if (tile.RequiredCount <= 0)
            return 0.0;

        var bestTime = double.MaxValue;

        foreach (var rule in tile.AllowedActivities)
        {
            if (!snapshot.ActivitiesById.TryGetValue(rule.ActivityDefinitionId, out var activity))
                continue;

            var expectedProgressPerRun = CalculateExpectedProgressPerRun(activity, rule);
            if (expectedProgressPerRun <= 0)
                continue;

            var timePerRun = GetMaxAttemptTime(activity);
            if (timePerRun <= 0)
                continue;

            var runsNeeded = Math.Ceiling(tile.RequiredCount / expectedProgressPerRun);
            var totalTime = runsNeeded * timePerRun;

            if (totalTime < bestTime)
                bestTime = totalTime;
        }

        return bestTime;
    }

    /// <summary>
    /// Expected progress per activity run (one duration) for grants matching the rule's AcceptedDropKeys.
    /// Sums over all attempts; each attempt contributes expected progress from its outcome weights.
    /// </summary>
    private static double CalculateExpectedProgressPerRun(
        ActivitySnapshotDto activity,
        TileActivityRuleSnapshotDto rule)
    {
        var acceptedKeys = rule.AcceptedDropKeys;
        if (acceptedKeys.Count == 0)
            return 0.0;

        double totalExpected = 0.0;

        foreach (var attempt in activity.Attempts)
        {
            if (attempt.Outcomes.Count == 0)
                continue;

            var totalWeight = attempt.Outcomes.Sum(o => o.WeightNumerator);
            if (totalWeight <= 0)
                continue;

            foreach (var outcome in attempt.Outcomes)
            {
                var prob = (double)outcome.WeightNumerator / totalWeight;
                var progressFromOutcome = outcome.Grants
                    .Where(g => acceptedKeys.Contains(g.DropKey, StringComparer.Ordinal))
                    .Sum(g => g.Units);
                totalExpected += prob * progressFromOutcome;
            }
        }

        return totalExpected;
    }

    /// <summary>
    /// Returns the maximum baseline attempt time (seconds) for the activity.
    /// For estimation, variance and skill/group multipliers are ignored.
    /// </summary>
    private static double GetMaxAttemptTime(ActivitySnapshotDto activity)
    {
        if (activity.Attempts.Count == 0)
            return 0.0;

        return activity.Attempts.Max(a => a.BaselineTimeSeconds);
    }
}
