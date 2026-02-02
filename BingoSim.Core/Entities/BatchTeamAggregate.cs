namespace BingoSim.Core.Entities;

/// <summary>
/// Precomputed per-team aggregates for a batch (mean/min/max, winner rate).
/// </summary>
public class BatchTeamAggregate
{
    public Guid Id { get; private set; }
    public Guid SimulationBatchId { get; private set; }
    public Guid TeamId { get; private set; }
    public string TeamName { get; private set; } = string.Empty;
    public string StrategyKey { get; private set; } = string.Empty;
    public double MeanPoints { get; private set; }
    public int MinPoints { get; private set; }
    public int MaxPoints { get; private set; }
    public double MeanTilesCompleted { get; private set; }
    public int MinTilesCompleted { get; private set; }
    public int MaxTilesCompleted { get; private set; }
    public double MeanRowReached { get; private set; }
    public int MinRowReached { get; private set; }
    public int MaxRowReached { get; private set; }
    public double WinnerRate { get; private set; }
    public int RunCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private BatchTeamAggregate() { }

    public BatchTeamAggregate(
        Guid simulationBatchId,
        Guid teamId,
        string teamName,
        string strategyKey,
        double meanPoints,
        int minPoints,
        int maxPoints,
        double meanTilesCompleted,
        int minTilesCompleted,
        int maxTilesCompleted,
        double meanRowReached,
        int minRowReached,
        int maxRowReached,
        double winnerRate,
        int runCount)
    {
        if (simulationBatchId == default)
            throw new ArgumentException("SimulationBatchId cannot be empty.", nameof(simulationBatchId));

        ArgumentNullException.ThrowIfNull(teamName);
        ArgumentNullException.ThrowIfNull(strategyKey);

        Id = Guid.NewGuid();
        SimulationBatchId = simulationBatchId;
        TeamId = teamId;
        TeamName = teamName;
        StrategyKey = strategyKey;
        MeanPoints = meanPoints;
        MinPoints = minPoints;
        MaxPoints = maxPoints;
        MeanTilesCompleted = meanTilesCompleted;
        MinTilesCompleted = minTilesCompleted;
        MaxTilesCompleted = maxTilesCompleted;
        MeanRowReached = meanRowReached;
        MinRowReached = minRowReached;
        MaxRowReached = maxRowReached;
        WinnerRate = winnerRate;
        RunCount = runCount;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
