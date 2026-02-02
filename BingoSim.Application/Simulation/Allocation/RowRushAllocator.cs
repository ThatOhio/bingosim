namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// RowRush: complete rows in order; prefer lowest row index, then lowest points (1 then 2 then 3 then 4).
/// Tie-break: tile key order (deterministic).
/// </summary>
public sealed class RowRushAllocator : IProgressAllocator
{
    public string? SelectTargetTile(AllocatorContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        return context.EligibleTileKeys
            .OrderBy(key => context.TileRowIndex[key])
            .ThenBy(key => context.TilePoints[key])
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }
}
