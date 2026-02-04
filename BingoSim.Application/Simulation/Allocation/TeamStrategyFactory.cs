using BingoSim.Application.StrategyKeys;

namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Returns RowRush or GreedyPoints strategy by strategy key; defaults to RowRush for unknown keys.
/// </summary>
public sealed class TeamStrategyFactory : ITeamStrategyFactory
{
    private readonly IReadOnlyDictionary<string, ITeamStrategy> _strategies;

    public TeamStrategyFactory()
    {
        _strategies = new Dictionary<string, ITeamStrategy>(StringComparer.Ordinal)
        {
            [StrategyCatalog.RowRush] = new RowRushAllocator(),
            [StrategyCatalog.GreedyPoints] = new GreedyPointsAllocator()
        };
    }

    public ITeamStrategy GetStrategy(string strategyKey)
    {
        if (!string.IsNullOrWhiteSpace(strategyKey) && _strategies.TryGetValue(strategyKey.Trim(), out var strategy))
            return strategy;
        return _strategies[StrategyCatalog.RowRush];
    }
}
