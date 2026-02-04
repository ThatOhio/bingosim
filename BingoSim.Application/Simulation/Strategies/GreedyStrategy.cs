using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Greedy strategy that always selects the highest point value tiles, using completion time estimates as a tie-breaker.
/// Simpler than Row Unlocking: prioritizes tiles purely by point value without considering row unlock optimization.
/// Both task selection and grant allocation use the same logic: choose the highest point tile available,
/// with estimated completion time (ascending) as secondary sort, then tile key (alphabetical) for determinism.
/// </summary>
/// <remarks>
/// Use Greedy when you want straightforward point maximization without the complexity of row unlock optimization.
/// Use Row Unlocking when unlocking the next row quickly is more important than immediate point gains.
/// </remarks>
public sealed class GreedyStrategy : ITeamStrategy
{
    /// <summary>
    /// Selects which activity/tile a player should work on next.
    /// Prioritizes tiles by: points (descending), then estimated completion time (ascending), then tile key (alphabetical).
    /// Returns null when no valid task exists (e.g. no unlocked rows, all tiles completed, player lacks capabilities).
    /// </summary>
    /// <param name="context">Task selection context with player capabilities, event snapshot, and team state.</param>
    /// <returns>(activityId, rule) for the selected task, or null if no eligible task.</returns>
    public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
    {
        if (context.UnlockedRowIndices.Count == 0)
            return null;

        var availableTiles = context.EventSnapshot.Rows
            .Where(r => context.UnlockedRowIndices.Contains(r.Index))
            .SelectMany(r => r.Tiles)
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .ToList();

        if (availableTiles.Count == 0)
            return null;

        var tilesWithEstimates = availableTiles
            .Select(tile => new
            {
                Tile = tile,
                EstimatedTime = TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot)
            })
            .ToList();

        var sortedTiles = tilesWithEstimates
            .OrderByDescending(t => t.Tile.Points)
            .ThenBy(t => t.EstimatedTime)
            .ThenBy(t => t.Tile.Key, StringComparer.Ordinal);

        foreach (var item in sortedTiles)
        {
            var eligibleRule = FindEligibleRule(context, item.Tile);
            if (eligibleRule.HasValue)
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
        }

        return null;
    }

    /// <summary>
    /// Selects the tile that should receive a progress grant.
    /// Prioritizes tiles by: points (descending), then estimated completion time (ascending), then tile key (alphabetical).
    /// Returns null when there are no eligible tiles.
    /// </summary>
    /// <param name="context">Grant allocation context with eligible tiles and tile metadata.</param>
    /// <returns>The tile key to receive the grant, or null if no eligible tiles.</returns>
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        var tilesWithEstimates = context.EligibleTileKeys
            .Select(key => new
            {
                TileKey = key,
                Points = context.TilePoints[key],
                EstimatedTime = GetEstimatedCompletionTime(key, context)
            })
            .ToList();

        return tilesWithEstimates
            .OrderByDescending(t => t.Points)
            .ThenBy(t => t.EstimatedTime)
            .ThenBy(t => t.TileKey, StringComparer.Ordinal)
            .First()
            .TileKey;
    }

    /// <summary>
    /// Checks if the player can work on a tile and returns the first eligible (activityId, rule).
    /// Validates capability requirements and activity existence.
    /// </summary>
    private static (Guid activityId, TileActivityRuleSnapshotDto rule)? FindEligibleRule(
        TaskSelectionContext context,
        TileSnapshotDto tile)
    {
        foreach (var rule in tile.AllowedActivities)
        {
            if (rule.RequirementKeys.Count > 0 &&
                !rule.RequirementKeys.All(context.PlayerCapabilities.Contains))
            {
                continue;
            }

            var activity = context.EventSnapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
            if (activity is null || activity.Attempts.Count == 0)
                continue;

            return (rule.ActivityDefinitionId, rule);
        }

        return null;
    }

    /// <summary>
    /// Gets the estimated completion time for a tile in grant allocation context.
    /// Uses TileCompletionEstimator with the tile from the event snapshot.
    /// </summary>
    private static double GetEstimatedCompletionTime(string tileKey, GrantAllocationContext context)
    {
        var tile = FindTileByKey(tileKey, context.EventSnapshot);
        return tile is null
            ? double.MaxValue
            : TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot);
    }

    /// <summary>
    /// Finds a tile by key in the event snapshot.
    /// </summary>
    private static TileSnapshotDto? FindTileByKey(string tileKey, EventSnapshotDto snapshot)
    {
        foreach (var row in snapshot.Rows)
        {
            var tile = row.Tiles.FirstOrDefault(t => string.Equals(t.Key, tileKey, StringComparison.Ordinal));
            if (tile is not null)
                return tile;
        }

        return null;
    }
}
