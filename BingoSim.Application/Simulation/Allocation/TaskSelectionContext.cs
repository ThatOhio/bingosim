using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Context passed to ITeamStrategy.SelectTaskForPlayer for task assignment decisions.
/// Provides all data needed to decide which activity/tile a player should work on.
/// </summary>
public sealed class TaskSelectionContext
{
    /// <summary>Player index within the team (0-based).</summary>
    public required int PlayerIndex { get; init; }

    /// <summary>Player capabilities as a set for efficient lookup.</summary>
    public required IReadOnlySet<string> PlayerCapabilities { get; init; }

    /// <summary>Event snapshot reference.</summary>
    public required EventSnapshotDto EventSnapshot { get; init; }

    /// <summary>Team snapshot reference.</summary>
    public required TeamSnapshotDto TeamSnapshot { get; init; }

    /// <summary>Row indices currently unlocked for this team (monotonic).</summary>
    public required IReadOnlySet<int> UnlockedRowIndices { get; init; }

    /// <summary>Tile key -> current progress units (not completed).</summary>
    public required IReadOnlyDictionary<string, int> TileProgress { get; init; }

    /// <summary>Tile key -> required count to complete.</summary>
    public required IReadOnlyDictionary<string, int> TileRequiredCount { get; init; }

    /// <summary>Tile keys that are already completed.</summary>
    public required IReadOnlySet<string> CompletedTiles { get; init; }

    /// <summary>Tile key -> row index.</summary>
    public required IReadOnlyDictionary<string, int> TileRowIndex { get; init; }

    /// <summary>Tile key -> points (1-4).</summary>
    public required IReadOnlyDictionary<string, int> TilePoints { get; init; }

    /// <summary>Tile key -> activity rules for that tile.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>> TileToRules { get; init; }
}
