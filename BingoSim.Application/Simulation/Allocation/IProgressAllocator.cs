namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Strategy for allocating a progress grant to the best eligible tile. Per-team; called by runner when a grant occurs.
/// </summary>
public interface IProgressAllocator
{
    /// <summary>
    /// Returns the tile key that should receive the full grant (single tile in v1).
    /// If no eligible tile, returns null (grant is dropped).
    /// </summary>
    string? SelectTargetTile(AllocatorContext context);
}
