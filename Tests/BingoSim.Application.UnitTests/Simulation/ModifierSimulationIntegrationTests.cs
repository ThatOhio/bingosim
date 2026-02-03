using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Integration-style tests: full SimulationRunner with modifiers. Verifies that players with
/// modifier capabilities outperform those without (faster time + better probability).
/// </summary>
public class ModifierSimulationIntegrationTests
{
    [Fact]
    public void Execute_PlayerWithModifierCapability_OutperformsOrMatchesPlayerWithout()
    {
        var snapshotJson = BuildSnapshotWithAndWithoutCapability();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var teamAPoints = new List<int>();
        var teamBPoints = new List<int>();

        for (var i = 0; i < 500; i++)
        {
            var seed = $"modifier-test_{i}";
            var results = runner.Execute(snapshotJson, seed, CancellationToken.None);
            var teamA = results.First(r => r.TeamName == "Team With Capability");
            var teamB = results.First(r => r.TeamName == "Team Without Capability");
            teamAPoints.Add(teamA.TotalPoints);
            teamBPoints.Add(teamB.TotalPoints);
        }

        var meanA = teamAPoints.Average();
        var meanB = teamBPoints.Average();
        meanA.Should().BeGreaterThanOrEqualTo(meanB, "player with modifier capability (faster time, better drop chance) should not underperform");
        teamAPoints.Sum().Should().BeGreaterThan(0, "simulation should produce progress");
    }

    [Fact]
    public void Execute_SnapshotWithoutModifiers_RunsSuccessfully()
    {
        var snapshotJson = BuildMinimalSnapshotWithoutModifiers();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var results = runner.Execute(snapshotJson, "backward-compat-seed", CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Sum(r => r.TotalPoints).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Execute_SameSeedWithModifiers_ProducesDeterministicResults()
    {
        var snapshotJson = BuildSnapshotWithModifiers();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var run1 = runner.Execute(snapshotJson, "determinism-mod-seed", CancellationToken.None);
        var run2 = runner.Execute(snapshotJson, "determinism-mod-seed", CancellationToken.None);

        run1.Should().HaveCount(run2.Count);
        for (var i = 0; i < run1.Count; i++)
        {
            run1[i].TotalPoints.Should().Be(run2[i].TotalPoints);
            run1[i].TilesCompletedCount.Should().Be(run2[i].TilesCompletedCount);
        }
    }

    private static string BuildSnapshotWithAndWithoutCapability()
    {
        var actId = Guid.NewGuid();
        var teamAId = Guid.NewGuid();
        var teamBId = Guid.NewGuid();
        var playerAId = Guid.NewGuid();
        var playerBId = Guid.NewGuid();

        var ruleWithModifier = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers =
            [
                new ActivityModifierRuleSnapshotDto { CapabilityKey = "quest.ds2", TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.2m }
            ]
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Modifier Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [ruleWithModifier] }
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
                            Outcomes =
                            [
                                new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [] },
                                new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }
                            ]
                        }
                    ],
                    GroupScalingBands = []
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamAId,
                    TeamName = "Team With Capability",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerAId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = ["quest.ds2"] }]
                },
                new TeamSnapshotDto
                {
                    TeamId = teamBId,
                    TeamName = "Team Without Capability",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players = [new PlayerSnapshotDto { PlayerId = playerBId, Name = "P2", SkillTimeMultiplier = 1.0m, CapabilityKeys = [] }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    private static string BuildSnapshotWithModifiers()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var ruleWithModifier = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = [new ActivityModifierRuleSnapshotDto { CapabilityKey = "quest.ds2", TimeMultiplier = 0.9m, ProbabilityMultiplier = 1.1m }]
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Modifier Determinism",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [ruleWithModifier] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [ruleWithModifier] }
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
                    GroupScalingBands = []
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
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = ["quest.ds2"] }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// Builds snapshot JSON that omits Modifiers (simulates pre-Slice-8 snapshot).
    /// Verifies backward compatibility when deserializing old snapshots.
    /// </summary>
    private static string BuildMinimalSnapshotWithoutModifiers()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var ruleWithoutModifiers = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = []
            // Modifiers intentionally omitted - when deserialized from old JSON, may be null
        };

        var dto = new EventSnapshotDto
        {
            EventName = "Backward Compat",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [ruleWithoutModifiers] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [ruleWithoutModifiers] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [ruleWithoutModifiers] },
                        new TileSnapshotDto { Key = "t4", Name = "T4", Points = 4, RequiredCount = 1, AllowedActivities = [ruleWithoutModifiers] }
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
                    GroupScalingBands = []
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
                    Players = [new PlayerSnapshotDto { PlayerId = playerId, Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [] }]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
