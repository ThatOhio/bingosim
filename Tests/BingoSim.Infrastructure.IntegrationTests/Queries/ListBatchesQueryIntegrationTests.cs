using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using BingoSim.Infrastructure.Queries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BingoSim.Infrastructure.IntegrationTests.Queries;

[Collection("Postgres")]
public class ListBatchesQueryIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private AppDbContext _context = null!;
    private SimulationBatchRepository _batchRepo = null!;
    private EventSnapshotRepository _snapshotRepo = null!;
    private SimulationRunRepository _runRepo = null!;
    private IListBatchesQuery _query = null!;

    public ListBatchesQueryIntegrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        var connectionString = await _postgres.CreateIsolatedDatabaseAsync(GetType().Name);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _batchRepo = new SimulationBatchRepository(_context);
        _snapshotRepo = new EventSnapshotRepository(_context);
        _runRepo = new SimulationRunRepository(_context);
        _query = new ListBatchesQuery(_context, NullLogger<ListBatchesQuery>.Instance);
    }

    public void Dispose() => _context?.Dispose();

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _query.ExecuteAsync(new ListBatchesRequest { Top = 50 });

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithBatchAndSnapshot_ReturnsEventNameStatusCountsAndSeed()
    {
        var eventId = Guid.NewGuid();
        var batch = new SimulationBatch(eventId, 10, "seed-xyz", ExecutionMode.Local);
        await _batchRepo.AddAsync(batch);

        var snapshotJson = """{"EventName":"Winter Bingo 2025","DurationSeconds":3600,"UnlockPointsRequiredPerRow":5,"Rows":[],"ActivitiesById":{},"Teams":[]}""";
        var snapshot = new EventSnapshot(batch.Id, snapshotJson);
        await _snapshotRepo.AddAsync(snapshot);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed-xyz_0"),
            new(batch.Id, 1, "seed-xyz_1"),
            new(batch.Id, 2, "seed-xyz_2")
        };
        await _runRepo.AddRangeAsync(runs);
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        runs[1].MarkCompleted(DateTimeOffset.UtcNow);
        for (var i = 0; i < 5; i++)
            runs[2].MarkFailed("err", DateTimeOffset.UtcNow);
        foreach (var r in runs)
            await _runRepo.UpdateAsync(r);

        var freshOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_context.Database.GetConnectionString())
            .Options;
        await using var freshContext = new AppDbContext(freshOptions);
        var queryWithFreshContext = new ListBatchesQuery(freshContext, NullLogger<ListBatchesQuery>.Instance);
        var result = await queryWithFreshContext.ExecuteAsync(new ListBatchesRequest { Top = 50 });

        result.Items.Should().HaveCount(1);
        var row = result.Items[0];
        row.BatchId.Should().Be(batch.Id);
        row.CreatedAt.Should().BeCloseTo(batch.CreatedAt, TimeSpan.FromSeconds(1));
        row.Status.Should().Be(batch.Status);
        row.EventName.Should().Be("Winter Bingo 2025");
        row.RunCount.Should().Be(10);
        row.CompletedCount.Should().Be(2);
        row.FailedCount.Should().Be(1);
        row.Seed.Should().Be("seed-xyz");
        row.ExecutionMode.Should().Be(ExecutionMode.Local);
    }

    [Fact]
    public async Task ExecuteAsync_OrderedByCreatedAtDesc_NewestFirst()
    {
        var eventId = Guid.NewGuid();
        var batch1 = new SimulationBatch(eventId, 1, "a", ExecutionMode.Local);
        await _batchRepo.AddAsync(batch1);
        await _snapshotRepo.AddAsync(new EventSnapshot(batch1.Id, """{"EventName":"First","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        await Task.Delay(10);
        var batch2 = new SimulationBatch(eventId, 1, "b", ExecutionMode.Local);
        await _batchRepo.AddAsync(batch2);
        await _snapshotRepo.AddAsync(new EventSnapshot(batch2.Id, """{"EventName":"Second","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        var result = await _query.ExecuteAsync(new ListBatchesRequest { Top = 10 });

        result.Items.Should().HaveCount(2);
        result.Items[0].BatchId.Should().Be(batch2.Id);
        result.Items[0].EventName.Should().Be("Second");
        result.Items[1].BatchId.Should().Be(batch1.Id);
        result.Items[1].EventName.Should().Be("First");
    }

    [Fact]
    public async Task ExecuteAsync_StatusFilter_ReturnsOnlyMatchingBatches()
    {
        var eventId = Guid.NewGuid();
        var completed = new SimulationBatch(eventId, 1, "c", ExecutionMode.Local);
        await _batchRepo.AddAsync(completed);
        completed.SetCompleted(DateTimeOffset.UtcNow);
        await _batchRepo.UpdateAsync(completed);
        await _snapshotRepo.AddAsync(new EventSnapshot(completed.Id, """{"EventName":"Done","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        var running = new SimulationBatch(eventId, 1, "d", ExecutionMode.Local);
        running.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(running);
        await _snapshotRepo.AddAsync(new EventSnapshot(running.Id, """{"EventName":"InProgress","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        var result = await _query.ExecuteAsync(new ListBatchesRequest { Top = 50, StatusFilter = BatchStatus.Completed });

        result.Items.Should().HaveCount(1);
        result.Items[0].Status.Should().Be(BatchStatus.Completed);
        result.Items[0].EventName.Should().Be("Done");
    }

    [Fact]
    public async Task ExecuteAsync_EventNameSearch_FiltersByEventName()
    {
        var eventId = Guid.NewGuid();
        var batchWinter = new SimulationBatch(eventId, 1, "w", ExecutionMode.Local);
        await _batchRepo.AddAsync(batchWinter);
        await _snapshotRepo.AddAsync(new EventSnapshot(batchWinter.Id, """{"EventName":"Winter Bingo","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        var batchSpring = new SimulationBatch(eventId, 1, "s", ExecutionMode.Local);
        await _batchRepo.AddAsync(batchSpring);
        await _snapshotRepo.AddAsync(new EventSnapshot(batchSpring.Id, """{"EventName":"Spring League","DurationSeconds":0,"UnlockPointsRequiredPerRow":0,"Rows":[],"ActivitiesById":{},"Teams":[]}"""));

        var result = await _query.ExecuteAsync(new ListBatchesRequest { Top = 50, EventNameSearch = "Winter" });

        result.Items.Should().HaveCount(1);
        result.Items[0].EventName.Should().Be("Winter Bingo");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsTop()
    {
        var eventId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            var batch = new SimulationBatch(eventId, 1, $"seed{i}", ExecutionMode.Local);
            await _batchRepo.AddAsync(batch);
            var json = $"{{\"EventName\":\"Event{i}\",\"DurationSeconds\":0,\"UnlockPointsRequiredPerRow\":0,\"Rows\":[],\"ActivitiesById\":{{}},\"Teams\":[]}}";
            await _snapshotRepo.AddAsync(new EventSnapshot(batch.Id, json));
        }

        var result = await _query.ExecuteAsync(new ListBatchesRequest { Top = 3 });

        result.Items.Should().HaveCount(3);
    }
}
