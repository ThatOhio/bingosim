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
/// Verifies ComboUnlockingStrategy is registered and behaves correctly in both phases.
/// </summary>
public class ComboUnlockingStrategyTests
{
    [Fact]
    public void StrategyCatalog_ContainsComboUnlocking()
    {
        StrategyCatalog.IsSupported(StrategyCatalog.ComboUnlocking).Should().BeTrue();
        StrategyCatalog.GetSupportedKeys().Should().Contain(StrategyCatalog.ComboUnlocking);
    }

    [Fact]
    public void Factory_GetStrategy_ComboUnlocking_ReturnsComboUnlockingStrategy()
    {
        var factory = new TeamStrategyFactory();
        var strategy = factory.GetStrategy(StrategyCatalog.ComboUnlocking);

        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<ComboUnlockingStrategy>();
    }

    [Fact]
    public void SelectTargetTileForGrant_NoEligible_ReturnsNull()
    {
        var strategy = new ComboUnlockingStrategy();
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
    public void SelectTargetTileForGrant_Phase1_MultipleTilesOnFurthestRow_SelectsHighestPoint()
    {
        var strategy = new ComboUnlockingStrategy();
        var snapshot = BuildEventSnapshotWithThreeRows();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 1 },
            TilePoints = new Dictionary<string, int> { ["a"] = 4, ["b"] = 2, ["c"] = 4 },
            EligibleTileKeys = ["a", "b", "c"],
            EventSnapshot = snapshot
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("c");
    }

    [Fact]
    public void SelectTargetTileForGrant_Phase2_AllRowsUnlocked_SelectsHighestPoint()
    {
        var strategy = new ComboUnlockingStrategy();
        var snapshot = BuildEventSnapshotWithTwoRows();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 1 },
            TilePoints = new Dictionary<string, int> { ["a"] = 2, ["b"] = 2, ["c"] = 4 },
            EligibleTileKeys = ["a", "b", "c"],
            EventSnapshot = snapshot
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("c");
    }

    [Fact]
    public void SelectTaskForPlayer_NoUnlockedRows_ReturnsNull()
    {
        var strategy = new ComboUnlockingStrategy();
        var context = BuildMinimalTaskSelectionContext();
        var contextWithNoUnlocked = new TaskSelectionContext
        {
            PlayerIndex = context.PlayerIndex,
            PlayerCapabilities = context.PlayerCapabilities,
            EventSnapshot = context.EventSnapshot,
            TeamSnapshot = context.TeamSnapshot,
            UnlockedRowIndices = new HashSet<int>(),
            TileProgress = context.TileProgress,
            TileRequiredCount = context.TileRequiredCount,
            CompletedTiles = context.CompletedTiles,
            TileRowIndex = context.TileRowIndex,
            TilePoints = context.TilePoints,
            TileToRules = context.TileToRules
        };

        var result = strategy.SelectTaskForPlayer(contextWithNoUnlocked);

        result.Should().BeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_Phase1_PlayerCanWorkOnTile_ReturnsTask()
    {
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
        result.Value.rule.Should().NotBeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_Phase2_AllRowsUnlocked_PlayerCanWorkOnTile_ReturnsTask()
    {
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskSelectionContextAllRowsUnlocked();

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
        result.Value.rule.Should().NotBeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_AllTilesCompleted_ReturnsNull()
    {
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();
        var completedTiles = new HashSet<string>(StringComparer.Ordinal) { "t1" };
        var contextWithCompleted = new TaskSelectionContext
        {
            PlayerIndex = context.PlayerIndex,
            PlayerCapabilities = context.PlayerCapabilities,
            EventSnapshot = context.EventSnapshot,
            TeamSnapshot = context.TeamSnapshot,
            UnlockedRowIndices = context.UnlockedRowIndices,
            TileProgress = context.TileProgress,
            TileRequiredCount = context.TileRequiredCount,
            CompletedTiles = completedTiles,
            TileRowIndex = context.TileRowIndex,
            TilePoints = context.TilePoints,
            TileToRules = context.TileToRules
        };

        var result = strategy.SelectTaskForPlayer(contextWithCompleted);

        result.Should().BeNull();
    }

    [Fact]
    public void InvalidateCacheForRow_ClearsCache()
    {
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();

        strategy.SelectTaskForPlayer(context);

        strategy.InvalidateCacheForRow(0);

        var result = strategy.SelectTaskForPlayer(context);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Simulation_WithComboUnlockingTeam_DoesNotCrash()
    {
        var snapshotJson = BuildSnapshotWithComboUnlockingTeam();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var act = () => runner.Execute(snapshotJson, "combo-unlocking-test_0", CancellationToken.None);
        act.Should().NotThrow();
        var results = runner.Execute(snapshotJson, "combo-unlocking-test_0", CancellationToken.None);
        results.Should().NotBeEmpty();
        var comboTeam = results.First(r => r.StrategyKey == StrategyCatalog.ComboUnlocking);
        comboTeam.Should().NotBeNull();
    }

    private static EventSnapshotDto BuildEventSnapshotWithThreeRows()
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

        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "a", Name = "A", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "b", Name = "B", Points = 2, RequiredCount = 1, AllowedActivities = [rule] }, new TileSnapshotDto { Key = "c", Name = "C", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 2, Tiles = [new TileSnapshotDto { Key = "d", Name = "D", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }] }
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
    }

    private static EventSnapshotDto BuildEventSnapshotWithTwoRows()
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

        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "a", Name = "A", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "b", Name = "B", Points = 2, RequiredCount = 1, AllowedActivities = [rule] }, new TileSnapshotDto { Key = "c", Name = "C", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] }
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
    }

    private static string BuildSnapshotWithComboUnlockingTeam()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
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
            EventName = "ComboUnlocking Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5)).ToString("o"),
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
                    TeamName = "ComboUnlocking Team",
                    StrategyKey = StrategyCatalog.ComboUnlocking,
                    Players =
                    [
                        new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }
                    ]
                }
            ]
        };

        return JsonSerializer.Serialize(dto);
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
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.ComboUnlocking,
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
            TileRequiredCount = new Dictionary<string, int> { ["t1"] = 1 },
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
    }

    private static TaskSelectionContext BuildTaskSelectionContextAllRowsUnlocked()
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
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t1", Name = "T1", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }
                    ]
                },
                new RowSnapshotDto
                {
                    Index = 1,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "t2", Name = "T2", Points = 2, RequiredCount = 1, AllowedActivities = [rule] }
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
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.ComboUnlocking,
            Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
        };

        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 0, ["t2"] = 1 };
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal) { ["t1"] = 1, ["t2"] = 2 };
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal) { ["t1"] = [rule], ["t2"] = [rule] };

        return new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int> { ["t1"] = 1, ["t2"] = 1 },
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
    }

    private static TaskSelectionContext BuildMinimalTaskSelectionContext()
    {
        var snapshot = new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows = [],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>(),
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.ComboUnlocking,
            Players = []
        };

        return new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(),
            CompletedTiles = new HashSet<string>(StringComparer.Ordinal),
            TileRowIndex = new Dictionary<string, int>(),
            TilePoints = new Dictionary<string, int>(),
            TileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal)
        };
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
}
