using System.Collections.Concurrent;
using System.Text.Json;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Application.StrategyKeys;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Diagnostic test for Row 8 investigation. Runs a single simulation with full logging
/// to capture row unlocks, combination calculations, and task assignments.
/// Run with: dotnet test --filter "Row8DiagnosticInvestigationTests" --logger "console;verbosity=detailed"
/// </summary>
public class Row8DiagnosticInvestigationTests
{
    [Fact]
    public void SingleSimulation_WithDiagnosticLogging_CapturesRow8Behavior()
    {
        var messages = new ConcurrentQueue<string>();
        SimulationDiagnostics.EnableDiagnosticLogging = true;
        SimulationDiagnostics.LogAction = s => messages.Enqueue(s);

        try
        {
            var snapshotJson = BuildTwentyRowSnapshot();
            var factory = new TeamStrategyFactory();
            var runner = new SimulationRunner(factory);

            var results = runner.Execute(snapshotJson, "row8-investigation-seed", CancellationToken.None);

            results.Should().HaveCount(2);
            var rowUnlock = results.Single(r => r.StrategyKey == StrategyCatalog.RowUnlocking);
            var combo = results.Single(r => r.StrategyKey == StrategyCatalog.ComboUnlocking);

            messages.Enqueue("");
            messages.Enqueue("=== RESULTS ===");
            messages.Enqueue($"RowUnlocking: RowReached={rowUnlock.RowReached}, TilesCompleted={rowUnlock.TilesCompletedCount}, Points={rowUnlock.TotalPoints}");
            messages.Enqueue($"ComboUnlocking: RowReached={combo.RowReached}, TilesCompleted={combo.TilesCompletedCount}, Points={combo.TotalPoints}");

            var logOutput = string.Join(Environment.NewLine, messages);
            logOutput.Should().NotBeEmpty();

            // Write to file for investigation document
            var baseDir = AppContext.BaseDirectory;
            var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var logPath = Path.Combine(repoRoot, "Docs", "Strategies", "row-8-investigation-log.txt");
            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir))
                Directory.CreateDirectory(logDir);
            File.WriteAllText(logPath, logOutput);
        }
        finally
        {
            SimulationDiagnostics.EnableDiagnosticLogging = false;
            SimulationDiagnostics.LogAction = null;
        }
    }

    private static string BuildTwentyRowSnapshot()
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
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var rows = new List<RowSnapshotDto>();
        for (var r = 0; r < 20; r++)
        {
            rows.Add(new RowSnapshotDto
            {
                Index = r,
                Tiles =
                [
                    new TileSnapshotDto { Key = $"r{r}t1", Name = $"R{r}T1", Points = 2, RequiredCount = 1, AllowedActivities = [rule] },
                    new TileSnapshotDto { Key = $"r{r}t2", Name = $"R{r}T2", Points = 3, RequiredCount = 1, AllowedActivities = [rule] }
                ]
            });
        }

        var dto = new EventSnapshotDto
        {
            EventName = "Row 8 Investigation",
            DurationSeconds = 14400,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = new DateTimeOffset(2025, 2, 4, 9, 0, 0, TimeSpan.FromHours(-5)).ToString("o"),
            Rows = rows,
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
                new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "Alpha", StrategyKey = StrategyCatalog.RowUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] },
                new TeamSnapshotDto { TeamId = Guid.NewGuid(), TeamName = "Beta", StrategyKey = StrategyCatalog.ComboUnlocking, Players = [new PlayerSnapshotDto { PlayerId = Guid.NewGuid(), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] }
            ]
        };

        return JsonSerializer.Serialize(dto);
    }
}
