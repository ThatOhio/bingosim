using System.Threading.Channels;
using BingoSim.Application.Interfaces;

namespace BingoSim.Infrastructure.Simulation;

/// <summary>
/// Bounded channel-based queue for simulation run work items. Web enqueues; hosted service dequeues.
/// Implements ISimulationRunWorkPublisher for local mode (publish = enqueue).
/// </summary>
public sealed class SimulationRunQueue : ISimulationRunQueue, ISimulationRunWorkPublisher
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(100_000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(runId, cancellationToken);

    public async ValueTask EnqueueBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return;
        await Task.WhenAll(runIds.Select(id => EnqueueAsync(id, cancellationToken).AsTask()));
    }

    public ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default) =>
        EnqueueAsync(runId, cancellationToken);

    public ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default) =>
        EnqueueBatchAsync(runIds, cancellationToken);

    public async ValueTask<Guid?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        if (await _channel.Reader.WaitToReadAsync(cancellationToken) && _channel.Reader.TryRead(out var runId))
            return runId;
        return null;
    }
}
