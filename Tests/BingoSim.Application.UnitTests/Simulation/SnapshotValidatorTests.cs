using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class SnapshotValidatorTests
{
    [Fact]
    public void Validate_EventStartTimeEtMissing_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var dto = new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = snapshot.Rows,
            ActivitiesById = snapshot.ActivitiesById,
            Teams = snapshot.Teams,
            EventStartTimeEt = ""
        };

        var act = () => SnapshotValidator.Validate(dto);

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*EventStartTimeEt*");
    }

    [Fact]
    public void Validate_EventStartTimeEtInvalidFormat_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var dto = new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = snapshot.Rows,
            ActivitiesById = snapshot.ActivitiesById,
            Teams = snapshot.Teams,
            EventStartTimeEt = "not-a-date"
        };

        var act = () => SnapshotValidator.Validate(dto);

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*EventStartTimeEt*");
    }

    [Fact]
    public void Validate_ScheduleMissingOnPlayer_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var team = snapshot.Teams[0];
        var player = team.Players[0];
        var newPlayer = new PlayerSnapshotDto
        {
            PlayerId = player.PlayerId,
            Name = player.Name,
            SkillTimeMultiplier = player.SkillTimeMultiplier,
            CapabilityKeys = player.CapabilityKeys,
            Schedule = null!
        };
        var newTeam = new TeamSnapshotDto
        {
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            StrategyKey = team.StrategyKey,
            ParamsJson = team.ParamsJson,
            Players = [newPlayer]
        };

        var act = () => SnapshotValidator.Validate(new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = snapshot.Rows,
            ActivitiesById = snapshot.ActivitiesById,
            Teams = [newTeam],
            EventStartTimeEt = snapshot.EventStartTimeEt
        });

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*Schedule*");
    }

    [Fact]
    public void Validate_ModeSupportMissing_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var actId = snapshot.ActivitiesById.Keys.First();
        var activity = snapshot.ActivitiesById[actId];
        var newActivity = new ActivitySnapshotDto
        {
            Id = activity.Id,
            Key = activity.Key,
            Attempts = activity.Attempts,
            GroupScalingBands = activity.GroupScalingBands,
            ModeSupport = null!
        };
        var newActivities = new Dictionary<Guid, ActivitySnapshotDto>(snapshot.ActivitiesById) { [actId] = newActivity };

        var act = () => SnapshotValidator.Validate(new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = snapshot.Rows,
            ActivitiesById = newActivities,
            Teams = snapshot.Teams,
            EventStartTimeEt = snapshot.EventStartTimeEt
        });

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*ModeSupport*");
    }

    [Fact]
    public void Validate_ModifiersNull_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var row = snapshot.Rows[0];
        var tile = row.Tiles[0];
        var rule = tile.AllowedActivities[0];
        var newRule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = rule.ActivityDefinitionId,
            ActivityKey = rule.ActivityKey,
            AcceptedDropKeys = rule.AcceptedDropKeys,
            RequirementKeys = rule.RequirementKeys,
            Modifiers = null!
        };
        var newTile = new TileSnapshotDto
        {
            Key = tile.Key,
            Name = tile.Name,
            Points = tile.Points,
            RequiredCount = tile.RequiredCount,
            AllowedActivities = [newRule]
        };
        var newRow = new RowSnapshotDto { Index = row.Index, Tiles = [newTile] };

        var act = () => SnapshotValidator.Validate(new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = [newRow],
            ActivitiesById = snapshot.ActivitiesById,
            Teams = snapshot.Teams,
            EventStartTimeEt = snapshot.EventStartTimeEt
        });

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*Modifiers*");
    }

    [Fact]
    public void Validate_StrategyKeyMissing_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var team = snapshot.Teams[0];
        var newTeam = new TeamSnapshotDto
        {
            TeamId = team.TeamId,
            TeamName = team.TeamName,
            StrategyKey = "",
            ParamsJson = team.ParamsJson,
            Players = team.Players
        };

        var act = () => SnapshotValidator.Validate(new EventSnapshotDto
        {
            EventName = snapshot.EventName,
            DurationSeconds = snapshot.DurationSeconds,
            UnlockPointsRequiredPerRow = snapshot.UnlockPointsRequiredPerRow,
            Rows = snapshot.Rows,
            ActivitiesById = snapshot.ActivitiesById,
            Teams = [newTeam],
            EventStartTimeEt = snapshot.EventStartTimeEt
        });

        act.Should().Throw<SnapshotValidationException>()
            .WithMessage("*StrategyKey*");
    }

    [Fact]
    public void Validate_ValidSnapshot_Passes()
    {
        var snapshot = CreateValidSnapshot();

        var act = () => SnapshotValidator.Validate(snapshot);

        act.Should().NotThrow();
    }

    private static EventSnapshotDto CreateValidSnapshot()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));

        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }
                    ]
                }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "act",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 0,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
                }
            ]
        };
    }
}
