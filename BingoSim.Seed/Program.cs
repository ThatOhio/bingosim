using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BingoSim.Seed;

public static class Program
{
    private const string FullResetConfirmationWord = "yes";

    public static async Task<int> Main(string[] args)
    {
        var reset = args.Contains("--reset", StringComparer.OrdinalIgnoreCase);
        var fullReset = args.Contains("--full-reset", StringComparer.OrdinalIgnoreCase);
        var confirm = args.Contains("--confirm", StringComparer.OrdinalIgnoreCase);
        var perf = args.Contains("--perf", StringComparer.OrdinalIgnoreCase);
        var perfRegression = args.Contains("--perf-regression", StringComparer.OrdinalIgnoreCase);

        if (perfRegression)
        {
            return RunPerfRegressionGuard(GetArgInt(args, "--runs", 1_000), GetArgInt(args, "--min-runs-per-sec", 50));
        }

        var perfRuns = GetArgInt(args, "--runs", 10_000);
        var perfEvent = GetArg(args, "--event") ?? "Winter Bingo 2025";
        var perfSeed = GetArg(args, "--seed") ?? "perf-baseline-2025";
        var maxDurationSeconds = GetArgInt(args, "--max-duration", 0);

        PerfRecorder? perfRecorder = perf ? new PerfRecorder() : null;
        var host = Host.CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureLogging(logging =>
            {
                if (perf)
                    logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);
                if (perfRecorder is not null)
                    services.AddSingleton<IPerfRecorder>(perfRecorder);
            })
            .Build();

        // Ensure database is migrated
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        if (perf)
        {
            return await RunPerfScenarioAsync(host, perfRuns, perfEvent, perfSeed, maxDurationSeconds);
        }

        if (fullReset)
        {
            if (!confirm && !await PromptConfirmFullResetAsync())
            {
                Console.WriteLine("Full reset cancelled.");
                return 1;
            }

            using (var scope = host.Services.CreateScope())
            {
                var fullResetService = scope.ServiceProvider.GetRequiredService<IFullDatabaseResetService>();
                await fullResetService.ResetAllDataAsync();
            }

            Console.WriteLine("Full database reset: done.");
            return 0;
        }

        using (var scope = host.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDevSeedService>();

            if (reset)
            {
                Console.WriteLine("Dev seed: reset and reseed...");
                await seeder.ResetAndSeedAsync();
            }
            else
            {
                Console.WriteLine("Dev seed: idempotent seed...");
                await seeder.SeedAsync();
            }
        }

