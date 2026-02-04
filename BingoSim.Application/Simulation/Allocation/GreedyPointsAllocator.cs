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
        // TODO: Remove this placeholder - strategy will be deleted
        return null;
    }
}
