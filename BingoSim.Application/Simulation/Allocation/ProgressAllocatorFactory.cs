using BingoSim.Application.StrategyKeys;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Returns RowRush or GreedyPoints allocator by strategy key; defaults to RowRush for unknown keys.
/// </summary>
public sealed class ProgressAllocatorFactory : IProgressAllocatorFactory
{
    private readonly IReadOnlyDictionary<string, ITeamStrategy> _allocators;

    public ProgressAllocatorFactory()
    {
        _allocators = new Dictionary<string, ITeamStrategy>(StringComparer.Ordinal)
        {
            [StrategyCatalog.RowRush] = new RowRushAllocator(),
            [StrategyCatalog.GreedyPoints] = new GreedyPointsAllocator()
        };
    }

    public ITeamStrategy GetAllocator(string strategyKey)
    {
        if (!string.IsNullOrWhiteSpace(strategyKey) && _allocators.TryGetValue(strategyKey.Trim(), out var allocator))
            return allocator;
        return _allocators[StrategyCatalog.RowRush];
    }
}
