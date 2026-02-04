using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Context passed to ITeamStrategy.SelectTargetTileForGrant for grant allocation decisions.
/// Contains eligible tiles and team state for deciding which tile receives a progress grant.
/// </summary>
public sealed class GrantAllocationContext
{
    /// <summary>Row indices currently unlocked for this team (monotonic).</summary>
    public required IReadOnlySet<int> UnlockedRowIndices { get; init; }

    /// <summary>Tile key -> current progress units (not completed).</summary>
    public required IReadOnlyDictionary<string, int> TileProgress { get; init; }

    /// <summary>Tile key -> required count to complete.</summary>
    public required IReadOnlyDictionary<string, int> TileRequiredCount { get; init; }

    /// <summary>Tile key -> row index.</summary>
    public required IReadOnlyDictionary<string, int> TileRowIndex { get; init; }

    /// <summary>Tile key -> points (1-4).</summary>
    public required IReadOnlyDictionary<string, int> TilePoints { get; init; }

    /// <summary>Eligible tile keys: in unlocked row, accept DropKey, not yet completed.</summary>
    public required IReadOnlyList<string> EligibleTileKeys { get; init; }

    /// <summary>Event snapshot for tile lookup and completion time estimation.</summary>
    public required EventSnapshotDto EventSnapshot { get; init; }
}
