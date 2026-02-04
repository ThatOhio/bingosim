using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Strategy for team-level decisions: grant allocation and task selection.
/// Per-team; called by runner when a grant occurs or when assigning work to players.
/// </summary>
public interface ITeamStrategy
{
    /// <summary>
    /// Returns the tile key that should receive the full grant (single tile in v1).
    /// Used for grant allocation only — not for task assignment.
    /// If no eligible tile, returns null (grant is dropped).
    /// </summary>
    string? SelectTargetTileForGrant(GrantAllocationContext context);

    /// <summary>
    /// Selects which activity/tile a player should work on next.
    /// Returns (activityId, rule) or null if no eligible task.
    /// </summary>
    (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context);
}
