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
/// Engine-only perf tests. No DB, no DI repositories. Excluded from normal CI via Category filter.
/// Run with: dotnet test --filter "Category=Perf"
/// Set BINGOSIM_PERF_OUTPUT=1 to print elapsed/throughput summary for the 10K run.
/// </summary>
[Trait("Category", "Perf")]
public class SimulationPerfScenarioTests
{
    private const int DefaultRunCount = 10_000;
    private const int DefaultTimeoutSeconds = 300;

    [Fact]
    public void EngineOnly_10000Runs_CompletesWithinReasonableTime()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var completed = 0;
        for (var i = 0; i < DefaultRunCount; i++)
        {
            var runSeed = SeedDerivation.DeriveRunSeedString("perf-baseline-2025", i);
            _ = runner.Execute(snapshotJson, runSeed, CancellationToken.None);
            completed++;
        }
        sw.Stop();

        completed.Should().Be(DefaultRunCount);
        var runsPerSec = sw.Elapsed.TotalSeconds > 0 ? completed / sw.Elapsed.TotalSeconds : 0;
        // Sanity: expect at least 50 runs/sec on typical hardware
        runsPerSec.Should().BeGreaterThan(50, "engine-only 10K runs should complete at reasonable throughput");

        if (IsPerfOutputEnabled())
        {
            WritePerfSummary(completed, DefaultRunCount, sw.Elapsed.TotalSeconds, runsPerSec);
        }
    }

    private static bool IsPerfOutputEnabled()
    {
        var value = Environment.GetEnvironmentVariable("BINGOSIM_PERF_OUTPUT");
        return string.Equals(value, "1", StringComparison.Ordinal) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePerfSummary(int completed, int total, double elapsedSeconds, double runsPerSec)
    {
        Console.WriteLine();
        Console.WriteLine("=== Engine-only Perf Summary ===");
        Console.WriteLine($"Runs completed: {completed} / {total}");
        Console.WriteLine($"Elapsed: {elapsedSeconds:F1}s");
        Console.WriteLine($"Throughput: {runsPerSec:F1} runs/sec");
        Console.WriteLine();
    }

    [Fact]
    public void EngineOnly_SameSeed_Deterministic()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var r1 = runner.Execute(snapshotJson, "perf-baseline-2025_0", CancellationToken.None);
        var r2 = runner.Execute(snapshotJson, "perf-baseline-2025_0", CancellationToken.None);

        r1.Should().HaveCount(r2.Count);
        for (var i = 0; i < r1.Count; i++)
        {
            r1[i].TotalPoints.Should().Be(r2[i].TotalPoints);
            r1[i].RowUnlockTimesJson.Should().Be(r2[i].RowUnlockTimesJson);
        }
    }

    /// <summary>
    /// Compares simulation throughput: Greedy vs RowUnlocking. Greedy is expected to be faster (no combination cache).
    /// Run with BINGOSIM_PERF_OUTPUT=1 to see timing comparison.
    /// </summary>
    [Fact]
    public void StrategyComparison_GreedyVsRowUnlocking_Throughput()
    {
        const int runCount = 2000;
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var greedyJson = BuildPerfSnapshotWithStrategy(StrategyCatalog.Greedy);
        var rowUnlockJson = BuildPerfSnapshotWithStrategy(StrategyCatalog.RowUnlocking);

        var swGreedy = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < runCount; i++)
            _ = runner.Execute(greedyJson, SeedDerivation.DeriveRunSeedString("greedy-perf", i), CancellationToken.None);
        swGreedy.Stop();

        var swRowUnlock = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < runCount; i++)
            _ = runner.Execute(rowUnlockJson, SeedDerivation.DeriveRunSeedString("rowunlock-perf", i), CancellationToken.None);
        swRowUnlock.Stop();

        var greedyRps = runCount / swGreedy.Elapsed.TotalSeconds;
        var rowUnlockRps = runCount / swRowUnlock.Elapsed.TotalSeconds;

        if (IsPerfOutputEnabled())
        {
            Console.WriteLine();
            Console.WriteLine("=== Strategy Throughput Comparison ===");
            Console.WriteLine($"Greedy:       {swGreedy.Elapsed.TotalSeconds:F2}s for {runCount} runs = {greedyRps:F0} runs/sec");
            Console.WriteLine($"RowUnlocking: {swRowUnlock.Elapsed.TotalSeconds:F2}s for {runCount} runs = {rowUnlockRps:F0} runs/sec");
            Console.WriteLine();
        }

        greedyRps.Should().BeGreaterThan(30, "Greedy strategy should complete at reasonable throughput");
        rowUnlockRps.Should().BeGreaterThan(30, "RowUnlocking strategy should complete at reasonable throughput");
    }

    private static string BuildPerfSnapshotWithStrategy(string strategyKey)
    {
        var actId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var teamId = Guid.Parse("22222222-2222-2222-2222-222222222221");
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        var dto = new EventSnapshotDto
        {
            EventName = "Strategy Perf",
            DurationSeconds = 86400,
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
                    Attempts = [new AttemptSnapshotDto { Key = "main", RollScope = 0, BaselineTimeSeconds = 60, VarianceSeconds = 10, Outcomes = [new OutcomeSnapshotDto { WeightNumerator = 1, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }] }],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams = [new TeamSnapshotDto { TeamId = teamId, TeamName = "Team", StrategyKey = strategyKey, ParamsJson = null, Players = [new PlayerSnapshotDto { PlayerId = Guid.Parse("33333333-3333-3333-3333-333333333331"), Name = "P1", SkillTimeMultiplier = 1.0m, CapabilityKeys = [], Schedule = alwaysOnline }] }]
        };

        return JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// Regression guard: Execute(string) and Execute(EventSnapshotDto) must produce identical results for the same seed.
    /// Validates that snapshot caching (Proposal 1) does not change semantics.
    /// </summary>
    [Fact]
    public void Execute_JsonVsDtoPath_SameSeed_ProducesIdenticalResults()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var snapshot = EventSnapshotBuilder.Deserialize(snapshotJson);
        snapshot.Should().NotBeNull();
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

        var viaJson = runner.Execute(snapshotJson!, "perf-baseline-2025_42", CancellationToken.None);
        var viaDto = runner.Execute(snapshot!, "perf-baseline-2025_42", CancellationToken.None);

        viaJson.Should().HaveCount(viaDto.Count);
        for (var i = 0; i < viaJson.Count; i++)
        {
            viaJson[i].TotalPoints.Should().Be(viaDto[i].TotalPoints, "TotalPoints must match between JSON and DTO paths");
            viaJson[i].TilesCompletedCount.Should().Be(viaDto[i].TilesCompletedCount);
            viaJson[i].RowReached.Should().Be(viaDto[i].RowReached);
            viaJson[i].IsWinner.Should().Be(viaDto[i].IsWinner);
            viaJson[i].RowUnlockTimesJson.Should().Be(viaDto[i].RowUnlockTimesJson);
            viaJson[i].TileCompletionTimesJson.Should().Be(viaDto[i].TileCompletionTimesJson);
        }
    }

}
