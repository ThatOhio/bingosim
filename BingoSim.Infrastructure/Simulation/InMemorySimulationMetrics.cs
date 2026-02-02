using System.Collections.Concurrent;
using BingoSim.Application.Interfaces;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// In-memory implementation of ISimulationMetrics for observability (runs completed/failed/retried, batch duration).
/// Suitable for diagnosing throughput; can be replaced with System.Diagnostics.Metrics in production.
/// </summary>
public class InMemorySimulationMetrics : ISimulationMetrics
{
    private readonly ConcurrentDictionary<Guid, BatchMetrics> _batchMetrics = new();

    public void RecordRunCompleted(Guid batchId, Guid runId)
    {
        _batchMetrics.AddOrUpdate(batchId, _ => new BatchMetrics { Completed = 1 }, (_, m) => m with { Completed = m.Completed + 1 });
    }

    public void RecordRunFailed(Guid batchId, Guid runId)
    {
        _batchMetrics.AddOrUpdate(batchId, _ => new BatchMetrics { Failed = 1 }, (_, m) => m with { Failed = m.Failed + 1 });
    }

    public void RecordRunRetried(Guid batchId, Guid runId)
    {
        _batchMetrics.AddOrUpdate(batchId, _ => new BatchMetrics { Retried = 1 }, (_, m) => m with { Retried = m.Retried + 1 });
    }

    public void RecordBatchCompleted(Guid batchId, TimeSpan duration)
    {
        _batchMetrics.AddOrUpdate(batchId, _ => new BatchMetrics { Duration = duration }, (_, m) => m with { Duration = duration });
    }

    /// <summary>
    /// Gets a snapshot of metrics for a batch, if recorded.
    /// </summary>
    public BatchMetricsSnapshot? GetBatchSnapshot(Guid batchId)
    {
        return _batchMetrics.TryGetValue(batchId, out var m)
            ? new BatchMetricsSnapshot(m.Completed, m.Failed, m.Retried, m.Duration)
            : null;
    }

    private sealed record BatchMetrics(int Completed = 0, int Failed = 0, int Retried = 0, TimeSpan? Duration = null);
}

/// <summary>
/// Snapshot of metrics for a batch (for UI or diagnostics).
/// </summary>
public record BatchMetricsSnapshot(int Completed, int Failed, int Retried, TimeSpan? Duration)
{
    public double RunsPerSecond =>
        Duration is { TotalSeconds: > 0 } d ? (Completed + Failed) / d.TotalSeconds : 0;
}
