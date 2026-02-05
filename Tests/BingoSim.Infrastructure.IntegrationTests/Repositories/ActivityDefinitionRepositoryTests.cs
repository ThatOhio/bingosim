using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

[Collection("Postgres")]
public class ActivityDefinitionRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private AppDbContext _context = null!;
    private ActivityDefinitionRepository _repository = null!;

    public ActivityDefinitionRepositoryTests(PostgresFixture postgres)
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

        _repository = new ActivityDefinitionRepository(_context);
    }

    public void Dispose() => _context?.Dispose();

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidEntity_PersistsToDatabase()
    {
        var entity = CreateMinimalEntity("activity.test", "Test Activity");

        await _repository.AddAsync(entity);

        var retrieved = await _context.ActivityDefinitions.FindAsync(entity.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("activity.test");
        retrieved.Name.Should().Be("Test Activity");
    }

    [Fact]
    public async Task AddAsync_WithAttemptsAndOutcomesAndGrants_PersistsNestedJson()
    {
        var entity = CreateEntityWithFullAttempts("activity.zulrah", "Zulrah");

        await _repository.AddAsync(entity);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.ActivityDefinitions
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Attempts.Should().HaveCount(2);
        retrieved.Attempts[0].Key.Should().Be("personal_loot");
        retrieved.Attempts[0].RollScope.Should().Be(RollScope.PerPlayer);
        retrieved.Attempts[0].TimeModel.BaselineTimeSeconds.Should().Be(60);
        retrieved.Attempts[0].TimeModel.VarianceSeconds.Should().Be(10);
        retrieved.Attempts[0].Outcomes.Should().HaveCount(2);
        retrieved.Attempts[0].Outcomes[0].Key.Should().Be("common");
        retrieved.Attempts[0].Outcomes[0].Grants.Should().HaveCount(1);
        retrieved.Attempts[0].Outcomes[0].Grants[0].DropKey.Should().Be("drop.common");
        retrieved.Attempts[0].Outcomes[0].Grants[0].Units.Should().Be(1);
        retrieved.Attempts[0].Outcomes[1].Grants[0].Units.Should().Be(3);
        retrieved.Attempts[1].RollScope.Should().Be(RollScope.PerGroup);
    }

    [Fact]
    public async Task AddAsync_WithGroupScalingBands_PersistsBands()
    {
        var entity = CreateMinimalEntity("activity.band", "With Bands");
        entity.SetGroupScalingBands([
            new GroupSizeBand(1, 1, 1.0m, 1.0m),
            new GroupSizeBand(2, 4, 0.9m, 1.1m),
            new GroupSizeBand(5, 8, 0.85m, 1.15m)
        ]);

        await _repository.AddAsync(entity);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.ActivityDefinitions
            .FirstOrDefaultAsync(e => e.Id == entity.Id);

        retrieved.Should().NotBeNull();
        retrieved!.GroupScalingBands.Should().HaveCount(3);
        retrieved.GroupScalingBands[0].MinSize.Should().Be(1);
        retrieved.GroupScalingBands[0].MaxSize.Should().Be(1);
        // Band range 2–4 (acceptance: "e.g., 1, 2–4, 5–8")
        retrieved.GroupScalingBands[1].MinSize.Should().Be(2);
        retrieved.GroupScalingBands[1].MaxSize.Should().Be(4);
        retrieved.GroupScalingBands[1].TimeMultiplier.Should().Be(0.9m);
        retrieved.GroupScalingBands[2].MinSize.Should().Be(5);
        retrieved.GroupScalingBands[2].MaxSize.Should().Be(8);
        retrieved.GroupScalingBands[2].ProbabilityMultiplier.Should().Be(1.15m);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEntityWithNestedData()
    {
        var entity = CreateEntityWithFullAttempts("activity.get", "Get Test");
        await _repository.AddAsync(entity);

        var result = await _repository.GetByIdAsync(entity.Id);

        result.Should().NotBeNull();
        result!.Key.Should().Be("activity.get");
        result.ModeSupport.SupportsSolo.Should().BeTrue();
        result.ModeSupport.MinGroupSize.Should().Be(2);
        result.Attempts.Should().HaveCount(2);
        result.Attempts[0].Outcomes[0].Grants.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByKeyAsync_ExistingKey_ReturnsEntity()
    {
        var entity = CreateMinimalEntity("activity.bykey", "By Key");
        await _repository.AddAsync(entity);

        var result = await _repository.GetByKeyAsync("activity.bykey");

        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
        result.Name.Should().Be("By Key");
    }

    [Fact]
    public async Task GetByKeyAsync_NonExistingKey_ReturnsNull()
    {
        var result = await _repository.GetByKeyAsync("nonexistent.key");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_MultipleEntities_ReturnsAllOrderedByCreatedAt()
    {
        var e1 = CreateMinimalEntity("activity.first", "First");
        await _repository.AddAsync(e1);
        await Task.Delay(10);
        var e2 = CreateMinimalEntity("activity.second", "Second");
        await _repository.AddAsync(e2);

        var list = await _repository.GetAllAsync();

        list.Should().HaveCount(2);
        list[0].Key.Should().Be("activity.second");
        list[1].Key.Should().Be("activity.first");
    }

    [Fact]
    public async Task UpdateAsync_ModifyAttempts_PersistsChanges()
    {
        var entity = CreateEntityWithFullAttempts("activity.update", "Update Test");
        await _repository.AddAsync(entity);

        var freshEntity = await _repository.GetByIdAsync(entity.Id);
        freshEntity.Should().NotBeNull();
        freshEntity!.SetAttempts([
            new ActivityAttemptDefinition(
                "single_attempt",
                RollScope.PerGroup,
                new AttemptTimeModel(120, TimeDistribution.NormalApprox, 20),
                [new ActivityOutcomeDefinition("only_outcome", 1, 1, [new ProgressGrant("drop.only", 2)])])
        ]);
        await _repository.UpdateAsync(freshEntity);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.ActivityDefinitions.FindAsync(entity.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Attempts.Should().HaveCount(1);
        retrieved.Attempts[0].Key.Should().Be("single_attempt");
        retrieved.Attempts[0].Outcomes[0].Grants[0].Units.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesFromDatabase()
    {
        var entity = CreateMinimalEntity("activity.delete", "To Delete");
        await _repository.AddAsync(entity);

        await _repository.DeleteAsync(entity.Id);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.ActivityDefinitions.FindAsync(entity.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingId_ReturnsTrue()
    {
        var entity = CreateMinimalEntity("activity.exists", "Exists");
        await _repository.AddAsync(entity);

        var exists = await _repository.ExistsAsync(entity.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingId_ReturnsFalse()
    {
        var exists = await _repository.ExistsAsync(Guid.NewGuid());
        exists.Should().BeFalse();
    }

    private static ActivityDefinition CreateMinimalEntity(string key, string name)
    {
        var modeSupport = new ActivityModeSupport(true, true, null, null);
        var entity = new ActivityDefinition(key, name, modeSupport);
        var attempt = new ActivityAttemptDefinition(
            "attempt_1",
            RollScope.PerPlayer,
            new AttemptTimeModel(60, TimeDistribution.Uniform),
            [new ActivityOutcomeDefinition("outcome_1", 1, 1, [new ProgressGrant("drop.key", 1)])]);
        entity.SetAttempts([attempt]);
        return entity;
    }

    private static ActivityDefinition CreateEntityWithFullAttempts(string key, string name)
    {
        var modeSupport = new ActivityModeSupport(true, true, 2, 8);
        var entity = new ActivityDefinition(key, name, modeSupport);
        var attempt1 = new ActivityAttemptDefinition(
            "personal_loot",
            RollScope.PerPlayer,
            new AttemptTimeModel(60, TimeDistribution.Uniform, 10),
            [
                new ActivityOutcomeDefinition("common", 1, 2, [new ProgressGrant("drop.common", 1)]),
                new ActivityOutcomeDefinition("rare", 1, 100, [new ProgressGrant("drop.rare", 3)])
            ]);
        var attempt2 = new ActivityAttemptDefinition(
            "group_loot",
            RollScope.PerGroup,
            new AttemptTimeModel(90, TimeDistribution.NormalApprox),
            [new ActivityOutcomeDefinition("team_rare", 1, 50, [new ProgressGrant("drop.team", 1)])]);
        entity.SetAttempts([attempt1, attempt2]);
        return entity;
    }

    private AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_context.Database.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}
