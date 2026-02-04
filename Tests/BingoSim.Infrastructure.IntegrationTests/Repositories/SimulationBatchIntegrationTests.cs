using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

public class SimulationBatchIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private SimulationBatchRepository _batchRepo = null!;
    private EventSnapshotRepository _snapshotRepo = null!;
    private SimulationRunRepository _runRepo = null!;
    private TeamRunResultRepository _resultRepo = null!;
    private BatchTeamAggregateRepository _aggregateRepo = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _batchRepo = new SimulationBatchRepository(_context);
        _snapshotRepo = new EventSnapshotRepository(_context);
        _runRepo = new SimulationRunRepository(_context);
        _resultRepo = new TeamRunResultRepository(_context);
        _aggregateRepo = new BatchTeamAggregateRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateBatch_WithSnapshot_AndRuns_AndResults_AndAggregates_PersistsCorrectly()
    {
        var eventId = Guid.NewGuid();
        var batch = new SimulationBatch(eventId, 2, "seed-123", ExecutionMode.Local);
        await _batchRepo.AddAsync(batch);

        var snapshot = new EventSnapshot(batch.Id, "{\"EventName\":\"Test\",\"DurationSeconds\":3600,\"UnlockPointsRequiredPerRow\":5,\"Rows\":[],\"ActivitiesById\":{},\"Teams\":[]}");
        await _snapshotRepo.AddAsync(snapshot);

        var runs = new List<SimulationRun>
        {
            new(batch.Id, 0, "seed-123_0"),
            new(batch.Id, 1, "seed-123_1")
        };
        await _runRepo.AddRangeAsync(runs);

        runs[0].MarkRunning(DateTimeOffset.UtcNow);
        runs[0].MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.UpdateAsync(runs[0]);

        var teamId = Guid.NewGuid();
        var results = new List<TeamRunResult>
        {
            new(runs[0].Id, teamId, "Team A", "RowUnlocking", null, 10, 5, 2, true, "{\"0\":0,\"1\":100}", "{\"t1\":50}")
        };
        await _resultRepo.AddRangeAsync(results);

        var aggregates = new List<BatchTeamAggregate>
        {
            new(batch.Id, teamId, "Team A", "RowUnlocking", 10, 10, 10, 5, 5, 5, 2, 2, 2, 1.0, 1)
        };
        await _aggregateRepo.AddRangeAsync(aggregates);

        batch.SetCompleted(DateTimeOffset.UtcNow);
        await _batchRepo.UpdateAsync(batch);

        var freshContext = CreateFreshContext();
        var retrievedBatch = await freshContext.SimulationBatches.FindAsync(batch.Id);
        retrievedBatch.Should().NotBeNull();
        retrievedBatch!.Status.Should().Be(BatchStatus.Completed);
        retrievedBatch.Seed.Should().Be("seed-123");

        var retrievedRuns = await _runRepo.GetByBatchIdAsync(batch.Id);
        retrievedRuns.Should().HaveCount(2);

        var retrievedResults = await _resultRepo.GetByBatchIdAsync(batch.Id);
        retrievedResults.Should().HaveCount(1);
        retrievedResults[0].TotalPoints.Should().Be(10);
        retrievedResults[0].IsWinner.Should().BeTrue();

        var retrievedAggregates = await _aggregateRepo.GetByBatchIdAsync(batch.Id);
        retrievedAggregates.Should().HaveCount(1);
        retrievedAggregates[0].MeanPoints.Should().Be(10);
        retrievedAggregates[0].WinnerRate.Should().Be(1.0);
    }

    private AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}
