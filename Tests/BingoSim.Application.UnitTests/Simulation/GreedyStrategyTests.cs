using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Application.Simulation.Strategies;
using BingoSim.Application.StrategyKeys;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Verifies GreedyStrategy behavior: highest points first, completion time tie-breaker, deterministic key fallback.
/// </summary>
public class GreedyStrategyTests
{
    [Fact]
    public void StrategyCatalog_ContainsGreedy()
    {
        StrategyCatalog.IsSupported(StrategyCatalog.Greedy).Should().BeTrue();
        StrategyCatalog.GetSupportedKeys().Should().Contain(StrategyCatalog.Greedy);
    }

    [Fact]
    public void Factory_GetStrategy_Greedy_ReturnsGreedyStrategy()
    {
        var factory = new TeamStrategyFactory();
        var strategy = factory.GetStrategy(StrategyCatalog.Greedy);

        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<GreedyStrategy>();
    }

    [Fact]
    public void SelectTargetTileForGrant_NoEligible_ReturnsNull()
    {
        var strategy = new GreedyStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(),
            TileRowIndex = new Dictionary<string, int>(),
            TilePoints = new Dictionary<string, int>(),
            EligibleTileKeys = [],
            EventSnapshot = BuildMinimalEventSnapshot()
        };
        strategy.SelectTargetTileForGrant(context).Should().BeNull();
    }

    [Fact]
    public void SelectTargetTileForGrant_HighestPoint_Selected()
    {
        var strategy = new GreedyStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 1 },
            TilePoints = new Dictionary<string, int> { ["a"] = 2, ["b"] = 4, ["c"] = 3 },
            EligibleTileKeys = ["a", "b", "c"],
            EventSnapshot = BuildEventSnapshotWithTiles(["a", "b", "c"], [2, 4, 3])
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("b");
    }

    [Fact]
    public void SelectTargetTileForGrant_TieBreakByTileKey_Deterministic()
    {
        var strategy = new GreedyStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["x"] = 1, ["a"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TilePoints = new Dictionary<string, int> { ["x"] = 4, ["a"] = 4 },
            EligibleTileKeys = ["x", "a"],
            EventSnapshot = BuildEventSnapshotWithTiles(["x", "a"], [4, 4])
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("a");
    }

    [Fact]
    public void SelectTargetTileForGrant_CrossRow_SelectsHighestPointRegardlessOfRow()
    {
        var strategy = new GreedyStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 },
            TilePoints = new Dictionary<string, int> { ["a"] = 4, ["b"] = 2 },
            EligibleTileKeys = ["a", "b"],
            EventSnapshot = BuildEventSnapshotWithTiles(["a", "b"], [4, 2])
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("a");
    }

    [Fact]
    public void SelectTaskForPlayer_NoUnlockedRows_ReturnsNull()
    {
        var strategy = new GreedyStrategy();
        var context = BuildTaskSelectionContext(unlockedRows: new HashSet<int>(), hasWorkableTile: true);

        strategy.SelectTaskForPlayer(context).Should().BeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_AllTilesCompleted_ReturnsNull()
    {
        var strategy = new GreedyStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();
        var contextWithCompleted = new TaskSelectionContext
        {
            PlayerIndex = context.PlayerIndex,
            PlayerCapabilities = context.PlayerCapabilities,
            EventSnapshot = context.EventSnapshot,
            TeamSnapshot = context.TeamSnapshot,
            UnlockedRowIndices = context.UnlockedRowIndices,
            TileProgress = context.TileProgress,
            TileRequiredCount = context.TileRequiredCount,
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal) { "t1" },
            TileRowIndex = context.TileRowIndex,
            TilePoints = context.TilePoints,
            TileToRules = context.TileToRules
        };

        strategy.SelectTaskForPlayer(contextWithCompleted).Should().BeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_PlayerCanWorkOnTile_ReturnsTask()
    {
        var strategy = new GreedyStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
        result.Value.rule.Should().NotBeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_HighestPointTile_Selected()
    {
        var strategy = new GreedyStrategy();
        var (context, tileKeyToActivityId) = BuildTaskSelectionContextWithMultipleTilesDistinctActivities();

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
        var selectedTileKey = tileKeyToActivityId.FirstOrDefault(kv => kv.Value == result.Value.activityId).Key;
        selectedTileKey.Should().NotBeNull();
        selectedTileKey.Should().Be("t2");
    }

    [Fact]
    public void Simulation_WithGreedyTeam_DoesNotCrash()
    {
        var snapshotJson = BuildSnapshotWithGreedyTeam();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var act = () => runner.Execute(snapshotJson, "greedy-shell-test_0", CancellationToken.None);
        act.Should().NotThrow();
        var results = runner.Execute(snapshotJson, "greedy-shell-test_0", CancellationToken.None);
        results.Should().NotBeEmpty();
        var greedyTeam = results.First(r => r.StrategyKey == StrategyCatalog.Greedy);
        greedyTeam.Should().NotBeNull();
    }

    private static EventSnapshotDto BuildMinimalEventSnapshot()
    {
        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows = [],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>(),
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };
    }

    private static EventSnapshotDto BuildEventSnapshotWithTiles(IReadOnlyList<string> keys, IReadOnlyList<int> points)
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

        var tiles = keys.Zip(points, (k, p) => new TileSnapshotDto
        {
            Key = k,
            Name = k.ToUpperInvariant(),
            Points = p,
            RequiredCount = 1,
            AllowedActivities = [rule]
        }).ToList();

        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows = [new RowSnapshotDto { Index = 0, Tiles = tiles }],
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };
    }

    private static TaskSelectionContext BuildTaskSelectionContext(
        IReadOnlySet<int> unlockedRows,
        bool hasWorkableTile)
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

        var snapshot = new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows = hasWorkableTile
                ? [new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }] }]
                : [],
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.Greedy,
            Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
        };

        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal);
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal);
        foreach (var row in snapshot.Rows)
        {
            foreach (var tile in row.Tiles)
            {
                tileRowIndex[tile.Key] = row.Index;
                tilePoints[tile.Key] = tile.Points;
                tileToRules[tile.Key] = tile.AllowedActivities;
            }
        }

        return new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = new HashSet<int>(unlockedRows),
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = tileRowIndex.ToDictionary(k => k.Key, v => 1, StringComparer.Ordinal),
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
    }

    private static TaskSelectionContext BuildTaskSelectionContextWithWorkableTile()
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

        var snapshot = new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles = [new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }]
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.Greedy,
            Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
        };

        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 0 };
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 1 };
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal) { ["t1"] = [rule] };

        return new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 1 },
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
    }

    private static (TaskSelectionContext context, IReadOnlyDictionary<string, Guid> tileKeyToActivityId) BuildTaskSelectionContextWithMultipleTilesDistinctActivities()
    {
        var actId1 = Guid.NewGuid();
        var actId2 = Guid.NewGuid();
        var actId3 = Guid.NewGuid();
        var rule1 = new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId1, ActivityKey = "act1", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] };
        var rule2 = new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId2, ActivityKey = "act2", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] };
        var rule3 = new TileActivityRuleSnapshotDto { ActivityDefinitionId = actId3, ActivityKey = "act3", AcceptedDropKeys = ["drop"], RequirementKeys = [], Modifiers = [] };

        var attempt = new AttemptSnapshotDto
        {
            Key = "main",
            RollScope = 0,
            BaselineTimeSeconds = 60,
            VarianceSeconds = 0,
            Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }]
        };

        var snapshot = new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 2, RequiredCount = 1, AllowedActivities = [rule1] },
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 4, RequiredCount = 1, AllowedActivities = [rule2] },
                        new TileSnapshotDto { Key = "t3", Name = "T3", Points = 3, RequiredCount = 1, AllowedActivities = [rule3] }
                    ]
                }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId1] = new ActivitySnapshotDto { Id = actId1, Key = "act1", Attempts = [attempt], GroupScalingBands = [], ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false } },
                [actId2] = new ActivitySnapshotDto { Id = actId2, Key = "act2", Attempts = [attempt], GroupScalingBands = [], ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false } },
                [actId3] = new ActivitySnapshotDto { Id = actId3, Key = "act3", Attempts = [attempt], GroupScalingBands = [], ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false } }
            },
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.Greedy,
            Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
        };

        var tileKeyToActivityId = new Dictionary<string, Guid>(StringComparer.Ordinal) { ["t1"] = actId1, ["t2"] = actId2, ["t3"] = actId3 };
        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 0, ["t2"] = 0, ["t3"] = 0 };
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 2, ["t2"] = 4, ["t3"] = 3 };
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal)
        {
            ["t1"] = [rule1],
            ["t2"] = [rule2],
            ["t3"] = [rule3]
        };

        var context = new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 1, ["t2"] = 1, ["t3"] = 1 },
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };

        return (context, tileKeyToActivityId);
    }

    private static string BuildSnapshotWithGreedyTeam()
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
            EventName = "Greedy Test",
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
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }
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
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Greedy Team",
                    StrategyKey = StrategyCatalog.Greedy,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
