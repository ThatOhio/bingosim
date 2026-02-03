namespace BingoSim.Application.Interfaces;

/// <summary>
/// Options for perf scenario runs (synthetic snapshot, verbose, dump).
/// When null, default behavior is used.
/// </summary>
public interface IPerfScenarioOptions
{
    bool UseSyntheticSnapshot { get; }
    string? DumpSnapshotPath { get; }
    bool Verbose { get; }
}
