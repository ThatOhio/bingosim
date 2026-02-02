using System.Reflection;
using BingoSim.Application.Services;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class SimulationBatchServiceTests
{
    [Fact]
    public async Task GetProgressAsync_WithBatchAndRuns_ReturnsRetryCountElapsedAndRunsPerSecond()
    {
        var eventId = Guid.NewGuid();
        var batch = new SimulationBatch(eventId, 3, "myseed", ExecutionMode.Local);
        var batchId = batch.Id;
        SetCreatedAt(batch, DateTimeOffset.UtcNow.AddSeconds(-10));

        var run0 = new SimulationRun(batchId, 0, "myseed_0");
        run0.MarkCompleted(DateTimeOffset.UtcNow);
        var run1 = new SimulationRun(batchId, 1, "myseed_1");
        for (var i = 0; i < 5; i++)
            run1.MarkFailed("err", DateTimeOffset.UtcNow);
        var run2 = new SimulationRun(batchId, 2, "myseed_2");
        for (var i = 0; i < 5; i++)
            run2.MarkFailed("err", DateTimeOffset.UtcNow);

        var batchRepo = Substitute.For<ISimulationBatchRepository>();
        var runRepo = Substitute.For<ISimulationRunRepository>();
        batchRepo.GetByIdAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
        runRepo.GetByBatchIdAsync(batchId, Arg.Any<CancellationToken>()).Returns([run0, run1, run2]);

        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var snapshotRepo = Substitute.For<IEventSnapshotRepository>();
        var resultRepo = Substitute.For<ITeamRunResultRepository>();
        var aggregateRepo = Substitute.For<IBatchTeamAggregateRepository>();
        var snapshotBuilder = new EventSnapshotBuilder(eventRepo, teamRepo, activityRepo, playerRepo);
        var runQueue = Substitute.For<BingoSim.Application.Interfaces.ISimulationRunQueue>();
        var logger = Substitute.For<ILogger<SimulationBatchService>>();

        var service = new SimulationBatchService(
            eventRepo, teamRepo, batchRepo, snapshotRepo, runRepo, resultRepo, aggregateRepo,
            snapshotBuilder, runQueue, logger);

        var progress = await service.GetProgressAsync(batchId);

        progress.Completed.Should().Be(1);
        progress.Failed.Should().Be(2);
        progress.RetryCount.Should().Be(8);
        progress.ElapsedSeconds.Should().BeGreaterThanOrEqualTo(10);
        progress.RunsPerSecond.Should().BeGreaterThan(0);
    }

    private static void SetCreatedAt(SimulationBatch batch, DateTimeOffset value)
    {
        typeof(SimulationBatch).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(batch, value);
    }
}
