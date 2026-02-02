using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

public class SimulationRunRepositoryTryClaimTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private SimulationRunRepository _runRepo = null!;
    private SimulationBatchRepository _batchRepo = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _batchRepo = new SimulationBatchRepository(_context);
        _runRepo = new SimulationRunRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task TryClaimAsync_PendingRun_ReturnsTrue()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 1, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run = new SimulationRun(batch.Id, 0, "seed_0");
        await _runRepo.AddRangeAsync([run]);

        var claimed = await _runRepo.TryClaimAsync(run.Id, DateTimeOffset.UtcNow);

        claimed.Should().BeTrue();
        var loaded = await _runRepo.GetByIdAsync(run.Id);
        loaded!.Status.Should().Be(RunStatus.Running);
    }

    [Fact]
    public async Task TryClaimAsync_AlreadyRunning_ReturnsFalse()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 1, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run = new SimulationRun(batch.Id, 0, "seed_0");
        await _runRepo.AddRangeAsync([run]);

        var first = await _runRepo.TryClaimAsync(run.Id, DateTimeOffset.UtcNow);
        first.Should().BeTrue();

        var second = await _runRepo.TryClaimAsync(run.Id, DateTimeOffset.UtcNow);
        second.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimAsync_AlreadyCompleted_ReturnsFalse()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 1, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run = new SimulationRun(batch.Id, 0, "seed_0");
        await _runRepo.AddRangeAsync([run]);
        run.MarkRunning(DateTimeOffset.UtcNow);
        run.MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.UpdateAsync(run);

        var claimed = await _runRepo.TryClaimAsync(run.Id, DateTimeOffset.UtcNow);

        claimed.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimAsync_AlreadyFailed_ReturnsFalse()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 1, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run = new SimulationRun(batch.Id, 0, "seed_0");
        await _runRepo.AddRangeAsync([run]);
        for (var i = 0; i < 5; i++)
            run.MarkFailed("err", DateTimeOffset.UtcNow);
        await _runRepo.UpdateAsync(run);

        var claimed = await _runRepo.TryClaimAsync(run.Id, DateTimeOffset.UtcNow);

        claimed.Should().BeFalse();
    }
}
