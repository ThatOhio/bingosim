namespace BingoSim.Application.Interfaces;

/// <summary>
/// Publishes simulation run work for execution. Guid-based; no message types.
/// Local: enqueues to in-process channel. Distributed: publishes to message bus.
/// </summary>
public interface ISimulationRunWorkPublisher
{
    ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple run work items in a batch. More efficient than calling PublishRunWorkAsync in a loop.
    /// </summary>
    ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default);
}
