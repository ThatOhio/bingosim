namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// GreedyPoints: maximize total points; prefer highest points (4 then 3 then 2 then 1), then lowest row index.
/// Tie-break: tile key order (deterministic).
/// </summary>
public sealed class GreedyPointsAllocator : IProgressAllocator
{
    public string? SelectTargetTile(AllocatorContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        return context.EligibleTileKeys
            .OrderByDescending(key => context.TilePoints[key])
            .ThenBy(key => context.TileRowIndex[key])
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }
}
