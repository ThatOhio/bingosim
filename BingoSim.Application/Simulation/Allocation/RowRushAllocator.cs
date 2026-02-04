using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// RowRush: complete rows in order; prefer lowest row index, then lowest points (1 then 2 then 3 then 4).
/// Tie-break: tile key order (deterministic).
/// </summary>
public sealed class RowRushAllocator : ITeamStrategy
{
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        return context.EligibleTileKeys
            .OrderBy(key => context.TileRowIndex[key])
            .ThenBy(key => context.TilePoints[key])
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
            foreach (var tile in row.Tiles.OrderBy(t => t.Points))
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
