using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

[Collection("Postgres")]
public class SimulationRunRepositoryTryClaimTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private AppDbContext _context = null!;
    private SimulationRunRepository _runRepo = null!;
    private SimulationBatchRepository _batchRepo = null!;

    public SimulationRunRepositoryTryClaimTests(PostgresFixture postgres)
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
        _runRepo = new SimulationRunRepository(_context);
    }

    public void Dispose() => _context?.Dispose();

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
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

    [Fact]
    public async Task ClaimBatchAsync_PendingRuns_ReturnsAllClaimedIds()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run0 = new SimulationRun(batch.Id, 0, "seed_0");
        var run1 = new SimulationRun(batch.Id, 1, "seed_1");
        var run2 = new SimulationRun(batch.Id, 2, "seed_2");
        await _runRepo.AddRangeAsync([run0, run1, run2]);

        var runIds = new List<Guid> { run0.Id, run1.Id, run2.Id };
        var claimed = await _runRepo.ClaimBatchAsync(runIds, DateTimeOffset.UtcNow);

        claimed.Should().HaveCount(3);
        claimed.Should().Contain(run0.Id);
        claimed.Should().Contain(run1.Id);
        claimed.Should().Contain(run2.Id);
        foreach (var id in claimed)
        {
            var loaded = await _runRepo.GetByIdAsync(id);
            loaded!.Status.Should().Be(RunStatus.Running);
        }
    }

    [Fact]
    public async Task ClaimBatchAsync_SomeAlreadyClaimed_ReturnsOnlyPending()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 3, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run0 = new SimulationRun(batch.Id, 0, "seed_0");
        var run1 = new SimulationRun(batch.Id, 1, "seed_1");
        var run2 = new SimulationRun(batch.Id, 2, "seed_2");
        await _runRepo.AddRangeAsync([run0, run1, run2]);

        await _runRepo.TryClaimAsync(run1.Id, DateTimeOffset.UtcNow);

        var runIds = new List<Guid> { run0.Id, run1.Id, run2.Id };
        var claimed = await _runRepo.ClaimBatchAsync(runIds, DateTimeOffset.UtcNow);

        claimed.Should().HaveCount(2);
        claimed.Should().Contain(run0.Id);
        claimed.Should().Contain(run2.Id);
        claimed.Should().NotContain(run1.Id);
    }

    [Fact]
    public async Task ClaimBatchAsync_EmptyList_ReturnsEmpty()
    {
        var claimed = await _runRepo.ClaimBatchAsync([], DateTimeOffset.UtcNow);
        claimed.Should().BeEmpty();
    }

    [Fact]
    public async Task HasNonTerminalRunsAsync_WithPendingRun_ReturnsTrue()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 2, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run0 = new SimulationRun(batch.Id, 0, "seed_0");
        var run1 = new SimulationRun(batch.Id, 1, "seed_1");
        await _runRepo.AddRangeAsync([run0, run1]);

        var hasNonTerminal = await _runRepo.HasNonTerminalRunsAsync(batch.Id);

        hasNonTerminal.Should().BeTrue();
    }

    [Fact]
    public async Task HasNonTerminalRunsAsync_AllCompleted_ReturnsFalse()
    {
        var batch = new SimulationBatch(Guid.NewGuid(), 1, "seed", ExecutionMode.Distributed);
        await _batchRepo.AddAsync(batch);
        var run = new SimulationRun(batch.Id, 0, "seed_0");
        await _runRepo.AddRangeAsync([run]);
        run.MarkRunning(DateTimeOffset.UtcNow);
        run.MarkCompleted(DateTimeOffset.UtcNow);
        await _runRepo.UpdateAsync(run);

        var hasNonTerminal = await _runRepo.HasNonTerminalRunsAsync(batch.Id);

        hasNonTerminal.Should().BeFalse();
    }
}
