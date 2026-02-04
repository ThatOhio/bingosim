using System.Text.Json;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation;

/// <summary>
/// Standard perf scenario snapshot for regression guards and benchmarking.
/// Deterministic: 2 teams, 8 players (4 per team), always online.
/// </summary>
public static class PerfScenarioSnapshot
{
    /// <summary>
    /// Builds the standard perf snapshot JSON. Same structure as SimulationPerfScenarioTests.
    /// </summary>
    public static string BuildJson()
    {
        var actId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var team1Id = Guid.Parse("22222222-2222-2222-2222-222222222221");
        var team2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Perf Winter Bingo 2025",
            DurationSeconds = 86400,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId, ActivityKey = "act", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] }] }
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = team1Id,
                    TeamName = "Team Alpha",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333331"), Name = "Alice", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333332"), Name = "Bob", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Carol", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333334"), Name = "Dave", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                },
                new TeamSnapshotDto
                {
                    TeamId = team2Id,
                    TeamName = "Team Beta",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333335"), Name = "Eve", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333336"), Name = "Frank", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333337"), Name = "Grace", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333338"), Name = "Henry", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
