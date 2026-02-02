namespace BingoSim.Application.DTOs;

public sealed class TeamRunResultResponse
{
    public Guid SimulationRunId { get; init; }
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string StrategyKey { get; init; } = string.Empty;
    public string? StrategyParamsJson { get; init; }
    public int TotalPoints { get; init; }
    public int TilesCompletedCount { get; init; }
    public int RowReached { get; init; }
    public bool IsWinner { get; init; }
    public string RowUnlockTimesJson { get; init; } = "{}";
    public string TileCompletionTimesJson { get; init; } = "{}";
}
