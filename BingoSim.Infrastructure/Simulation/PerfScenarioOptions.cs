using BingoSim.Application.Interfaces;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Implementation of IPerfScenarioOptions for perf scenario runs.
/// </summary>
public sealed class PerfScenarioOptions : IPerfScenarioOptions
{
    public bool UseSyntheticSnapshot { get; init; }
    public string? DumpSnapshotPath { get; init; }
    public bool Verbose { get; init; }
}
