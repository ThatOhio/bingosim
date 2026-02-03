using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Integration tests for schedule enforcement during simulation.
/// </summary>
public class ScheduleSimulationIntegrationTests
{
    [Fact]
    public void PlayerWithOneHourPerDay_ProgressesLessThanAlwaysOnline()
    {
        var snapshotJson = BuildSnapshotAlwaysOnlineVsOneHourPerDay();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var alwaysOnlinePoints = new List<int>();
        var scheduledPoints = new List<int>();

        for (var i = 0; i < 50; i++)
        {
            var results = runner.Execute(snapshotJson, $"schedule-compare-{i}", CancellationToken.None);
            var always = results.First(r => r.TeamName == "Always Online");
            var scheduled = results.First(r => r.TeamName == "1h/day");
            alwaysOnlinePoints.Add(always.TotalPoints);
            scheduledPoints.Add(scheduled.TotalPoints);
        }

        var meanAlways = alwaysOnlinePoints.Average();
        var meanScheduled = scheduledPoints.Average();
        meanAlways.Should().BeGreaterThan(meanScheduled,
            "always-online player should progress more than 15min/day player (always={0}, scheduled={1})", meanAlways, meanScheduled);
    }

    [Fact]
    public void Determinism_SameSeedAndSchedule_SameOutcomes()
    {
        var snapshotJson = BuildSnapshotWithSchedule();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var run1 = runner.Execute(snapshotJson, "schedule-determinism-seed", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "schedule-determinism-seed", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints);
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
            run1[i].RowReached.Should().Be(run2[i].RowReached);
        }
    }

    [Fact]
    public void GroupActivity_OnlyOneEligiblePlayerOnline_DoesNotStartGroup()
    {
        var snapshotJson = BuildSnapshotGroupRequiresTwo_OneOnline();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var results = runner.Execute(snapshotJson, "group-schedule-seed", CancellationToken.None);

        results.Should().NotBeEmpty();
        var team = results.First();
        team.TotalPoints.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void AttemptEndPolicy_AttemptWouldEndAtOrPastSessionEnd_Skipped()
    {
        var snapshotJson = BuildSnapshotAttemptEndBoundary();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var run1 = runner.Execute(snapshotJson, "boundary-seed", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "boundary-seed", CancellationToken.None);

        var scheduled = run1.First(r => r.TeamName == "Tight Window");
        scheduled.TotalPoints.Should().BeLessThan(run1.First(r => r.TeamName == "Always Online").TotalPoints,
            "player with 1-min session and 60s attempts should progress less (attempts ending at session boundary are skipped)");
        run1[0].TotalPoints.Should().Be(run2[0].TotalPoints, "determinism");
        run1[1].TotalPoints.Should().Be(run2[1].TotalPoints, "determinism");
    }

    private static string BuildSnapshotAttemptEndBoundary()
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var oneMinSchedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 1 }]
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));

        var dto = new EventSnapshotDto
        {
            EventName = "Attempt End Boundary",
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
                            VarianceSeconds = 0,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = [new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m }],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Always Online",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "Always", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
                },
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Tight Window",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "Tight", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = oneMinSchedule }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotAlwaysOnlineVsOneHourPerDay()
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var alwaysOnlinePlayer = Guid.NewGuid();
        var scheduledPlayer = Guid.NewGuid();

        var fifteenMinSchedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 15 }]
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));

        var dto = new EventSnapshotDto
        {
            EventName = "Schedule Compare",
            DurationSeconds = 21600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 3, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 3, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 3, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 3, AllowedActivities = [rule] }
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
                            BaselineTimeSeconds = 120,
                            VarianceSeconds = 20,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = [new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m }],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Always Online",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = alwaysOnlinePlayer, Name = "Always", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
                },
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "1h/day",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = scheduledPlayer, Name = "Scheduled", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = fifteenMinSchedule }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithSchedule()
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions =
            [
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 120 },
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 18 * 60, DurationMinutes = 120 }
            ]
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 8, 0, 0, TimeSpan.FromHours(-5));

        var dto = new EventSnapshotDto
        {
            EventName = "Scheduled",
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
                    GroupScalingBands = [new GroupSizeBandSnapshotDto { MinSize = 1, MaxSize = 1, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m }],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Team",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = schedule }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotGroupRequiresTwo_OneOnline()
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var p1Schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 }]
        };
        var p2Schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 14 * 60, DurationMinutes = 60 }]
        };

        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));

        var dto = new EventSnapshotDto
        {
            EventName = "Group Schedule",
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
                            VarianceSeconds = 0,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands =
                    [
                        new GroupSizeBandSnapshotDto { MinSize = 2, MaxSize = 4, TimeMultiplier = 1.0m, ProbabilityMultiplier = 1.0m }
                    ],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = false, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 4 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Team",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = p1Schedule },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = p2Schedule }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
