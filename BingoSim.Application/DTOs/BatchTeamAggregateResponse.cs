namespace BingoSim.Application.DTOs;

public sealed class BatchTeamAggregateResponse
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string StrategyKey { get; init; } = string.Empty;
    public double MeanPoints { get; init; }
    public int MinPoints { get; init; }
    public int MaxPoints { get; init; }
    public double MeanTilesCompleted { get; init; }
    public int MinTilesCompleted { get; init; }
    public int MaxTilesCompleted { get; init; }
    public double MeanRowReached { get; init; }
    public int MinRowReached { get; init; }
    public int MaxRowReached { get; init; }
    public double WinnerRate { get; init; }
    public int RunCount { get; init; }
}
