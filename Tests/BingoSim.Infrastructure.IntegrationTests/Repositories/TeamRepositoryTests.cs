using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using BingoSim.Infrastructure.IntegrationTests.Fixtures;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.IntegrationTests.Repositories;

[Collection("Postgres")]
public class TeamRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly PostgresFixture _postgres;
    private AppDbContext _context = null!;
    private TeamRepository _repository = null!;

    public TeamRepositoryTests(PostgresFixture postgres)
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

        _repository = new TeamRepository(_context);
    }

    public void Dispose() => _context?.Dispose();

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_WithMembershipsAndStrategy_RoundTripsAndRehydrates()
    {
        var (eventId, player1Id, player2Id) = await SeedEventAndPlayersAsync();

        var team = new Team(eventId, "Team Alpha");
        var strategy = new StrategyConfig(team.Id, "RowUnlocking", "{\"key\":\"value\"}");
        var teamPlayers = new List<TeamPlayer>
        {
            new(team.Id, player1Id),
            new(team.Id, player2Id)
        };

        await _repository.AddAsync(team, strategy, teamPlayers);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == team.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Team Alpha");
        retrieved.EventId.Should().Be(eventId);
        retrieved.StrategyConfig.Should().NotBeNull();
        retrieved.StrategyConfig!.StrategyKey.Should().Be("RowUnlocking");
        retrieved.StrategyConfig.ParamsJson.Should().Be("{\"key\":\"value\"}");
        retrieved.TeamPlayers.Should().HaveCount(2);
        retrieved.TeamPlayers.Select(tp => tp.PlayerProfileId).Should().Contain([player1Id, player2Id]);
    }

    [Fact]
    public async Task UpdateAsync_ModifyRosterAndStrategy_PersistsChanges()
    {
        var (eventId, player1Id, player2Id) = await SeedEventAndPlayersAsync();
        var team = new Team(eventId, "Original");
        var strategy = new StrategyConfig(team.Id, "RowUnlocking", null);
        var teamPlayers = new List<TeamPlayer> { new(team.Id, player1Id) };
        await _repository.AddAsync(team, strategy, teamPlayers);

        team.UpdateName("Updated Name");
        strategy.Update("RowUnlocking", "{\"x\":1}");
        var newRoster = new List<TeamPlayer>
        {
            new(team.Id, player1Id),
            new(team.Id, player2Id)
        };

        await _repository.UpdateAsync(team, strategy, newRoster);

        var freshContext = CreateFreshContext();
        var retrieved = await freshContext.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == team.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.StrategyConfig!.StrategyKey.Should().Be("RowUnlocking");
        retrieved.StrategyConfig.ParamsJson.Should().Be("{\"x\":1}");
        retrieved.TeamPlayers.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTeamAndMemberships()
    {
        var (eventId, player1Id, _) = await SeedEventAndPlayersAsync();
        var team = new Team(eventId, "To Delete");
        var strategy = new StrategyConfig(team.Id, "RowUnlocking", null);
        var teamPlayers = new List<TeamPlayer> { new(team.Id, player1Id) };
        await _repository.AddAsync(team, strategy, teamPlayers);

        await _repository.DeleteAsync(team.Id);

        var freshContext = CreateFreshContext();
        var retrievedTeam = await freshContext.Teams.FindAsync(team.Id);
        retrievedTeam.Should().BeNull();

        var remainingPlayers = await freshContext.TeamPlayers.Where(tp => tp.TeamId == team.Id).ToListAsync();
        remainingPlayers.Should().BeEmpty();

        var retrievedStrategy = await freshContext.StrategyConfigs.FirstOrDefaultAsync(s => s.TeamId == team.Id);
        retrievedStrategy.Should().BeNull();
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsOnlyTeamsForEvent()
    {
        var (eventId, player1Id, _) = await SeedEventAndPlayersAsync();
        var team1 = new Team(eventId, "Team One");
        var strategy1 = new StrategyConfig(team1.Id, "RowUnlocking", null);
        await _repository.AddAsync(team1, strategy1, []);

        var evt2 = new Event("Other Event", TimeSpan.FromHours(12), 5);
        evt2.SetRows([CreateMinimalRow(0)]);
        _context.Events.Add(evt2);
        await _context.SaveChangesAsync();

        var team2 = new Team(evt2.Id, "Team Two");
        var strategy2 = new StrategyConfig(team2.Id, "RowUnlocking", null);
        await _repository.AddAsync(team2, strategy2, []);

        var teams = await _repository.GetByEventIdAsync(eventId);

        teams.Should().HaveCount(1);
        teams[0].Name.Should().Be("Team One");
    }

    /// <summary>
    /// Acceptance: "I draft two teams, assign players" and persistence works across restarts (fresh context).
    /// </summary>
    [Fact]
    public async Task AddAsync_TwoTeamsForSameEvent_PersistAndRehydrateInFreshContext()
    {
        var (eventId, player1Id, player2Id) = await SeedEventAndPlayersAsync();

        var team1 = new Team(eventId, "Team Alpha");
        var strategy1 = new StrategyConfig(team1.Id, "RowUnlocking", "{\"baseline\":true}");
        var team1Players = new List<TeamPlayer> { new(team1.Id, player1Id), new(team1.Id, player2Id) };
        await _repository.AddAsync(team1, strategy1, team1Players);

        var team2 = new Team(eventId, "Team Beta");
        var strategy2 = new StrategyConfig(team2.Id, "RowUnlocking", "{\"alt\":1}");
        var team2Players = new List<TeamPlayer> { new(team2.Id, player1Id) };
        await _repository.AddAsync(team2, strategy2, team2Players);

        var freshContext = CreateFreshContext();
        var freshRepo = new TeamRepository(freshContext);
        var teams = await freshRepo.GetByEventIdAsync(eventId);
        await freshContext.DisposeAsync();

        teams.Should().HaveCount(2);
        var alpha = teams.FirstOrDefault(t => t.Name == "Team Alpha");
        var beta = teams.FirstOrDefault(t => t.Name == "Team Beta");
        alpha.Should().NotBeNull();
        beta.Should().NotBeNull();
        alpha!.StrategyConfig!.StrategyKey.Should().Be("RowUnlocking");
        alpha.StrategyConfig.ParamsJson.Should().Be("{\"baseline\":true}");
        alpha.TeamPlayers.Should().HaveCount(2);
        beta!.StrategyConfig!.StrategyKey.Should().Be("RowUnlocking");
        beta.StrategyConfig.ParamsJson.Should().Be("{\"alt\":1}");
        beta.TeamPlayers.Should().HaveCount(1);
    }

    private async Task<(Guid EventId, Guid Player1Id, Guid Player2Id)> SeedEventAndPlayersAsync()
    {
        var evt = new Event("Test Event", TimeSpan.FromHours(24), 5);
        evt.SetRows([CreateMinimalRow(0)]);
        _context.Events.Add(evt);

        var p1 = new PlayerProfile("Player One", 1.0m);
        var p2 = new PlayerProfile("Player Two", 0.9m);
        _context.PlayerProfiles.AddRange(p1, p2);

        await _context.SaveChangesAsync();

        return (evt.Id, p1.Id, p2.Id);
    }

    private static Row CreateMinimalRow(int index)
    {
        var activityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var rule = new TileActivityRule(activityId, "activity.key", [], [], []);
        return new Row(index, [
            new Tile($"r{index}.p1", "T1", 1, 1, [rule]),
            new Tile($"r{index}.p2", "T2", 2, 1, [rule]),
            new Tile($"r{index}.p3", "T3", 3, 1, [rule]),
            new Tile($"r{index}.p4", "T4", 4, 1, [rule])
        ]);
    }

    private AppDbContext CreateFreshContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_context.Database.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}
