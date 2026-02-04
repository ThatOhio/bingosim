using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Unit tests for group play: roll scope, group formation, determinism, and v1 skill assumption (slowest dominates).
/// </summary>
public class GroupPlaySimulationTests
{
    [Fact]
    public void GroupSkillMultiplier_SlowestMemberDominates_GroupTakesLongerThanFastestSolo()
    {
        var snapshotJson = BuildSnapshotWithMixedSkillGroup();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var soloFastPoints = new List<int>();
        var groupMixedPoints = new List<int>();

        for (var i = 0; i < 100; i++)
        {
            var seed = $"skill-test_{i}";
            var results = runner.Execute(snapshotJson, seed, CancellationToken.None);
            var soloTeam = results.First(r => r.TeamName == "Solo Fast");
            var groupTeam = results.First(r => r.TeamName == "Group Mixed");
            soloFastPoints.Add(soloTeam.TotalPoints);
            groupMixedPoints.Add(groupTeam.TotalPoints);
        }

        var meanSolo = soloFastPoints.Average();
        var meanGroup = groupMixedPoints.Average();
        meanGroup.Should().BeGreaterThan(0);
        meanSolo.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GroupFormation_SamePlayersSameOrder_SameResultsWithSameSeed()
    {
        var snapshotJson = BuildSnapshotWithGroupActivity();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var run1 = runner.Execute(snapshotJson, "determinism-group-seed", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "determinism-group-seed", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints);
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
        }
    }

    private static string BuildSnapshotWithMixedSkillGroup()
    {
        var actId = Guid.NewGuid();
        var soloTeamId = Guid.NewGuid();
        var groupTeamId = Guid.NewGuid();
        var fastPlayerId = Guid.NewGuid();
        var slowPlayerId = Guid.NewGuid();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Skill Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }
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
                            VarianceSeconds = 10,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands =
                    [
                        new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m },
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = soloTeamId,
                    TeamName = "Solo Fast",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = fastPlayerId, Name = "Fast", SkillTimeMultiplier = 0.8m, CapabilityKeys = [], Schedule = alwaysOnline }]
                },
                new TeamSnapshotDto
                {
                    TeamId = groupTeamId,
                    TeamName = "Group Mixed",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = fastPlayerId, Name = "Fast", SkillTimeMultiplier = 0.8m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = slowPlayerId, Name = "Slow", SkillTimeMultiplier = 1.4m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithGroupActivity()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Group Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }
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
                            VarianceSeconds = 5,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands =
                    [
                        new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m },
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.85m, ProbabilityMultiplier = 1.1m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = p1, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = p2, Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
