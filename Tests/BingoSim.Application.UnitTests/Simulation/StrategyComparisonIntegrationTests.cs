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
/// Integration tests comparing Greedy vs RowUnlocking strategies on the same event configuration.
/// Runs full simulations with both strategies and compares results.
/// </summary>
public class StrategyComparisonIntegrationTests
{
    [Fact]
    public void BothStrategies_SameEvent_CompleteSuccessfully()
    {
        var snapshotJson = BuildComparisonSnapshot();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var results = runner.Execute(snapshotJson, "strategy-compare_0", CancellationToken.None);

        results.Should().HaveCount(2);
        var rowUnlocking = results.First(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
        var greedy = results.First(r => r.StrategyKey == StrategyCatalog.Greedy);

        rowUnlocking.TotalPoints.Should().BeGreaterThan(0);
        greedy.TotalPoints.Should().BeGreaterThan(0);
        rowUnlocking.TilesCompletedCount.Should().BeGreaterThan(0);
        greedy.TilesCompletedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BothStrategies_SameSeed_Deterministic()
    {
        var snapshotJson = BuildComparisonSnapshot();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var r1 = runner.Execute(snapshotJson, "determinism-test_0", CancellationToken.None);
        var r2 = runner.Execute(snapshotJson, "determinism-test_0", CancellationToken.None);

        r1.Should().HaveCount(r2.Count);
        for (var i = 0; i < r1.Count; i++)
        {
            r1[i].TotalPoints.Should().Be(r2[i].TotalPoints);
            r1[i].RowUnlockTimesJson.Should().Be(r2[i].RowUnlockTimesJson);
            r1[i].TileCompletionTimesJson.Should().Be(r2[i].TileCompletionTimesJson);
        }
    }

    [Fact]
    public void BothStrategies_NoInterference_EachTeamUsesOwnStrategy()
    {
        var snapshotJson = BuildComparisonSnapshot();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var results = runner.Execute(snapshotJson, "no-interference_0", CancellationToken.None);

        var rowUnlocking = results.Single(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
        var greedy = results.Single(r => r.StrategyKey == StrategyCatalog.Greedy);

        rowUnlocking.TeamName.Should().Be("Team RowUnlocking");
        greedy.TeamName.Should().Be("Team Greedy");
    }

    [Fact]
    public void StrategySwitching_WorksCorrectly()
    {
        var snapshotRowUnlock = BuildSingleTeamSnapshot(StrategyCatalog.RowUnlocking);
        var snapshotGreedy = BuildSingleTeamSnapshot(StrategyCatalog.Greedy);
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var resultsRowUnlock = runner.Execute(snapshotRowUnlock, "switch-test_0", CancellationToken.None);
        var resultsGreedy = runner.Execute(snapshotGreedy, "switch-test_0", CancellationToken.None);

        resultsRowUnlock.Should().HaveCount(1);
        resultsGreedy.Should().HaveCount(1);
        resultsRowUnlock[0].StrategyKey.Should().Be(StrategyCatalog.RowUnlocking);
        resultsGreedy[0].StrategyKey.Should().Be(StrategyCatalog.Greedy);
    }

    [Fact]
    public void MultiRowEvent_BothStrategies_ProduceValidResults()
    {
        var snapshotJson = BuildMultiRowComparisonSnapshot();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var results = runner.Execute(snapshotJson, "multirow_0", CancellationToken.None);

        results.Should().HaveCount(2);
        foreach (var r in results)
        {
            r.TotalPoints.Should().BeGreaterThanOrEqualTo(0);
            r.RowReached.Should().BeInRange(0, 3);
            r.RowUnlockTimesJson.Should().NotBeNullOrEmpty();
        }
    }

    private static string BuildComparisonSnapshot()
    {
        var actId = Guid.NewGuid();
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Strategy Comparison",
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
                        new TileSnapshotDto { Key = "r0t1", Name = "R0T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "r0t2", Name = "R0T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "r0t3", Name = "R0T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule] },
                        new TileSnapshotDto { Key = "r0t4", Name = "R0T4", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = team1Id,
                    TeamName = "Team RowUnlocking",
                    StrategyKey = StrategyCatalog.RowUnlocking,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                },
                new TeamSnapshotDto
                {
                    TeamId = team2Id,
                    TeamName = "Team Greedy",
                    StrategyKey = StrategyCatalog.Greedy,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline },
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSingleTeamSnapshot(string strategyKey)
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Single Team",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows = [new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }] }],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "act",
                    Attempts = [new AttemptSnapshotDto { Key = "main", RollScope = 0, BaselineTimeSeconds = 60, VarianceSeconds = 0, Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }] }],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams = [new TeamSnapshotDto { TeamId = teamId, TeamName = "Team", StrategyKey = strategyKey, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] }]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildMultiRowComparisonSnapshot()
    {
        var actId = Guid.NewGuid();
        var team1Id = Guid.NewGuid();
        var team2Id = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = []
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Multi-Row Comparison",
            DurationSeconds = 14400,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "r0t1", Name = "R0T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }, new TileSnapshotDto { Key = "r0t2", Name = "R0T2", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "r1t1", Name = "R1T1", Points = 2, RequiredCount = 1, AllowedActivities = [rule] }, new TileSnapshotDto { Key = "r1t2", Name = "R1T2", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 2, Tiles = [new TileSnapshotDto { Key = "r2t1", Name = "R2T1", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "act",
                    Attempts = [new AttemptSnapshotDto { Key = "main", RollScope = 0, BaselineTimeSeconds = 60, VarianceSeconds = 5, Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }] }],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto { TeamId = team1Id, TeamName = "RowUnlocking", StrategyKey = StrategyCatalog.RowUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] },
                new TeamSnapshotDto { TeamId = team2Id, TeamName = "Greedy", StrategyKey = StrategyCatalog.Greedy, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
