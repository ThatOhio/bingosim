namespace BingoSim.Application.Interfaces;

/// <summary>
/// Basic metrics for simulation runs (completed, failed, retried, batch duration). Exposed for observability.
/// </summary>
public interface ISimulationMetrics
{
    void RecordRunCompleted(Guid batchId, Guid runId);
    void RecordRunFailed(Guid batchId, Guid runId);
    void RecordRunRetried(Guid batchId, Guid runId);
    void RecordBatchCompleted(Guid batchId, TimeSpan duration);
}
