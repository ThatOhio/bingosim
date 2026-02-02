using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

public class EventRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private EventRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _repository = new EventRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ValidEvent_PersistsToDatabase()
    {
        var evt = CreateMinimalEvent();

        await _repository.AddAsync(evt);

        var retrieved = await _context.Events.FindAsync(evt.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Event");
        retrieved.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddAsync_WithMultipleRowsAndTilesAndRules_RoundTripsNestedJson()
    {
        var evt = CreateEventWithMultipleRowsAndRules();

        await _repository.AddAsync(evt);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.Events
            .FirstOrDefaultAsync(e => e.Id == evt.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Multi Row Event");
        retrieved.Rows.Should().HaveCount(2);
        retrieved.Rows[0].Index.Should().Be(0);
        retrieved.Rows[0].Tiles.Should().HaveCount(4);
        retrieved.Rows[0].Tiles[0].Key.Should().Be("r0.p1");
        retrieved.Rows[0].Tiles[0].Points.Should().Be(1);
        retrieved.Rows[0].Tiles[0].AllowedActivities.Should().NotBeNull();
        if (retrieved.Rows[0].Tiles[0].AllowedActivities.Count > 0)
        {
            retrieved.Rows[0].Tiles[0].AllowedActivities[0].ActivityDefinitionId.Should().NotBe(Guid.Empty);
            retrieved.Rows[0].Tiles[0].AllowedActivities[0].ActivityKey.Should().Be("activity.zulrah");
        }
        retrieved.Rows[1].Index.Should().Be(1);
        retrieved.Rows[1].Tiles.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEventWithNestedData()
    {
        var evt = CreateMinimalEvent();
        await _repository.AddAsync(evt);

        var result = await _repository.GetByIdAsync(evt.Id);

        result.Should().NotBeNull();
        result!.Rows.Should().HaveCount(1);
        result.Rows[0].Tiles.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetByNameAsync_ExistingName_ReturnsEvent()
    {
        var evt = CreateMinimalEvent("UniqueEventName");
        await _repository.AddAsync(evt);

        var result = await _repository.GetByNameAsync("UniqueEventName");

        result.Should().NotBeNull();
        result!.Id.Should().Be(evt.Id);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistingName_ReturnsNull()
    {
        var result = await _repository.GetByNameAsync("NonExistentEvent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_MultipleEvents_ReturnsAllOrderedByCreatedAt()
    {
        var evt1 = CreateMinimalEvent("First");
        await _repository.AddAsync(evt1);
        await Task.Delay(10);
        var evt2 = CreateMinimalEvent("Second");
        await _repository.AddAsync(evt2);

        var list = await _repository.GetAllAsync();

        list.Should().HaveCount(2);
        list[0].Name.Should().Be("Second");
        list[1].Name.Should().Be("First");
    }

    [Fact]
    public async Task UpdateAsync_ModifyNameAndRows_PersistsChanges()
    {
        var evt = CreateMinimalEvent();
        await _repository.AddAsync(evt);

        var freshEntity = await _repository.GetByIdAsync(evt.Id);
        freshEntity.Should().NotBeNull();
        freshEntity!.UpdateName("Updated Name");
        var activityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var rule = new TileActivityRule(activityId, "activity.updated", ["drop.new"], [], []);
        var newRow = new Row(1, [
            new Tile("r1.p1", "N1", 1, 2, [rule]),
            new Tile("r1.p2", "N2", 2, 2, [rule]),
            new Tile("r1.p3", "N3", 3, 2, [rule]),
            new Tile("r1.p4", "N4", 4, 2, [rule])
        ]);
        freshEntity.SetRows(freshEntity.Rows.Concat([newRow]));
        await _repository.UpdateAsync(freshEntity);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.Events.FindAsync(evt.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesFromDatabase()
    {
        var evt = CreateMinimalEvent();
        await _repository.AddAsync(evt);

        await _repository.DeleteAsync(evt.Id);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.Events.FindAsync(evt.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingId_ReturnsTrue()
    {
        var evt = CreateMinimalEvent();
        await _repository.AddAsync(evt);

        var exists = await _repository.ExistsAsync(evt.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingId_ReturnsFalse()
    {
        var exists = await _repository.ExistsAsync(Guid.NewGuid());
        exists.Should().BeFalse();
    }

    private static Event CreateMinimalEvent(string name = "Test Event")
    {
        var evt = new Event(name, TimeSpan.FromHours(24), 5);
        var activityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var rule = new TileActivityRule(activityId, "activity.key", [], [], []);
        var row = new Row(0, [
            new Tile("r0.p1", "Tile 1", 1, 1, [rule]),
            new Tile("r0.p2", "Tile 2", 2, 1, [rule]),
            new Tile("r0.p3", "Tile 3", 3, 1, [rule]),
            new Tile("r0.p4", "Tile 4", 4, 1, [rule])
        ]);
        evt.SetRows([row]);
        return evt;
    }

    private static Event CreateEventWithMultipleRowsAndRules()
    {
        var evt = new Event("Multi Row Event", TimeSpan.FromHours(48), 5);
        var activityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var rule = new TileActivityRule(
            activityId,
            "activity.zulrah",
            ["drop.magic_fang"],
            [new Capability("quest.ds2", "Dragon Slayer 2")],
            []);
        var row0 = new Row(0, [
            new Tile("r0.p1", "T1", 1, 1, [rule]),
            new Tile("r0.p2", "T2", 2, 1, [rule]),
            new Tile("r0.p3", "T3", 3, 1, [rule]),
            new Tile("r0.p4", "T4", 4, 1, [rule])
        ]);
        var row1 = new Row(1, [
            new Tile("r1.p1", "T1", 1, 1, [rule]),
            new Tile("r1.p2", "T2", 2, 1, [rule]),
            new Tile("r1.p3", "T3", 3, 1, [rule]),
            new Tile("r1.p4", "T4", 4, 1, [rule])
        ]);
        evt.SetRows([row0, row1]);
        return evt;
    }

    private AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}
