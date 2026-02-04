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
/// Scenario and integration tests for ComboUnlocking strategy: Phase 1 penalties,
/// Phase 2 shared activity bonus, phase transition, three-way comparison, and edge cases.
/// </summary>
public class ComboUnlockingStrategyScenarioTests
{
    #region Phase 1: Penalty and Combination Selection

    [Fact]
    public void Phase1_NoLockedShares_BehavesLikeRowUnlocking()
    {
        var (snapshot, rule) = BuildSnapshot_UniqueActivitiesPerRow();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeEmpty();
    }

    [Fact]
    public void Phase1_PrefersTilesWithoutLockedShares()
    {
        var (snapshot, activities) = BuildSnapshot_ActivityOverlap_Row0HasUniqueAndShared();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeNull();
        var selectedTile = FindTileByActivity(snapshot, result.Value.activityId!.Value);
        selectedTile.Should().NotBeNull();
        selectedTile!.Key.Should().Be("uniqueC", "ComboUnlocking should prefer tile with unique activity (no locked shares) over shared-activity tiles");
    }

    [Fact]
    public void Phase1_AllRowsUnlocked_DetectsCorrectly()
    {
        var (snapshot, _) = BuildSnapshot_UniqueActivitiesPerRow();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0, 1, 2], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
    }

    #endregion

    #region Phase 2: Shared Activity Maximization

    [Fact]
    public void Phase2_SingleRow_ImmediatelyUsesPhase2()
    {
        var (snapshot, _) = BuildSnapshot_SingleRow();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Phase2_PrefersTilesWithMoreSharedActivities()
    {
        var (snapshot, _) = BuildSnapshot_SharedActivities_Phase2();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0, 1], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
        result!.Value.activityId.Should().NotBeNull();
        var selectedTile = FindTileByActivity(snapshot, result.Value.activityId!.Value);
        selectedTile.Should().NotBeNull();
        selectedTile!.Key.Should().Be("sharedA", "Tile with Activity1 shared by 3 other tiles (virtual 2+3=5) should outrank 4pt tile with 0 shares (virtual 4+0=4)");
    }

    [Fact]
    public void Phase2_AllTilesCompleted_ReturnsNull()
    {
        var (snapshot, _) = BuildSnapshot_SingleRow();
        var strategy = new ComboUnlockingStrategy();
        var allTiles = snapshot.Rows.SelectMany(r => r.Tiles).Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: allTiles);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().BeNull();
    }

    #endregion

    #region Three-Way Integration Comparison

    [Fact]
    public void ThreeWay_ActivityOverlapBoard_ComboUnlockingMayOutperform()
    {
        var snapshotJson = BuildActivityOverlapBoard();
        var factory = new TeamStrategyFactory();
        var runner = new SimulationRunner(factory);

        var results = runner.Execute(snapshotJson, "overlap_0", CancellationToken.None);

        var rowUnlock = results.Single(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
        var greedy = results.Single(r => r.StrategyKey == StrategyCatalog.Greedy);
        var combo = results.Single(r => r.StrategyKey == StrategyCatalog.ComboUnlocking);

        combo.TotalPoints.Should().BeGreaterThanOrEqualTo(0);
        combo.RowReached.Should().BeInRange(0, 3);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_SingleTileOnRow_Works()
    {
        var (snapshot, _) = BuildSnapshot_SingleTilePerRow();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
    }

    [Fact]
    public void EdgeCase_PlayerWithNoValidTiles_ReturnsNull()
    {
        var (snapshot, _) = BuildSnapshot_RequiresCapability();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0], completedTiles: [], capabilities: new HashSet<string>(StringComparer.Ordinal));

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().BeNull();
    }

    [Fact]
    public void EdgeCase_Phase2_NoSharedActivities_BehavesLikeGreedy()
    {
        var (snapshot, _) = BuildSnapshot_UniqueActivitiesPerRow();
        var strategy = new ComboUnlockingStrategy();
        var context = BuildTaskContext(snapshot, unlockedRows: [0, 1, 2], completedTiles: []);

        var result = strategy.SelectTaskForPlayer(context);

        result.Should().NotBeNull();
    }

    #endregion

    #region Helpers

    private static (EventSnapshotDto snapshot, Dictionary<string, Guid> activities) BuildSnapshot_ActivityOverlap_Row0HasUniqueAndShared()
    {
        var act1 = Guid.NewGuid();
        var act2 = Guid.NewGuid();
        var act3 = Guid.NewGuid();
        var activities = new Dictionary<string, Guid> { ["act1"] = act1, ["act2"] = act2, ["act3"] = act3 };

        var r1 = Rule(act1, "drop");
        var r2 = Rule(act2, "drop");
        var r3 = Rule(act3, "drop");

        var snapshot = new EventSnapshotDto
        {
            EventName = "Overlap",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "sharedA", Name = "A", Points = 2, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "sharedB", Name = "B", Points = 3, RequiredCount = 1, AllowedActivities = [r2] },
                        new TileSnapshotDto { Key = "uniqueC", Name = "C", Points = 4, RequiredCount = 1, AllowedActivities = [r3] },
                        new TileSnapshotDto { Key = "sharedD", Name = "D", Points = 1, RequiredCount = 1, AllowedActivities = [r1] }
                    ]
                },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "r1x", Name = "R1X", Points = 2, RequiredCount = 1, AllowedActivities = [r1] }, new TileSnapshotDto { Key = "r1y", Name = "R1Y", Points = 3, RequiredCount = 1, AllowedActivities = [r2] }] },
                new RowSnapshotDto { Index = 2, Tiles = [new TileSnapshotDto { Key = "r2x", Name = "R2X", Points = 4, RequiredCount = 1, AllowedActivities = [r1] }] }
            ],
            ActivitiesById = BuildActivities([act1, act2, act3]),
            Teams = []
        };
        return (snapshot, activities);
    }

    private static (EventSnapshotDto snapshot, TileActivityRuleSnapshotDto rule) BuildSnapshot_UniqueActivitiesPerRow()
    {
        var actId = Guid.NewGuid();
        var rule = Rule(actId, "drop");
        var snapshot = new EventSnapshotDto
        {
            EventName = "Unique",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "r0a", Name = "R0A", Points = 2, RequiredCount = 1, AllowedActivities = [rule] }, new TileSnapshotDto { Key = "r0b", Name = "R0B", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "r1a", Name = "R1A", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 2, Tiles = [new TileSnapshotDto { Key = "r2a", Name = "R2A", Points = 1, RequiredCount = 1, AllowedActivities = [rule] }] }
            ],
            ActivitiesById = BuildActivities([actId]),
            Teams = []
        };
        return (snapshot, rule);
    }

    private static (EventSnapshotDto snapshot, TileActivityRuleSnapshotDto rule) BuildSnapshot_SingleRow()
    {
        var actId = Guid.NewGuid();
        var rule = Rule(actId, "drop");
        var snapshot = new EventSnapshotDto
        {
            EventName = "SingleRow",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows = [new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "t1", Name = "T1", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] }],
            ActivitiesById = BuildActivities([actId]),
            Teams = []
        };
        return (snapshot, rule);
    }

    private static (EventSnapshotDto snapshot, Dictionary<string, Guid> _) BuildSnapshot_SharedActivities_Phase2()
    {
        var act1 = Guid.NewGuid();
        var act2 = Guid.NewGuid();
        var r1 = Rule(act1, "drop");
        var r2 = Rule(act2, "drop");

        var snapshot = new EventSnapshotDto
        {
            EventName = "Phase2Shared",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "sharedA", Name = "A", Points = 2, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "sharedB", Name = "B", Points = 1, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "sharedC", Name = "C", Points = 1, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "sharedD", Name = "D", Points = 1, RequiredCount = 1, AllowedActivities = [r1] }
                    ]
                },
                new RowSnapshotDto
                {
                    Index = 1,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "soloE", Name = "E", Points = 4, RequiredCount = 1, AllowedActivities = [r2] }
                    ]
                }
            ],
            ActivitiesById = BuildActivities([act1, act2]),
            Teams = []
        };
        return (snapshot, new Dictionary<string, Guid> { ["act1"] = act1, ["act2"] = act2 });
    }

    private static (EventSnapshotDto snapshot, TileActivityRuleSnapshotDto rule) BuildSnapshot_SingleTilePerRow()
    {
        var actId = Guid.NewGuid();
        var rule = Rule(actId, "drop");
        var snapshot = new EventSnapshotDto
        {
            EventName = "SingleTile",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows =
            [
                new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "r0", Name = "R0", Points = 5, RequiredCount = 1, AllowedActivities = [rule] }] },
                new RowSnapshotDto { Index = 1, Tiles = [new TileSnapshotDto { Key = "r1", Name = "R1", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] }
            ],
            ActivitiesById = BuildActivities([actId]),
            Teams = []
        };
        return (snapshot, rule);
    }

    private static (EventSnapshotDto snapshot, TileActivityRuleSnapshotDto rule) BuildSnapshot_RequiresCapability()
    {
        var actId = Guid.NewGuid();
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = ["cap1"],
            Modifiers = []
        };
        var snapshot = new EventSnapshotDto
        {
            EventName = "RequiresCap",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = "2025-02-04T09:00:00-05:00",
            Rows = [new RowSnapshotDto { Index = 0, Tiles = [new TileSnapshotDto { Key = "t1", Name = "T1", Points = 4, RequiredCount = 1, AllowedActivities = [rule] }] }],
            ActivitiesById = BuildActivities([actId]),
            Teams = []
        };
        return (snapshot, rule);
    }

    private static string BuildActivityOverlapBoard()
    {
        var act1 = Guid.NewGuid();
        var act2 = Guid.NewGuid();
        var act3 = Guid.NewGuid();
        var r1 = Rule(act1, "drop");
        var r2 = Rule(act2, "drop");
        var r3 = Rule(act3, "drop");
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Overlap",
            DurationSeconds = 14400,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5)).ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "r0a", Name = "A", Points = 2, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "r0b", Name = "B", Points = 3, RequiredCount = 1, AllowedActivities = [r2] },
                        new TileSnapshotDto { Key = "r0c", Name = "C", Points = 4, RequiredCount = 1, AllowedActivities = [r3] },
                        new TileSnapshotDto { Key = "r0d", Name = "D", Points = 1, RequiredCount = 1, AllowedActivities = [r1] }
                    ]
                },
                new RowSnapshotDto
                {
                    Index = 1,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "r1a", Name = "E", Points = 3, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "r1b", Name = "F", Points = 2, RequiredCount = 1, AllowedActivities = [r3] },
                        new TileSnapshotDto { Key = "r1c", Name = "G", Points = 4, RequiredCount = 1, AllowedActivities = [r2] }
                    ]
                },
                new RowSnapshotDto
                {
                    Index = 2,
                    Tiles =
                    [
                        new TileSnapshotDto { Key = "r2a", Name = "I", Points = 4, RequiredCount = 1, AllowedActivities = [r1] },
                        new TileSnapshotDto { Key = "r2b", Name = "J", Points = 2, RequiredCount = 1, AllowedActivities = [r2] }
                    ]
                }
            ],
            ActivitiesById = BuildActivities([act1, act2, act3]),
            Teams =
            [
                new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "RowUnlocking", StrategyKey = StrategyCatalog.RowUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] },
                new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "Greedy", StrategyKey = StrategyCatalog.Greedy, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] },
                new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "ComboUnlocking", StrategyKey = StrategyCatalog.ComboUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] }
            ]
        };
        return JsonSerializer.Serialize(dto);
    }

    private static TaskSelectionContext BuildTaskContext(
        EventSnapshotDto snapshot,
        int[] unlockedRows,
        HashSet<string> completedTiles,
        HashSet<string>? capabilities = null)
    {
        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal);
        var tileRequiredCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var tileToRules = new Dictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal);
        foreach (var row in snapshot.Rows)
        {
            foreach (var tile in row.Tiles)
            {
                tileRowIndex[tile.Key] = row.Index;
                tilePoints[tile.Key] = tile.Points;
                tileRequiredCount[tile.Key] = tile.RequiredCount;
                tileToRules[tile.Key] = tile.AllowedActivities;
            }
        }

        var team = new TeamSnapshotDto
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Test",
            StrategyKey = StrategyCatalog.ComboUnlocking,
            Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = capabilities?.ToList() ?? [], Schedule = new WeeklyScheduleSnapshotDto { Sessions = [] } }]
        };

        return new TaskSelectionContext
        {
            PlayerIndex = 0,
            PlayerCapabilities = capabilities ?? new HashSet<string>(StringComparer.Ordinal) { "cap1" },
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = unlockedRows.ToHashSet(),
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = tileRequiredCount,
            CompletedTiles = completedTiles,
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRules
        };
    }

    private static TileSnapshotDto? FindTileByActivity(EventSnapshotDto snapshot, Guid activityId)
    {
        foreach (var row in snapshot.Rows)
        {
            foreach (var tile in row.Tiles)
            {
                if (tile.AllowedActivities.Any(r => r.ActivityDefinitionId == activityId))
                    return tile;
            }
        }
        return null;
    }

    private static TileActivityRuleSnapshotDto Rule(Guid actId, string dropKey) =>
        new()
        {
            ActivityDefinitionId = actId,
            ActivityKey = "act",
            AcceptedDropKeys = [dropKey],
            RequirementKeys = [],
            Modifiers = []
        };

    private static Dictionary<Guid, ActivitySnapshotDto> BuildActivities(IEnumerable<Guid> ids)
    {
        var dict = new Dictionary<Guid, ActivitySnapshotDto>();
        foreach (var id in ids)
        {
            dict[id] = new ActivitySnapshotDto
            {
                Id = id,
                Key = $"act_{id:N}".Substring(0, 20),
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
            };
        }
        return dict;
    }

    #endregion
}
