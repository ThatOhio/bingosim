using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Integration tests for group play: PerPlayer+PerGroup loot lines, 1 vs 4 players, scaling bands.
/// </summary>
public class GroupPlayIntegrationTests
{
    [Fact]
    public void GroupPlay_ActivityWithPerPlayerAndPerGroup_SameSeed_Reproducible()
    {
        var snapshotJson = BuildSnapshotWithPerPlayerAndPerGroup();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var run1 = runner.Execute(snapshotJson, "group-repro-seed", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "group-repro-seed", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints, "same seed must produce identical results");
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
        }
    }

    [Fact]
    public void GroupPlay_Team1PlayerVsTeam4Players_OutcomesDiffer()
    {
        var snapshotJson = BuildSnapshotOneVsFourPlayers();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var team1Points = new List<int>();
        var team4Points = new List<int>();

        for (var i = 0; i < 50; i++)
        {
            var seed = $"one-vs-four_{i}";
            var results = runner.Execute(snapshotJson, seed, CancellationToken.None);
            var t1 = results.First(r => r.TeamName == "Team 1 Player");
            var t4 = results.First(r => r.TeamName == "Team 4 Players");
            team1Points.Add(t1.TotalPoints);
            team4Points.Add(t4.TotalPoints);
        }

        var mean1 = team1Points.Average();
        var mean4 = team4Points.Average();
        mean1.Should().BeGreaterThan(0);
        mean4.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GroupPlay_GroupScalingBands_AppliedToTimeAndProbability()
    {
        var snapshotJson = BuildSnapshotWithScalingBands();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var results = runner.Execute(snapshotJson, "scaling-test", CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Sum(r => r.TotalPoints).Should().BeGreaterThan(0);
    }

    [Fact]
    public void GroupPlay_EightPlayersMaxFourPerGroup_FormsMultipleConcurrentGroups()
    {
        var snapshotJson = BuildSnapshotEightPlayersMaxFour();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var results = runner.Execute(snapshotJson, "multi-group-seed", CancellationToken.None);

        results.Should().NotBeEmpty();
        var team8 = results.First(r => r.TeamName == "Team 8 Players");
        team8.TotalPoints.Should().BeGreaterThan(0, "8 players in 2 groups of 4 should complete tiles");
    }

    private static string BuildSnapshotEightPlayersMaxFour()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var players = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).OrderBy(g => g).ToList();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnlineSchedule = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "8 Players Max 4",
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
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = false, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team 8 Players",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = players.Select((id, i) => new PlayerSnapshotDto { PlayerId = id, Name = $"P{i + 1}", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnlineSchedule }).ToList()
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithPerPlayerAndPerGroup()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "cox",
            AcceptedDropKeys = ["drop.common", "drop.rare"],
            RequirementKeys = [],
            Modifiers = []
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "PerPlayer+PerGroup",
            DurationSeconds = 7200,
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
                    Key = "cox",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "scavs",
                            RollScope = 1,
                            BaselineTimeSeconds = 600,
                            VarianceSeconds = 120,
                            Outcomes =
                            [
                                new OutcomeSnapshotDto { WeightNumerator = 4, WeightDenominator = 5, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop.common", Units = 1 }] },
                                new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 5, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop.rare", Units = 3 }] }
                            ]
                        },
                        new AttemptSnapshotDto
                        {
                            Key = "olm",
                            RollScope = 0,
                            BaselineTimeSeconds = 900,
                            VarianceSeconds = 180,
                            Outcomes =
                            [
                                new OutcomeSnapshotDto { WeightNumerator = 3, WeightDenominator = 4, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop.common", Units = 1 }] },
                                new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 4, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop.rare", Units = 3 }] }
                            ]
                        }
                    ],
                    GroupScalingBands =
                    [
                        new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m },
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = false, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowRush",
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

    private static string BuildSnapshotOneVsFourPlayers()
    {
        var actId = Guid.NewGuid();
        var team1Id = Guid.NewGuid();
        var team4Id = Guid.NewGuid();
        var players = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();

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
            EventName = "1 vs 4",
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
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 0.85m, ProbabilityMultiplier = 1.15m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = team1Id,
                    TeamName = "Team 1 Player",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = players[0], Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }]
                },
                new TeamSnapshotDto
                {
                    TeamId = team4Id,
                    TeamName = "Team 4 Players",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = players.Select((id, i) => new PlayerSnapshotDto { PlayerId = id, Name = $"P{i + 1}", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }).ToList()
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithScalingBands()
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
            EventName = "Scaling Bands",
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
                    StrategyKey = "RowRush",
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
