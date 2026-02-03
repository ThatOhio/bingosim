using System.Collections.Concurrent;

namespace BingoSim.Worker.Services;

/// <summary>
/// Thread-safe recorder for run completions. Used by WorkerThroughputHostedService for periodic logging.
/// </summary>
public sealed class WorkerRunThroughputRecorder : IWorkerRunThroughputRecorder
{
    private long _runCount;

    public void RecordRunCompleted() => Interlocked.Increment(ref _runCount);

    /// <summary>
    /// Gets and resets the count. Returns the number of runs since last call.
    /// </summary>
    public long TakeAndReset() => Interlocked.Exchange(ref _runCount, 0);
}
