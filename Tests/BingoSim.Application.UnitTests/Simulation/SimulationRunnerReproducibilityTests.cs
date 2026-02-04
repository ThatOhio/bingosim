using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class SimulationRunnerReproducibilityTests
{
    [Fact]
    public void Execute_SameSeed_Twice_ProducesIdenticalResults()
    {
        var snapshotJson = BuildMinimalSnapshotJson();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var run1 = runner.Execute(snapshotJson, "repro-seed-42", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "repro-seed-42", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints);
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
            run1[i].RowReached.Should().Be(run2[i].RowReached);
            run1[i].IsWinner.Should().Be(run2[i].IsWinner);
            run1[i].RowUnlockTimesJson.Should().Be(run2[i].RowUnlockTimesJson);
            run1[i].TileCompletionTimesJson.Should().Be(run2[i].TileCompletionTimesJson);
        }
    }

    [Fact]
    public void Execute_DifferentSeed_ProducesDeterministicResults()
    {
        var snapshotJson = BuildMinimalSnapshotJson();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var run1 = runner.Execute(snapshotJson, "seed-a", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "seed-b", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        run1.Should().OnlyContain(r => r.TeamId != default);
    }

    private static string BuildMinimalSnapshotJson()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Minimal",
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
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }]
                }
            ]
        };

        return System.Text.Json.JsonSerializer.Serialize(dto);
    }
}
