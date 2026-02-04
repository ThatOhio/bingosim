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
/// Verifies RowUnlockingStrategy is registered and can be invoked without exceptions.
/// </summary>
public class RowUnlockingStrategyRegistrationTests
{
    [Fact]
    public void StrategyCatalog_ContainsRowUnlocking()
    {
        StrategyCatalog.IsSupported(StrategyCatalog.RowUnlocking).Should().BeTrue();
        StrategyCatalog.GetSupportedKeys().Should().Contain(StrategyCatalog.RowUnlocking);
    }

    [Fact]
    public void Factory_GetStrategy_RowUnlocking_ReturnsRowUnlockingStrategy()
    {
        var factory = new TeamStrategyFactory();
        var strategy = factory.GetStrategy(StrategyCatalog.RowUnlocking);

        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<RowUnlockingStrategy>();
    }

    [Fact]
    public void SelectTargetTileForGrant_NoEligible_ReturnsNull()
    {
        var strategy = new RowUnlockingStrategy();
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
    public void SelectTargetTileForGrant_MultipleTilesOnFurthestRow_SelectsHighestPoint()
    {
        var strategy = new RowUnlockingStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 1 },
            TilePoints = new Dictionary<string, int> { ["a"] = 4, ["b"] = 2, ["c"] = 4 },
            EligibleTileKeys = ["a", "b", "c"],
            EventSnapshot = BuildMinimalEventSnapshot()
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("c");
    }

    [Fact]
    public void SelectTargetTileForGrant_OnlyTilesOnEarlierRows_FallbackToHighestPoint()
    {
        var strategy = new RowUnlockingStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 },
            TilePoints = new Dictionary<string, int> { ["a"] = 1, ["b"] = 4 },
            EligibleTileKeys = ["a", "b"],
            EventSnapshot = BuildMinimalEventSnapshot()
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("b");
    }

    [Fact]
    public void SelectTargetTileForGrant_TieBreakByTileKey()
    {
        var strategy = new RowUnlockingStrategy();
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["x"] = 1, ["a"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TilePoints = new Dictionary<string, int> { ["x"] = 4, ["a"] = 4 },
            EligibleTileKeys = ["x", "a"],
            EventSnapshot = BuildMinimalEventSnapshot()
        };
        strategy.SelectTargetTileForGrant(context).Should().Be("a");
    }

    [Fact]
    public void RowUnlockingStrategy_SelectTaskForPlayer_DoesNotThrow()
    {
        var factory = new TeamStrategyFactory();
        var strategy = (RowUnlockingStrategy)factory.GetStrategy(StrategyCatalog.RowUnlocking);

        var context = BuildMinimalTaskSelectionContext();

        var act = () => strategy.SelectTaskForPlayer(context);
        act.Should().NotThrow();
        strategy.SelectTaskForPlayer(context).Should().BeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_PlayerCanWorkOnTile_ReturnsTask()
    {
        var strategy = new RowUnlockingStrategy();
        var context = BuildTaskSelectionContextWithWorkableTile();

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
        result.Value.rule.Should().NotBeNull();
    }

    [Fact]
    public void SelectTaskForPlayer_AllTilesCompleted_ReturnsNull()
    {
        var strategy = new RowUnlockingStrategy();
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
    public void SelectTaskForPlayer_FallbackToAllRows_PrefersFurthestRowWhenPointsTie()
    {
        // When FindTaskInRow(furthestRow) returns null, we fall back to FindTaskInAllRows.
        // FindTaskInAllRows must prefer the furthest row when points tie (ThenByDescending).
        var (strategy, context) = BuildContext_FallbackPrefersFurthestRow();
        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        var selectedTile = context.EventSnapshot.Rows
            .SelectMany(r => r.Tiles)
            .FirstOrDefault(t => t.AllowedActivities.Any(a => a.ActivityDefinitionId == result!.Value.activityId));
        selectedTile.Should().NotBeNull();
        selectedTile!.Key.Should().Be("r1_tile", "When falling back to all rows with same points, furthest row (1) should be preferred over row 0");
    }

    private static (RowUnlockingStrategy strategy, TaskSelectionContext context) BuildContext_FallbackPrefersFurthestRow()
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
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "r0_tile", Name = "R0", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "r1_tile", Name = "R1", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 2, Tiles = [new TileSnapshotDto { Key = "r2_tile", Name = "R2", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }] }
            ],
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
            Teams = []
        };
        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal) { ["r0_tile"] = 0, ["r1_tile"] = 1, ["r2_tile"] = 2 };
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal) { ["r0_tile"] = 3, ["r1_tile"] = 3, ["r2_tile"] = 3 };
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal)
        {
            ["r0_tile"] = [rule], ["r1_tile"] = [rule], ["r2_tile"] = [rule]
        };
        var tileRequiredCount = new Dictionary<string, int>(StringComparer.Ordinal) { ["r0_tile"] = 1, ["r1_tile"] = 1, ["r2_tile"] = 1 };
        var completedTiles = new HashSet<string>(StringComparer.Ordinal) { "r2_tile" };
        var context = new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = new HashSet<string>(StringComparer.Ordinal),
            EventSnapshot = snapshot,
            TeamSnapshot = new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "Test", StrategyKey = StrategyCatalog.RowUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }] },
            UnlockedRowIndices = new HashSet<int> { 0, 1, 2 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = tileRequiredCount,
            CompletedTiles = completedTiles,
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
        return (new RowUnlockingStrategy(), context);
    }

    [Fact]
    public void Simulation_WithRowUnlockingTeam_DoesNotCrash()
    {
        var snapshotJson = BuildSnapshotWithRowUnlockingTeam();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var act = () => runner.Execute(snapshotJson, "row-unlocking-shell-test_0", CancellationToken.None);
        act.Should().NotThrow();
        var results = runner.Execute(snapshotJson, "row-unlocking-shell-test_0", CancellationToken.None);
        results.Should().NotBeEmpty();
        var rowUnlockingTeam = results.First(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
        rowUnlockingTeam.Should().NotBeNull();
    }

    private static string BuildSnapshotWithRowUnlockingTeam()
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
            EventName = "RowUnlocking Test",
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
                    TeamName = "RowUnlocking Team",
                    StrategyKey = StrategyCatalog.RowUnlocking,
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
            StrategyKey = StrategyCatalog.RowUnlocking,
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
            StrategyKey = StrategyCatalog.RowUnlocking,
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
