using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

[Collection("Postgres")]
public class PlayerProfileRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private AppDbContext _context = null!;
    private PlayerProfileRepository _repository = null!;

    public PlayerProfileRepositoryTests(PostgresFixture postgres)
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

        _repository = new PlayerProfileRepository(_context);
    }

    public void Dispose() => _context?.Dispose();

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_ValidProfile_PersistsToDatabase()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer", 1.0m);

        // Act
        await _repository.AddAsync(profile);

        // Assert
        var retrieved = await _context.PlayerProfiles.FindAsync(profile.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task AddAsync_WithCapabilities_PersistsCapabilities()
    {
        // Arrange
        var profile = new PlayerProfile("PlayerWithCaps", 0.9m);
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));
        profile.AddCapability(new Capability("item.lance", "Dragon Hunter Lance"));

        // Act
        await _repository.AddAsync(profile);

        // Assert
        // Need fresh context to verify persistence
        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.PlayerProfiles
            .FirstOrDefaultAsync(p => p.Id == profile.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Capabilities.Should().HaveCount(2);
        retrieved.HasCapability("quest.ds2").Should().BeTrue();
        retrieved.HasCapability("item.lance").Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_WithSchedule_PersistsSchedule()
    {
        // Arrange
        var profile = new PlayerProfile("PlayerWithSchedule", 1.0m);
        profile.SetWeeklySchedule(new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
            new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
        ]));

        // Act
        await _repository.AddAsync(profile);

        // Assert
        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.PlayerProfiles
            .FirstOrDefaultAsync(p => p.Id == profile.Id);

        retrieved.Should().NotBeNull();
        retrieved!.WeeklySchedule.Sessions.Should().HaveCount(2);
        retrieved.WeeklySchedule.Sessions[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        retrieved.WeeklySchedule.Sessions[0].DurationMinutes.Should().Be(120);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsProfile()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer", 1.0m);
        await _repository.AddAsync(profile);

        // Act
        var retrieved = await _repository.GetByIdAsync(profile.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task GetByNameAsync_ExistingName_ReturnsProfile()
    {
        // Arrange
        var profile = new PlayerProfile("UniqueByName", 1.0m);
        await _repository.AddAsync(profile);

        // Act
        var retrieved = await _repository.GetByNameAsync("UniqueByName");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistingName_ReturnsNull()
    {
        var result = await _repository.GetByNameAsync("NonExistentName");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistingId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_MultipleProfiles_ReturnsAllOrderedByCreatedAt()
    {
        // Arrange
        var profile1 = new PlayerProfile("First", 1.0m);
        await _repository.AddAsync(profile1);

        await Task.Delay(10); // Ensure different timestamps

        var profile2 = new PlayerProfile("Second", 1.0m);
        await _repository.AddAsync(profile2);

        // Act
        var profiles = await _repository.GetAllAsync();

        // Assert
        profiles.Should().HaveCount(2);
        profiles[0].Name.Should().Be("Second"); // Most recent first
        profiles[1].Name.Should().Be("First");
    }

    [Fact]
    public async Task UpdateAsync_ExistingProfile_PersistsChanges()
    {
        // Arrange
        var profile = new PlayerProfile("OldName", 1.0m);
        await _repository.AddAsync(profile);

        // Act
        profile.UpdateName("NewName");
        profile.UpdateSkillTimeMultiplier(0.8m);
        await _repository.UpdateAsync(profile);

        // Assert
        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.PlayerProfiles.FindAsync(profile.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("NewName");
        retrieved.SkillTimeMultiplier.Should().Be(0.8m);
    }

    [Fact]
    public async Task UpdateAsync_ModifyCapabilities_PersistsChanges()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer", 1.0m);
        profile.AddCapability(new Capability("old.cap", "Old Capability"));
        await _repository.AddAsync(profile);

        // Act
        profile.ClearCapabilities();
        profile.AddCapability(new Capability("new.cap", "New Capability"));
        await _repository.UpdateAsync(profile);

        // Assert
        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.PlayerProfiles.FindAsync(profile.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Capabilities.Should().HaveCount(1);
        retrieved.HasCapability("new.cap").Should().BeTrue();
        retrieved.HasCapability("old.cap").Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ExistingProfile_RemovesFromDatabase()
    {
        // Arrange
        var profile = new PlayerProfile("ToDelete", 1.0m);
        await _repository.AddAsync(profile);

        // Act
        await _repository.DeleteAsync(profile.Id);

        // Assert
        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.PlayerProfiles.FindAsync(profile.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_DoesNothing()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var act = async () => await _repository.DeleteAsync(nonExistingId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer", 1.0m);
        await _repository.AddAsync(profile);

        // Act
        var exists = await _repository.ExistsAsync(profile.Id);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingId_ReturnsFalse()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var exists = await _repository.ExistsAsync(nonExistingId);

        // Assert
        exists.Should().BeFalse();
    }

    private AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_context.Database.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}