        Console.WriteLine("Dev seed: done.");
        return 0;
    }

    /// <summary>
    /// Prompts the user to type 'yes' to confirm. Returns true only if the exact word is entered.
    /// </summary>
    private static async Task<bool> PromptConfirmFullResetAsync()
    {
        Console.WriteLine();
        Console.WriteLine("*** WARNING: This will PERMANENTLY delete ALL data in the database. ***");
        Console.WriteLine("    (Players, Activities, Events, Teams, Simulation batches, results, etc.)");
        Console.WriteLine();
        Console.Write($"Type '{FullResetConfirmationWord}' to confirm: ");

        var line = await Console.In.ReadLineAsync();
        return string.Equals(line?.Trim(), FullResetConfirmationWord, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> RunPerfScenarioAsync(
        IHost host,
        int runCount,
        string eventName,
        string seed,
        int maxDurationSeconds)
    {
        using var scope = host.Services.CreateScope();
        var eventRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventRepository>();
        var batchService = scope.ServiceProvider.GetRequiredService<ISimulationBatchService>();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ISimulationRunExecutor>();
        var perfRecorder = scope.ServiceProvider.GetService<IPerfRecorder>() as PerfRecorder;

        var evt = await eventRepo.GetByNameAsync(eventName);
        if (evt is null)
        {
            Console.WriteLine($"Event '{eventName}' not found. Run dev seed first: dotnet run --project BingoSim.Seed");
            return 1;
        }

        Console.WriteLine($"Perf scenario: {runCount} runs, event '{eventName}', seed '{seed}'");
        if (maxDurationSeconds > 0)
            Console.WriteLine($"Max duration: {maxDurationSeconds}s (will stop and report partial results if exceeded)");

        var batch = await batchService.StartBatchAsync(new BingoSim.Application.DTOs.StartSimulationBatchRequest
        {
            EventId = evt.Id,
            RunCount = runCount,
            Seed = seed,
            ExecutionMode = ExecutionMode.Local,
            Name = $"Perf-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
        });

        if (batch.Status == BingoSim.Core.Enums.BatchStatus.Error)
        {
            Console.WriteLine($"Batch failed: {batch.ErrorMessage}");
            return 1;
        }

        var runs = await runRepo.GetByBatchIdAsync(batch.Id);
        if (runs.Count == 0)
        {
            Console.WriteLine("No runs created.");
            return 1;
        }

        using var cts = maxDurationSeconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(maxDurationSeconds))
            : new CancellationTokenSource();
        var ct = cts.Token;

        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var completed = 0;
        for (var i = 0; i < runs.Count && !ct.IsCancellationRequested; i++)
        {
            try
            {
                await executor.ExecuteAsync(runs[i].Id, ct);
                completed++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        totalSw.Stop();

        var elapsedSec = totalSw.Elapsed.TotalSeconds;
        var runsPerSec = elapsedSec > 0 ? completed / elapsedSec : 0;
        var timedOut = maxDurationSeconds > 0 && totalSw.Elapsed.TotalSeconds >= maxDurationSeconds - 0.1;

        Console.WriteLine();
        Console.WriteLine("=== Perf Summary ===");
        Console.WriteLine($"Runs completed: {completed} / {runs.Count}");
        Console.WriteLine($"Elapsed: {elapsedSec:F1}s");
        Console.WriteLine($"Throughput: {runsPerSec:F1} runs/sec");
        if (timedOut)
            Console.WriteLine("[TIMED OUT - max-duration reached]");
        Console.WriteLine();

        if (perfRecorder is not null)
        {
            var totals = perfRecorder.GetTotals();
            Console.WriteLine("Phase totals (ms total, count):");
            foreach (var (phase, (totalMs, count)) in totals.OrderBy(x => x.Key))
                Console.WriteLine($"  {phase}: {totalMs}ms total, {count} invocations");
        }

        return 0;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int GetArgInt(string[] args, string name, int defaultValue)
    {
        var s = GetArg(args, name);
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    /// <summary>
    /// Lightweight engine-only regression guard. No DB. Exits 1 if runs/sec below threshold.
    /// Run manually or in a perf pipeline; not in normal CI.
    /// </summary>
    private static int RunPerfRegressionGuard(int runCount, int minRunsPerSec)
    {
        var snapshotJson = PerfScenarioSnapshot.BuildJson();
        var allocatorFactory = new ProgressAllocatorFactory();
        var runner = new SimulationRunner(allocatorFactory);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < runCount; i++)
        {
            var runSeed = SeedDerivation.DeriveRunSeedString("perf-regression-guard", i);
            _ = runner.Execute(snapshotJson, runSeed, CancellationToken.None);
        }
        sw.Stop();

        var elapsedSec = sw.Elapsed.TotalSeconds;
        var runsPerSec = elapsedSec > 0 ? runCount / elapsedSec : 0;

        Console.WriteLine($"Perf regression guard: {runCount} runs in {elapsedSec:F1}s = {runsPerSec:F1} runs/sec (min: {minRunsPerSec})");

        if (runsPerSec < minRunsPerSec)
        {
            Console.WriteLine($"FAIL: Throughput {runsPerSec:F1} runs/sec is below threshold {minRunsPerSec}. Investigate performance regression.");
            return 1;
        }

        Console.WriteLine("PASS");
        return 0;
    }
}
