using System.Text.Json;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Engine-only perf tests. No DB, no DI repositories. Excluded from normal CI via Category filter.
/// Run with: dotnet test --filter "Category=Perf"
/// </summary>
[Trait("Category", "Perf")]
public class SimulationPerfScenarioTests
{
    private const int DefaultRunCount = 10_000;
    private const int DefaultTimeoutSeconds = 300;

    [Fact]
    public void EngineOnly_10000Runs_CompletesWithinReasonableTime()
    {
        var snapshotJson = BuildPerfSnapshotJson();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var completed = 0;
        for (var i = 0; i < DefaultRunCount; i++)
        {
            var runSeed = SeedDerivation.DeriveRunSeedString("perf-baseline-2025", i);
            _ = runner.Execute(snapshotJson, runSeed, CancellationToken.None);
            completed++;
        }
        sw.Stop();

        completed.Should().Be(DefaultRunCount);
        var runsPerSec = sw.Elapsed.TotalSeconds > 0 ? completed / sw.Elapsed.TotalSeconds : 0;
        // Sanity: expect at least 50 runs/sec on typical hardware
        runsPerSec.Should().BeGreaterThan(50, "engine-only 10K runs should complete at reasonable throughput");
    }

    [Fact]
    public void EngineOnly_SameSeed_Deterministic()
    {
        var snapshotJson = BuildPerfSnapshotJson();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var r1 = runner.Execute(snapshotJson, "perf-baseline-2025_0", CancellationToken.None);
        var r2 = runner.Execute(snapshotJson, "perf-baseline-2025_0", CancellationToken.None);

        r1.Should().HaveCount(r2.Count);
        for (var i = 0; i < r1.Count; i++)
        {
            r1[i].TotalPoints.Should().Be(r2[i].TotalPoints);
            r1[i].RowUnlockTimesJson.Should().Be(r2[i].RowUnlockTimesJson);
        }
    }

    /// <summary>
    /// Builds a snapshot matching the perf scenario: 2 teams, 8 players (4 per team), always online.
    /// Deterministic and comparable across runs.
    /// </summary>
    private static string BuildPerfSnapshotJson()
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
                    StrategyKey = "RowRush",
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
                    StrategyKey = "GreedyPoints",
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
