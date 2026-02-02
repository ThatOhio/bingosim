namespace BingoSim.Core.Entities;

/// <summary>
/// Strategy selection with optional parameters for a team. 1:1 with Team.
/// </summary>
public class StrategyConfig
{
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public string StrategyKey { get; private set; } = string.Empty;
    public string? ParamsJson { get; private set; }

    /// <summary>Navigation for EF Core.</summary>
    public Team? Team { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private StrategyConfig() { }

    public StrategyConfig(Guid teamId, string strategyKey, string? paramsJson = null)
    {
        if (teamId == default)
            throw new ArgumentException("TeamId cannot be empty.", nameof(teamId));

        if (string.IsNullOrWhiteSpace(strategyKey))
            throw new ArgumentException("StrategyKey cannot be empty.", nameof(strategyKey));

        Id = Guid.NewGuid();
        TeamId = teamId;
        StrategyKey = strategyKey.Trim();
        ParamsJson = string.IsNullOrWhiteSpace(paramsJson) ? null : paramsJson.Trim();
    }

    public void Update(string strategyKey, string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(strategyKey))
            throw new ArgumentException("StrategyKey cannot be empty.", nameof(strategyKey));

        StrategyKey = strategyKey.Trim();
        ParamsJson = string.IsNullOrWhiteSpace(paramsJson) ? null : paramsJson.Trim();
    }
}
