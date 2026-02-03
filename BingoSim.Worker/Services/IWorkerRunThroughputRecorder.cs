namespace BingoSim.Worker.Services;

/// <summary>
/// Records run completions for periodic throughput logging. Worker-only; not in Core/Application.
/// </summary>
public interface IWorkerRunThroughputRecorder
{
    void RecordRunCompleted();

    /// <summary>
    /// Gets and resets the count. Returns the number of runs since last call.
    /// </summary>
    long TakeAndReset();
}
