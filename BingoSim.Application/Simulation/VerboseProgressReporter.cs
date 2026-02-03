namespace BingoSim.Application.Simulation;

/// <summary>
/// Logs simulation progress to Console every N iterations or on fast-forward.
/// Used when --perf-verbose is enabled.
/// </summary>
public sealed class VerboseProgressReporter(int logEveryN) : ISimulationProgressReporter
{
    private int _iterationCount;

    public void Report(int simTime, int? nextSimTime, int eventQueueCount, int onlinePlayersCount)
    {
        _iterationCount++;
        if (_iterationCount % logEveryN == 0)
        {
            var nextStr = nextSimTime.HasValue ? nextSimTime.Value.ToString() : "-";
            Console.WriteLine($"[perf-verbose] iter={_iterationCount} simTime={simTime} nextSimTime={nextStr} queue={eventQueueCount} online={onlinePlayersCount}");
        }
    }
}
