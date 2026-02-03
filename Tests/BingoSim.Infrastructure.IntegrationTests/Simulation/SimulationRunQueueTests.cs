using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure.Simulation;
using FluentAssertions;

namespace BingoSim.Infrastructure.IntegrationTests.Simulation;

public class SimulationRunQueueTests
{
    [Fact]
    public async Task EnqueueBatchAsync_MultipleRunIds_AllDequeuedInOrder()
    {
        var queue = new SimulationRunQueue();
        var runIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        await queue.EnqueueBatchAsync(runIds);

        var dequeued = new List<Guid>();
        for (var i = 0; i < runIds.Count; i++)
        {
            var id = await queue.DequeueAsync();
            id.Should().NotBeNull();
            dequeued.Add(id!.Value);
        }

        dequeued.Should().BeEquivalentTo(runIds);
    }

    [Fact]
    public async Task EnqueueBatchAsync_EmptyList_DoesNotThrow()
    {
        var queue = new SimulationRunQueue();

        var act = async () => await queue.EnqueueBatchAsync([]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishRunWorkBatchAsync_MultipleRunIds_AllDequeued()
    {
        var queue = new SimulationRunQueue();
        var runIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await queue.PublishRunWorkBatchAsync(runIds);

        var dequeued = new List<Guid>();
        for (var i = 0; i < runIds.Count; i++)
        {
            var id = await queue.DequeueAsync();
            id.Should().NotBeNull();
            dequeued.Add(id!.Value);
        }
        dequeued.Should().BeEquivalentTo(runIds);
    }
}
