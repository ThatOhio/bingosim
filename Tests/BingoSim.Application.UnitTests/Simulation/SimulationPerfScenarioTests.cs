using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Application.Simulation.Snapshot;
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
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

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
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

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
    /// Regression guard: Execute(string) and Execute(EventSnapshotDto) must produce identical results for the same seed.
    /// Validates that snapshot caching (Proposal 1) does not change semantics.
    /// </summary>
    [Fact]
    public void Execute_JsonVsDtoPath_SameSeed_ProducesIdenticalResults()
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var snapshot = EventSnapshotBuilder.Deserialize(snapshotJson);
        snapshot.Should().NotBeNull();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

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
