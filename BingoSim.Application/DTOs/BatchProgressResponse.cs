namespace BingoSim.Application.DTOs;

public sealed class BatchProgressResponse
{
    public int Completed { get; init; }
    public int Failed { get; init; }
    public int Running { get; init; }
    public int Pending { get; init; }
    /// <summary>Total retry attempts across all runs (sum of max(0, AttemptCount - 1) per run).</summary>
    public int RetryCount { get; init; }
    /// <summary>Elapsed seconds since batch started (or until completed if terminal).</summary>
    public double ElapsedSeconds { get; init; }
    /// <summary>Estimated runs per second (completed + failed) / elapsed, or 0 if elapsed &lt;= 0.</summary>
    public double RunsPerSecond { get; init; }
}
