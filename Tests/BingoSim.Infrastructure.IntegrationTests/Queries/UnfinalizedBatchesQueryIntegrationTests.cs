using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using BingoSim.Infrastructure.Queries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Queries;

public class UnfinalizedBatchesQueryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private SimulationBatchRepository _batchRepo = null!;
    private SimulationRunRepository _runRepo = null!;
    private IUnfinalizedBatchesQuery _query = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _batchRepo = new SimulationBatchRepository(_context);
        _runRepo = new SimulationRunRepository(_context);
        _query = new UnfinalizedBatchesQuery(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task GetBatchIdsAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _query.GetBatchIdsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatchIdsAsync_BatchPendingWithAllTerminalRuns_ReturnsBatchId()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "seed", ExecutionMode.Distributed);
        batch.SetStatus(BatchStatus.Pending);
        await _batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed_0"),
            new(batch.Id, 1, "seed_1")
        };
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        for (var i = 0; i < 5; i++)
            runs[1].MarkFailed("err", DateTimeOffset.UtcNow);
        await _runRepo.AddRangeAsync(runs);
        foreach (var r in runs)
            await _runRepo.UpdateAsync(r);

        var result = await _query.GetBatchIdsAsync();

        result.Should().ContainSingle().Which.Should().Be(batch.Id);
    }

    [Fact]
    public async Task GetBatchIdsAsync_BatchRunningWithAllTerminalRuns_ReturnsBatchId()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "seed", ExecutionMode.Distributed);
        batch.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed_0"),
            new(batch.Id, 1, "seed_1")
        };
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        for (var i = 0; i < 5; i++)
            runs[1].MarkFailed("err", DateTimeOffset.UtcNow);
        await _runRepo.AddRangeAsync(runs);
        foreach (var r in runs)
            await _runRepo.UpdateAsync(r);

        var result = await _query.GetBatchIdsAsync();

        result.Should().ContainSingle().Which.Should().Be(batch.Id);
    }

    [Fact]
    public async Task GetBatchIdsAsync_BatchCompleted_ExcludedFromResults()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "seed", ExecutionMode.Distributed);
        batch.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed_0"),
            new(batch.Id, 1, "seed_1")
        };
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        runs[1].MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.AddRangeAsync(runs);
        foreach (var r in runs)
            await _runRepo.UpdateAsync(r);

        batch.SetCompleted(DateTimeOffset.UtcNow);
        await _batchRepo.UpdateAsync(batch);

        var result = await _query.GetBatchIdsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatchIdsAsync_BatchWithNonTerminalRun_ExcludedUntilAllTerminal()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "seed", ExecutionMode.Distributed);
        batch.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(batch);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed_0"),
            new(batch.Id, 1, "seed_1")
        };
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.AddRangeAsync(runs);
        await _runRepo.UpdateAsync(runs[0]);

        var result = await _query.GetBatchIdsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBatchIdsAsync_MultipleBatches_ReturnsOnlyUnfinalizedWithAllTerminalRuns()
    {
        var eventId = Guid.NewGuid();

        var batch1 = new SimulationBatch(eventId, 1, "a", ExecutionMode.Distributed);
        batch1.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(batch1);
        var run1 = new SimulationRun(batch1.Id, 0, "a_0");
        run1.MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.AddRangeAsync([run1]);
        await _runRepo.UpdateAsync(run1);

        var batch2 = new SimulationBatch(eventId, 1, "b", ExecutionMode.Distributed);
        batch2.SetStatus(BatchStatus.Running);
        await _batchRepo.AddAsync(batch2);
        var run2 = new SimulationRun(batch2.Id, 0, "b_0");
        await _runRepo.AddRangeAsync([run2]);

        var result = await _query.GetBatchIdsAsync();

        result.Should().ContainSingle().Which.Should().Be(batch1.Id);
    }
}
