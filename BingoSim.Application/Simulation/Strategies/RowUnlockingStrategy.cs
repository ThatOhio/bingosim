using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Strategy focused on unlocking the next row as quickly as possible.
/// Task assignment prioritizes tiles in the optimal combination for unlocking the next row.
/// Grant allocation prioritizes highest point tiles on the furthest unlocked row.
/// </summary>
public sealed class RowUnlockingStrategy : ITeamStrategy
{
    /// <inheritdoc />
    public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
    {
        // TODO: Implement in next phase
        // For now, return null to avoid crashes
        return null;
    }

    /// <inheritdoc />
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        // TODO: Implement in next phase
        // For now, return null to avoid crashes
        return null;
    }
}
