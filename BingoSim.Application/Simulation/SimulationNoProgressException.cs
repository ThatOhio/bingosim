namespace BingoSim.Application.Simulation;

/// <summary>
/// Thrown when the simulation loop detects no progress (e.g. fast-forward stuck).
/// Includes diagnostics for debugging schedule/snapshot issues.
/// </summary>
public sealed class SimulationNoProgressException : InvalidOperationException
{
    public int SimTime { get; }
    public int? NextSimTime { get; }
    public string SimTimeEt { get; }
    public string? NextSimTimeEt { get; }
    public int OnlinePlayersCount { get; }
    public string? Diagnostics { get; }

    public SimulationNoProgressException(
        string message,
        int simTime,
        int? nextSimTime,
        string simTimeEt,
        string? nextSimTimeEt,
        int onlinePlayersCount,
        string? diagnostics = null)
        : base(message)
    {
        SimTime = simTime;
        NextSimTime = nextSimTime;
        SimTimeEt = simTimeEt;
        NextSimTimeEt = nextSimTimeEt;
        OnlinePlayersCount = onlinePlayersCount;
        Diagnostics = diagnostics;
    }
}
