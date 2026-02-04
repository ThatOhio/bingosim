using BingoSim.Application.DTOs;
using BingoSim.Application.Services;
using BingoSim.Application.StrategyKeys;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BingoSim.Application.UnitTests.Services;

public class TeamServiceTests
{
    private readonly ITeamRepository _teamRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IPlayerProfileRepository _playerProfileRepository;
    private readonly ILogger<TeamService> _logger;
    private readonly TeamService _service;

    public TeamServiceTests()
    {
        _teamRepository = Substitute.For<ITeamRepository>();
        _eventRepository = Substitute.For<IEventRepository>();
        _playerProfileRepository = Substitute.For<IPlayerProfileRepository>();
        _logger = Substitute.For<ILogger<TeamService>>();
        _service = new TeamService(_teamRepository, _eventRepository, _playerProfileRepository, _logger);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsTeamId()
    {
        var eventId = Guid.NewGuid();
        _eventRepository.ExistsAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);

        var request = new CreateTeamRequest(eventId, "Team Alpha", [], StrategyCatalog.RowUnlocking, null);
        var id = await _service.CreateAsync(request);

        id.Should().NotBe(Guid.Empty);
        await _teamRepository.Received(1).AddAsync(
            Arg.Is<Team>(t => t.Name == "Team Alpha" && t.EventId == eventId),
            Arg.Is<StrategyConfig>(s => s.StrategyKey == StrategyCatalog.RowUnlocking),
            Arg.Any<IEnumerable<TeamPlayer>>());
    }

    [Fact]
    public async Task CreateAsync_EventNotFound_ThrowsEventNotFoundException()
    {
        var eventId = Guid.NewGuid();
        _eventRepository.ExistsAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);

        var request = new CreateTeamRequest(eventId, "Team A", [], StrategyCatalog.RowUnlocking, null);
        var act = async () => await _service.CreateAsync(request);

        await act.Should().ThrowAsync<EventNotFoundException>();
        await _teamRepository.DidNotReceive().AddAsync(Arg.Any<Team>(), Arg.Any<StrategyConfig>(), Arg.Any<IEnumerable<TeamPlayer>>());
    }

    [Fact]
    public async Task CreateAsync_InvalidPlayerId_ThrowsPlayerProfileNotFoundException()
    {
        var eventId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        _eventRepository.ExistsAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);
        _playerProfileRepository.ExistsAsync(playerId, Arg.Any<CancellationToken>()).Returns(false);

        var request = new CreateTeamRequest(eventId, "Team A", [playerId], StrategyCatalog.RowUnlocking, null);
        var act = async () => await _service.CreateAsync(request);

        await act.Should().ThrowAsync<PlayerProfileNotFoundException>();
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsTeamsForEvent()
    {
        var eventId = Guid.NewGuid();
        var team = new Team(eventId, "Team A");
        _teamRepository.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>()).Returns([team]);
        _eventRepository.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(new Event("Evt", TimeSpan.FromHours(24), 5));

        var result = await _service.GetByEventIdAsync(eventId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Team A");
        result[0].EventId.Should().Be(eventId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsTeam()
    {
        var eventId = Guid.NewGuid();
        var team = new Team(eventId, "Team A");
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        _eventRepository.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(new Event("Evt", TimeSpan.FromHours(24), 5));

        var result = await _service.GetByIdAsync(team.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Team A");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _teamRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Team?)null);

        var result = await _service.GetByIdAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_Deletes()
    {
        var teamId = Guid.NewGuid();
        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(true);

        await _service.DeleteAsync(teamId);

        await _teamRepository.Received(1).DeleteAsync(teamId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ThrowsTeamNotFoundException()
    {
        var teamId = Guid.NewGuid();
        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _service.DeleteAsync(teamId);

        await act.Should().ThrowAsync<TeamNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingTeam_UpdatesAndPersists()
    {
        var eventId = Guid.NewGuid();
        var team = new Team(eventId, "Original");
        var strategy = new StrategyConfig(team.Id, StrategyCatalog.RowUnlocking, null);
        SetStrategyConfigOnTeam(team, strategy);
        _teamRepository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        var request = new UpdateTeamRequest("Updated Name", [], StrategyCatalog.RowUnlocking, "{}");
        await _service.UpdateAsync(team.Id, request);

        team.Name.Should().Be("Updated Name");
        strategy.StrategyKey.Should().Be(StrategyCatalog.RowUnlocking);
        await _teamRepository.Received(1).UpdateAsync(team, strategy, Arg.Any<IReadOnlyList<TeamPlayer>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_TeamNotFound_ThrowsTeamNotFoundException()
    {
        var id = Guid.NewGuid();
        _teamRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Team?)null);

        var request = new UpdateTeamRequest("Name", [], StrategyCatalog.RowUnlocking, null);
        var act = async () => await _service.UpdateAsync(id, request);

        await act.Should().ThrowAsync<TeamNotFoundException>();
    }

    private static void SetStrategyConfigOnTeam(Team team, StrategyConfig config)
    {
        var prop = typeof(Team).GetProperty("StrategyConfig");
        prop!.SetValue(team, config);
    }
}
