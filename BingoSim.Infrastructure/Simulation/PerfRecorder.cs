using System.Collections.Concurrent;
using BingoSim.Application.Interfaces;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Accumulates phase timing for performance measurement. Thread-safe for concurrent runs.
/// </summary>
public sealed class PerfRecorder : IPerfRecorder
{
    private readonly ConcurrentDictionary<string, (long TotalMs, int Count)> _totals = new();

    public void Record(string phase, long elapsedMs, int count)
    {
        _totals.AddOrUpdate(phase, (elapsedMs, count), (_, prev) => (prev.TotalMs + elapsedMs, prev.Count + count));
    }

    public IReadOnlyDictionary<string, (long TotalMs, int Count)> GetTotals() =>
        new Dictionary<string, (long TotalMs, int Count)>(_totals);

    public void Reset() => _totals.Clear();
}
