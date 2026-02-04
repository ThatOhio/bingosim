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
    public void RowUnlockingStrategy_SelectTargetTileForGrant_DoesNotThrow()
    {
        var factory = new TeamStrategyFactory();
        var strategy = (RowUnlockingStrategy)factory.GetStrategy(StrategyCatalog.RowUnlocking);

        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(),
            TileRowIndex = new Dictionary<string, int>(),
            TilePoints = new Dictionary<string, int>(),
            EligibleTileKeys = ["a", "b"]
        };

        var act = () => strategy.SelectTargetTileForGrant(context);
        act.Should().NotThrow();
        strategy.SelectTargetTileForGrant(context).Should().BeNull();
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
}
