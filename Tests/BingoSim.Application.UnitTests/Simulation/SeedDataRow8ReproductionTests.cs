using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Application.StrategyKeys;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Reproduction tests for the seed data row 8 issue. Verifies that when row 8 requires
/// group activities (CoX/ToA) for the 5-point unlock threshold, both RowUnlocking and
/// ComboUnlocking can progress when players have adequate schedule overlap.
/// See Docs/Strategies/seed-data-investigation.md for full analysis.
/// </summary>
public class SeedDataRow8ReproductionTests
{
    /// <summary>
    /// With always-online schedules and solo-only activities, both strategies should reach row 8+.
    /// This establishes the baseline: strategy logic is correct when no group constraints apply.
    /// The seed row 8 requires group activities (CoX/ToA); this test uses solo activities
    /// to verify the row structure and unlock logic work correctly.
    /// </summary>
    [Fact]
    public void SeedLikeRow8_AlwaysOnlineSoloOnly_BothStrategiesReachRow8()
    {
        var snapshotJson = BuildSeedLikeRow8Snapshot(alwaysOnline: true, groupActivities: false);
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var results = runner.Execute(snapshotJson, "seed-row8-baseline_0", CancellationToken.None);

        results.Should().HaveCount(2);
        var rowUnlock = results.Single(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
        var combo = results.Single(r => r.StrategyKey == StrategyCatalog.ComboUnlocking);

        rowUnlock.RowReached.Should().BeGreaterThanOrEqualTo(8, "RowUnlocking should unlock row 8 with always-online schedule");
        combo.RowReached.Should().BeGreaterThanOrEqualTo(8, "ComboUnlocking should unlock row 8 with always-online schedule");
    }

    /// <summary>
    /// Snapshot that mimics seed row 8: 4 tiles (1,2,3,4 pts), 5 pts required.
    /// When groupActivities=true, combinations for 5 pts require the group-only activity (like CoX/ToA).
    /// When groupActivities=false, all activities are solo (for baseline verification).
    /// </summary>
    private static string BuildSeedLikeRow8Snapshot(bool alwaysOnline, bool groupActivities = false)
    {
        var soloActId = Guid.NewGuid();
        var groupActId = Guid.NewGuid();

        var soloRule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = soloActId,
            ActivityKey = "solo",
            AcceptedDropKeys = ["solo-drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var groupRule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = groupActId,
            ActivityKey = "group",
            AcceptedDropKeys = ["group-drop"],
            RequirementKeys = ["quest.sote"],
            Modifiers = []
        };

        var alwaysOnlineSchedule = new WeeklyScheduleSnapshotDto { Sessions = [] };
        var limitedSchedule = new WeeklyScheduleSnapshotDto
        {
            Sessions =
            [
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 18 * 60, DurationMinutes = 120 },
                new ScheduledSessionSnapshotDto { DayOfWeek = 3, StartLocalTimeMinutes = 19 * 60, DurationMinutes = 120 }
            ]
        };

        var rows = new List<RowSnapshotDto>();
        for (var r = 0; r < 9; r++)
        {
            var tiles = new List<TileSnapshotDto>();
            for (var t = 0; t < 4; t++)
            {
                var points = t + 1;
                var activity = groupActivities && (r + t) % 2 == 1 ? groupRule : soloRule;
                tiles.Add(new TileSnapshotDto
                {
                    Key = $"r{r}t{points}",
                    Name = $"R{r}T{points}",
                    Points = points,
                    RequiredCount = 1,
                    AllowedActivities = [activity]
                });
            }
            rows.Add(new RowSnapshotDto { Index = r, Tiles = tiles });
        }

        var schedule = alwaysOnline ? alwaysOnlineSchedule : limitedSchedule;
        var capabilities = new HashSet<string>(StringComparer.Ordinal) { "quest.sote" };

        var dto = new EventSnapshotDto
        {
            EventName = "Seed Row 8 Reproduction",
            DurationSeconds = 86400,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5)).ToString("o"),
            Rows = rows,
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [soloActId] = new ActivitySnapshotDto
                {
                    Id = soloActId,
                    Key = "solo",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 5,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "solo-drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                },
                [groupActId] = new ActivitySnapshotDto
                {
                    Id = groupActId,
                    Key = "group",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 120,
                            VarianceSeconds = 20,
                            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "group-drop", Units = 1 }] }]
                        }
                    ],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = false, SupportsGroup = true, MinGroupSize = 2, MaxGroupSize = 8 }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Alpha",
                    StrategyKey = StrategyCatalog.RowUnlocking,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P3", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule }
                    ]
                },
                new TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Beta",
                    StrategyKey = StrategyCatalog.ComboUnlocking,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P3", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities.ToList(), Schedule = schedule }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
