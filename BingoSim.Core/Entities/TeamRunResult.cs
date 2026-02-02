namespace BingoSim.Core.Entities;

/// <summary>
/// Per-run, per-team aggregates and timeline data.
/// </summary>
public class TeamRunResult
{
    public Guid Id { get; private set; }
    public Guid SimulationRunId { get; private set; }
    public Guid TeamId { get; private set; }
    public string TeamName { get; private set; } = string.Empty;
    public string StrategyKey { get; private set; } = string.Empty;
    public string? StrategyParamsJson { get; private set; }
    public int TotalPoints { get; private set; }
    public int TilesCompletedCount { get; private set; }
    public int RowReached { get; private set; }
    public bool IsWinner { get; private set; }
    /// <summary>JSON: Dictionary&lt;int, int&gt; row index -> unlocked at sim time seconds.</summary>
    public string RowUnlockTimesJson { get; private set; } = "{}";
    /// <summary>JSON: Dictionary&lt;string, int&gt; tile key -> completed at sim time seconds.</summary>
    public string TileCompletionTimesJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private TeamRunResult() { }

    public TeamRunResult(
        Guid simulationRunId,
        Guid teamId,
        string teamName,
        string strategyKey,
        string? strategyParamsJson,
        int totalPoints,
        int tilesCompletedCount,
        int rowReached,
        bool isWinner,
        string rowUnlockTimesJson,
        string tileCompletionTimesJson)
    {
        if (simulationRunId == default)
            throw new ArgumentException("SimulationRunId cannot be empty.", nameof(simulationRunId));

        ArgumentNullException.ThrowIfNull(teamName);
        ArgumentNullException.ThrowIfNull(strategyKey);
        ArgumentNullException.ThrowIfNull(rowUnlockTimesJson);
        ArgumentNullException.ThrowIfNull(tileCompletionTimesJson);

        Id = Guid.NewGuid();
        SimulationRunId = simulationRunId;
        TeamId = teamId;
        TeamName = teamName;
        StrategyKey = strategyKey;
        StrategyParamsJson = string.IsNullOrWhiteSpace(strategyParamsJson) ? null : strategyParamsJson;
        TotalPoints = totalPoints;
        TilesCompletedCount = tilesCompletedCount;
        RowReached = rowReached;
        IsWinner = isWinner;
        RowUnlockTimesJson = rowUnlockTimesJson;
        TileCompletionTimesJson = tileCompletionTimesJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
