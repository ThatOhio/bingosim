using BingoSim.Models;

namespace BingoSim.Simulation;

public class TileCompletion
{
    public string TileId { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public int Points { get; set; }
    // Stored in seconds
    public double CompletionTimeSeconds { get; set; }
}

public class RunResult
{
    // Stored in seconds
    public double TotalTimeSeconds { get; set; }
    public int TotalPoints { get; set; }
    public List<TileCompletion> CompletionOrder { get; set; } = new();
    // Row index -> unlock time in seconds
    public Dictionary<int, double> RowUnlockTimesSeconds { get; set; } = new();
}

public class AggregateResult
{
    public int Runs { get; set; }
    public double AvgTotalTimeSeconds { get; set; }
    public double StdDevTotalTimeSeconds { get; set; }
    public double AvgPoints { get; set; }
}
