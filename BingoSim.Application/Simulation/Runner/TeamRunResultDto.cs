namespace BingoSim.Application.Simulation.Runner;

/// <summary>
/// Per-team result of one simulation run (aggregates + timeline JSON).
/// </summary>
public sealed class TeamRunResultDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string StrategyKey { get; init; } = string.Empty;
    public string? ParamsJson { get; init; }
    public int TotalPoints { get; init; }
    public int TilesCompletedCount { get; init; }
    public int RowReached { get; init; }
    public bool IsWinner { get; init; }
    public string RowUnlockTimesJson { get; init; } = "{}";
    public string TileCompletionTimesJson { get; init; } = "{}";
}
