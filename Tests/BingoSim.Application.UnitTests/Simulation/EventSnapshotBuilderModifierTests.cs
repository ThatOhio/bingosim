using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using BingoSim.Core.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class EventSnapshotBuilderModifierTests
{
    [Fact]
    public async Task BuildSnapshotJsonAsync_EventWithModifiers_IncludesModifiersInJson()
    {
        var actId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var activity = CreateActivity(actId);
        var evt = CreateEventWithModifiers(eventId, actId);
        var team = CreateTeamWithPlayer(eventId, teamId, playerId);
        var player = CreatePlayer(playerId);

        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var playerRepo = Substitute.For<IPlayerProfileRepository>();

        eventRepo.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(evt);
        teamRepo.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>()).Returns([team]);
        activityRepo.GetByIdAsync(actId, Arg.Any<CancellationToken>()).Returns(activity);
        playerRepo.GetByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(player);

        var builder = new EventSnapshotBuilder(eventRepo, teamRepo, activityRepo, playerRepo);
        var json = await builder.BuildSnapshotJsonAsync(eventId);

        var dto = EventSnapshotBuilder.Deserialize(json);
        dto.Should().NotBeNull();
        dto!.Rows.Should().NotBeEmpty();
        var firstRule = dto.Rows[0].Tiles[0].AllowedActivities[0];
        firstRule.Modifiers.Should().NotBeEmpty();
        firstRule.Modifiers.Should().Contain(m => m.CapabilityKey == "quest.ds2" && m.TimeMultiplier == 0.9m && m.ProbabilityMultiplier == 1.1m);
    }

    [Fact]
    public async Task BuildSnapshotJsonAsync_EventWithoutModifiers_SerializesEmptyList()
    {
        var actId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var activity = CreateActivity(actId);
        var evt = CreateEventWithoutModifiers(eventId, actId);
        var team = CreateTeamWithPlayer(eventId, teamId, playerId);
        var player = CreatePlayer(playerId);

        var eventRepo = Substitute.For<IEventRepository>();
        var teamRepo = Substitute.For<ITeamRepository>();
        var activityRepo = Substitute.For<IActivityDefinitionRepository>();
        var playerRepo = Substitute.For<IPlayerProfileRepository>();

        eventRepo.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(evt);
        teamRepo.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>()).Returns([team]);
        activityRepo.GetByIdAsync(actId, Arg.Any<CancellationToken>()).Returns(activity);
        playerRepo.GetByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(player);

        var builder = new EventSnapshotBuilder(eventRepo, teamRepo, activityRepo, playerRepo);
        var json = await builder.BuildSnapshotJsonAsync(eventId);

        var dto = EventSnapshotBuilder.Deserialize(json);
        dto.Should().NotBeNull();
        var firstRule = dto!.Rows[0].Tiles[0].AllowedActivities[0];
        firstRule.Modifiers.Should().BeEmpty();
    }

    private static ActivityDefinition CreateActivity(Guid id)
    {
        var activity = new ActivityDefinition("act", "Activity", new ActivityModeSupport(true, false, null, null));
        SetPrivateId(activity, id);
        activity.SetAttempts([
            new ActivityAttemptDefinition(
                "main",
                RollScope.PerPlayer,
                new AttemptTimeModel(60, TimeDistribution.Uniform, 10),
                [new ActivityOutcomeDefinition("out", 1, 1, [new ProgressGrant("drop", 1)])])
        ]);
        return activity;
    }

    private static Event CreateEventWithModifiers(Guid eventId, Guid actId)
    {
        var evt = new Event("Test", TimeSpan.FromHours(1));
        SetPrivateId(evt, eventId);
        var cap = new Capability("quest.ds2", "Dragon Slayer II");
        var rule = new TileActivityRule(actId, "act", ["drop"], [], [new ActivityModifierRule(cap, 0.9m, 1.1m)]);
        var tile = new Tile("t1", "T1", 1, 1, [rule]);
        var row = new Row(0, [tile, new Tile("t2", "T2", 2, 1, [rule]), new Tile("t3", "T3", 3, 1, [rule]), new Tile("t4", "T4", 4, 1, [rule])]);
        evt.SetRows([row]);
        return evt;
    }

    private static Event CreateEventWithoutModifiers(Guid eventId, Guid actId)
    {
        var evt = new Event("Test", TimeSpan.FromHours(1));
        SetPrivateId(evt, eventId);
        var rule = new TileActivityRule(actId, "act", ["drop"], [], []);
        var tile = new Tile("t1", "T1", 1, 1, [rule]);
        var row = new Row(0, [tile, new Tile("t2", "T2", 2, 1, [rule]), new Tile("t3", "T3", 3, 1, [rule]), new Tile("t4", "T4", 4, 1, [rule])]);
        evt.SetRows([row]);
        return evt;
    }

    private static Team CreateTeamWithPlayer(Guid eventId, Guid teamId, Guid playerId)
    {
        var team = new Team(eventId, "Team A");
        SetPrivateId(team, teamId);
        var tp = new TeamPlayer(teamId, playerId);
        SetTeamPlayers(team, [tp]);
        return team;
    }

    private static PlayerProfile CreatePlayer(Guid id)
    {
        var player = new PlayerProfile("P1", 1.0m);
        SetPrivateId(player, id);
        return player;
    }

    private static void SetPrivateId(object entity, Guid id)
    {
        var prop = entity.GetType().GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop ??= entity.GetType().GetProperty("Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(entity, id);
    }

    private static void SetTeamPlayers(Team team, List<TeamPlayer> players)
    {
        var field = typeof(Team).GetField("_teamPlayers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<TeamPlayer>)field!.GetValue(team)!;
        list.Clear();
        list.AddRange(players);
    }
}
