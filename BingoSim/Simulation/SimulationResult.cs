using BingoSim.Models;

namespace BingoSim.Simulation;

public class TileCompletion
{
    public string TileId { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public int Points { get; set; }
    public double CompletionTimeMinutes { get; set; }
}

public class RunResult
{
    public double TotalTimeMinutes { get; set; }
    public int TotalPoints { get; set; }
    public List<TileCompletion> CompletionOrder { get; set; } = new();
    public Dictionary<int, double> RowUnlockTimesMinutes { get; set; } = new();
}

public class AggregateResult
{
    public int Runs { get; set; }
    public double AvgTotalTimeMinutes { get; set; }
    public double StdDevTotalTimeMinutes { get; set; }
    public double AvgPoints { get; set; }
}
