using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// GreedyPoints: maximize total points; prefer highest points (4 then 3 then 2 then 1), then lowest row index.
/// Tie-break: tile key order (deterministic).
/// </summary>
public sealed class GreedyPointsAllocator : ITeamStrategy
{
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        return context.EligibleTileKeys
            .OrderByDescending(key => context.TilePoints[key])
            .ThenBy(key => context.TileRowIndex[key])
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }

    /// <inheritdoc />
    public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
    {
        foreach (var row in context.EventSnapshot.Rows.OrderBy(r => r.Index))
        {
            if (!context.UnlockedRowIndices.Contains(row.Index))
                continue;
            foreach (var tile in row.Tiles.OrderByDescending(t => t.Points))
            {
                if (context.CompletedTiles.Contains(tile.Key))
                    continue;
                foreach (var rule in tile.AllowedActivities)
                {
                    if (rule.RequirementKeys.Count > 0 && !rule.RequirementKeys.All(context.PlayerCapabilities.Contains))
                        continue;
                    var activity = context.EventSnapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
                    if (activity is null || activity.Attempts.Count == 0)
                        continue;
                    return (rule.ActivityDefinitionId, rule);
                }
            }
        }
        return null;
    }
}
