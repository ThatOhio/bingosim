using BingoSim.Application.Interfaces;
using BingoSim.Application.Services;
using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class DevSeedServiceTests
{
    [Fact]
    public async Task SeedAsync_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        playerRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PlayerProfile?)null);
        activityRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);
        eventRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Event?)null);

        var teamRepo = Substitute.For<ITeamRepository>();
        teamRepo.GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var logger = Substitute.For<ILogger<DevSeedService>>();
        var sut = new DevSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        // Act
        var act = async () => await sut.SeedAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetAndSeedAsync_WhenCalled_DoesNotThrow()
    {
        // Arrange - no existing seed data
        var playerRepo = Substitute.For<IPlayerProfileRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var eventRepo = Substitute.For<IEventRepository>();
        playerRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PlayerProfile?)null);
        activityRepo.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ActivityDefinition?)null);
        eventRepo.GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Event?)null);

        var teamRepo = Substitute.For<ITeamRepository>();
        teamRepo.GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        var logger = Substitute.For<ILogger<DevSeedService>>();
        var sut = new DevSeedService(playerRepo, activityRepo, eventRepo, teamRepo, logger);

        // Act
        var act = async () => await sut.ResetAndSeedAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
