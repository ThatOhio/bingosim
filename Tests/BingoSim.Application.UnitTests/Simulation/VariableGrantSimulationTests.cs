using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Integration tests for variable progress grants (UnitsMin–UnitsMax) in simulation.
/// </summary>
public class VariableGrantSimulationTests
{
    [Fact]
    public void VariableGrant_SampledWithinRange_CompletesTile()
    {
        var snapshotJson = BuildSnapshotWithVariableGrant(requiredCount: 100);
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var results = runner.Execute(snapshotJson, "variable-grant-seed", CancellationToken.None);

        results.Should().NotBeEmpty();
        var team = results.First();
        team.TilesCompletedCount.Should().BeGreaterThan(0, "tile requiring 100 units with 50–100 per attempt should complete in 2+ attempts");
    }

    [Fact]
    public void VariableGrant_Deterministic_SameSeedSameResult()
    {
        var snapshotJson = BuildSnapshotWithVariableGrant(requiredCount: 50);
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var run1 = runner.Execute(snapshotJson, "variable-determinism", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "variable-determinism", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints);
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
        }
    }

    [Fact]
    public void VariableGrant_DifferentSeeds_CanProduceDifferentProgress()
    {
        var snapshotJson = BuildSnapshotWithVariableGrantSingleTile(requiredCount: 450, durationSeconds: 420);
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var completionCounts = new List<int>();
        for (var i = 0; i < 50; i++)
        {
            var run = runner.Execute(snapshotJson, $"variable-variance-{i}", CancellationToken.None);
            completionCounts.Add(run.First().TilesCompletedCount);
        }

        completionCounts.Distinct().Count().Should().BeGreaterThan(1,
            "variable grants (50-100 per attempt) with ~7 attempts should produce both 0 and 1 completion across seeds");
    }

    private static string BuildSnapshotWithVariableGrant(int requiredCount, int durationSeconds = 7200)
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "boss.arrows",
            AcceptedDropKeys = ["item.arrows"],
            RequirementKeys = [],
            Modifiers = []
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Variable Grant Test",
            DurationSeconds = durationSeconds,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "Arrows", Points = 1, RequiredCount = requiredCount, AllowedActivities = [rule] },
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
                    Key = "boss.arrows",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 0,
                            Outcomes =
                            [
                                new OutcomeSnapshotDto
                                {
                                    WeightNumerator = 1,
                                    WeightDenominator = 1,
                                    Grants =
                                    [
                                        new ProgressGrantSnapshotDto
                                        {
                                            DropKey = "item.arrows",
                                            Units = 50,
                                            UnitsMin = 50,
                                            UnitsMax = 100
                                        }
                                    ]
                                }
                            ]
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
                    TeamName = "Test Team",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto
                        {
                            PlayerId = Guid.NewGuid(),
                            Name = "Player",
                            SkillTimeMultiplier = 1.0m,
                            CapabilityKeys = [],
                            Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] }
                        }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithVariableGrantSingleTile(int requiredCount, int durationSeconds)
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "boss.arrows",
            AcceptedDropKeys = ["item.arrows"],
            RequirementKeys = [],
            Modifiers = []
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Variable Grant Single Tile",
            DurationSeconds = durationSeconds,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "Arrows", Points = 1, RequiredCount = requiredCount, AllowedActivities = [rule] }
                    ]
                }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "boss.arrows",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 0,
                            Outcomes =
                            [
                                new OutcomeSnapshotDto
                                {
                                    WeightNumerator = 1,
                                    WeightDenominator = 1,
                                    Grants =
                                    [
                                        new ProgressGrantSnapshotDto
                                        {
                                            DropKey = "item.arrows",
                                            Units = 50,
                                            UnitsMin = 50,
                                            UnitsMax = 100
                                        }
                                    ]
                                }
                            ]
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
                    TeamName = "Test Team",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto
                        {
                            PlayerId = Guid.NewGuid(),
                            Name = "Player",
                            SkillTimeMultiplier = 1.0m,
                            CapabilityKeys = [],
                            Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] }
                        }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
