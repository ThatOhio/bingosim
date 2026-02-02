namespace BingoSim.Shared.Messages;

/// <summary>
/// Message to execute a single simulation run. Identifier-only; no snapshots over the bus.
/// </summary>
public record ExecuteSimulationRun
{
    public required Guid SimulationRunId { get; init; }
}
