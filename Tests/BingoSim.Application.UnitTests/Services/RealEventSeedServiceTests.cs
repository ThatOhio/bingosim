using BingoSim.Application.Interfaces;
using BingoSim.Application.Services;
using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class RealEventSeedServiceTests
{
    [Fact]
    public async Task SeedEventAsync_Bingo7_SeedsPlayersAndTeams()
    {
        // Arrange
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();

        playerRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PlayerProfile?)null);
        activityRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);
        eventRepo.GetByNameAsync("Bingo7", Arg.Any<CancellationToken>()).Returns(new Event("Bingo7", TimeSpan.FromHours(24), 5));
        teamRepo.GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var logger = Substitute.For<ILogger<RealEventSeedService>>();
        var sut = new RealEventSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        // Act
        await sut.SeedEventAsync("Bingo7");

        // Assert
        await playerRepo.Received(60).AddAsync(Arg.Any<PlayerProfile>(), Arg.Any<CancellationToken>());
        await teamRepo.Received(3).AddAsync(Arg.Any<Team>(), Arg.Any<StrategyConfig>(), Arg.Any<IEnumerable<TeamPlayer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedEventAsync_Bingo7_EventNotFound_SkipsPlayersAndTeams()
    {
        // Arrange - GetByNameAsync always returns null (event never found, e.g. DB not synced)
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();

        activityRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);
        eventRepo.GetByNameAsync("Bingo7", Arg.Any<CancellationToken>()).Returns((Event?)null);

        var logger = Substitute.For<ILogger<RealEventSeedService>>();
        var sut = new RealEventSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        // Act - SeedBingo7EventAsync creates event; SeedBingo7PlayersAndTeamsAsync gets null, skips
        await sut.SeedEventAsync("Bingo7");

        // Assert - no players or teams added (players/teams seed skips when event not found)
        await playerRepo.DidNotReceive().AddAsync(Arg.Any<PlayerProfile>(), Arg.Any<CancellationToken>());
        await playerRepo.DidNotReceive().UpdateAsync(Arg.Any<PlayerProfile>(), Arg.Any<CancellationToken>());
        await teamRepo.DidNotReceive().AddAsync(Arg.Any<Team>(), Arg.Any<StrategyConfig>(), Arg.Any<IEnumerable<TeamPlayer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedEventAsync_Bingo7_Idempotent_UpdatesExistingPlayersAndTeams()
    {
        // Arrange - existing players and teams
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();

        var evt = new Event("Bingo7", TimeSpan.FromHours(24), 5);
        eventRepo.GetByNameAsync("Bingo7", Arg.Any<CancellationToken>()).Returns(evt);
        activityRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);

        var existingPlayers = Enumerable.Range(1, 60)
            .Select(i => new PlayerProfile($"Bingo7-Player-{i}"))
            .ToList();
        playerRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => existingPlayers.FirstOrDefault(p => p.Name == call.Arg<string>()));

        var existingTeam = new Team(evt.Id, "Team Alpha");
        teamRepo.GetByEventIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns([
            existingTeam,
            new Team(evt.Id, "Team Beta"),
            new Team(evt.Id, "Team Gamma"),
        ]);

        var logger = Substitute.For<ILogger<RealEventSeedService>>();
        var sut = new RealEventSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        // Act
        await sut.SeedEventAsync("Bingo7");

        // Assert - updates existing players, updates existing teams
        await playerRepo.Received(60).UpdateAsync(Arg.Any<PlayerProfile>(), Arg.Any<CancellationToken>());
        await teamRepo.Received(3).UpdateAsync(Arg.Any<Team>(), Arg.Any<StrategyConfig>(), Arg.Any<IReadOnlyList<TeamPlayer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedEventAsync_UnknownEvent_ThrowsArgumentException()
    {
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();
        var logger = Substitute.For<ILogger<RealEventSeedService>>();
        var sut = new RealEventSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        var act = async () => await sut.SeedEventAsync("UnknownEvent");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown real event*UnknownEvent*");
    }
}
