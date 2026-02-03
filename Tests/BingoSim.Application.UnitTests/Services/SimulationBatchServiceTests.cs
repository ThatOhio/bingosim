using System.Reflection;
using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Services;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
        var runQueue = Substitute.For<ISimulationRunQueue>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var listBatchesQuery = Substitute.For<IListBatchesQuery>();
        var logger = Substitute.For<ILogger<SimulationBatchService>>();

        var service = new SimulationBatchService(
            eventRepo, teamRepo, batchRepo, snapshotRepo, runRepo, resultRepo, aggregateRepo,
            snapshotBuilder, runQueue, scopeFactory, listBatchesQuery, logger);

        var progress = await service.GetProgressAsync(batchId);

        progress.Completed.Should().Be(1);
        progress.Failed.Should().Be(2);
        progress.RetryCount.Should().Be(8);
        progress.ElapsedSeconds.Should().BeGreaterThanOrEqualTo(10);
        progress.RunsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBatchesAsync_DelegatesToQuery_ReturnsResult()
    {
        var request = new ListBatchesRequest { Top = 50, StatusFilter = BatchStatus.Completed };
        var expectedItems = new List<BatchListRowDto>
        {
            new()
            {
                BatchId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                Status = BatchStatus.Completed,
                EventName = "Winter Bingo",
                RunCount = 100,
                CompletedCount = 100,
                FailedCount = 0,
                Seed = "abc",
                ExecutionMode = ExecutionMode.Local
            }
        };
        var expectedResult = new ListBatchesResult { Items = expectedItems };

        var listBatchesQuery = Substitute.For<IListBatchesQuery>();
        listBatchesQuery.ExecuteAsync(Arg.Any<ListBatchesRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var batchRepo = Substitute.For<ISimulationBatchRepository>();
        var runRepo = Substitute.For<ISimulationRunRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var snapshotRepo = Substitute.For<IEventSnapshotRepository>();
        var resultRepo = Substitute.For<ITeamRunResultRepository>();
        var aggregateRepo = Substitute.For<IBatchTeamAggregateRepository>();
        var snapshotBuilder = new EventSnapshotBuilder(eventRepo, teamRepo, activityRepo, playerRepo);
        var runQueue = Substitute.For<ISimulationRunQueue>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var logger = Substitute.For<ILogger<SimulationBatchService>>();

        var service = new SimulationBatchService(
            eventRepo, teamRepo, batchRepo, snapshotRepo, runRepo, resultRepo, aggregateRepo,
            snapshotBuilder, runQueue, scopeFactory, listBatchesQuery, logger);

        var result = await service.GetBatchesAsync(request);

        result.Items.Should().HaveCount(1);
        result.Items[0].BatchId.Should().Be(expectedItems[0].BatchId);
        result.Items[0].EventName.Should().Be("Winter Bingo");
        result.Items[0].CompletedCount.Should().Be(100);
        result.Items[0].FailedCount.Should().Be(0);
        result.Items[0].Status.Should().Be(BatchStatus.Completed);
        await listBatchesQuery.Received(1).ExecuteAsync(Arg.Is<ListBatchesRequest>(r =>
            r.Top == 50 && r.StatusFilter == BatchStatus.Completed), Arg.Any<CancellationToken>());
    }

    private static void SetCreatedAt(SimulationBatch batch, DateTimeOffset value)
    {
        typeof(SimulationBatch).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(batch, value);
    }
}
