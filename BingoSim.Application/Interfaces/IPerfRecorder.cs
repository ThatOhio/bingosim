namespace BingoSim.Application.Interfaces;

/// <summary>
/// Records phase timing for performance measurement. Phase totals (ms + count) rather than per-run averages.
/// </summary>
public interface IPerfRecorder
{
    void Record(string phase, long elapsedMs, int count);
}
