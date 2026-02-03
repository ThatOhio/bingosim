namespace BingoSim.Application.Simulation;

/// <summary>
/// Optional callback for simulation progress (e.g. verbose perf logging).
/// </summary>
public interface ISimulationProgressReporter
{
    void Report(int simTime, int? nextSimTime, int eventQueueCount, int onlinePlayersCount);
}
