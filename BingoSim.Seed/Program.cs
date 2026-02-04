using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Snapshot;
using MassTransit;
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
        var recoverBatchId = GetArg(args, "--recover-batch");

        if (perfRegression)
        {
            return RunPerfRegressionGuard(GetArgInt(args, "--runs", 1_000), GetArgInt(args, "--min-runs-per-sec", 50));
        }

        var perfRuns = GetArgInt(args, "--runs", 10_000);
        var perfEvent = GetArg(args, "--event") ?? "Winter Bingo 2025";
        var perfSeed = GetArg(args, "--seed") ?? "perf-baseline-2025";
        var maxDurationSeconds = GetArgInt(args, "--max-duration", 0);
        var perfSnapshot = GetArg(args, "--perf-snapshot") ?? "devseed";
        var perfVerbose = args.Contains("--perf-verbose", StringComparer.OrdinalIgnoreCase);
        var perfDumpSnapshot = args.Contains("--perf-dump-snapshot", StringComparer.OrdinalIgnoreCase)
            ? (GetArg(args, "--perf-dump-snapshot") ?? "perf-snapshot.json")
            : null;

        var useSyntheticSnapshot = string.Equals(perfSnapshot, "synthetic", StringComparison.OrdinalIgnoreCase);

        PerfRecorder? perfRecorder = perf ? new PerfRecorder() : null;
        var perfOptions = perf
            ? new BingoSim.Infrastructure.Simulation.PerfScenarioOptions
            {
                UseSyntheticSnapshot = useSyntheticSnapshot,
                DumpSnapshotPath = perfDumpSnapshot,
                Verbose = perfVerbose
            }
            : null;

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
                if (perfOptions is not null)
                    services.AddSingleton<BingoSim.Application.Interfaces.IPerfScenarioOptions>(perfOptions);
                if (recoverBatchId is not null)
                {
                    var rabbitHost = context.Configuration["RabbitMQ:Host"] ?? "localhost";
                    var rabbitPort = int.TryParse(context.Configuration["RabbitMQ:Port"], out var p) ? p : 5672;
                    var rabbitUser = context.Configuration["RabbitMQ:Username"] ?? "guest";
                    var rabbitPass = context.Configuration["RabbitMQ:Password"] ?? "guest";
                    var rabbitUri = new Uri($"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}/");
                    services.AddMassTransit(x =>
                    {
                        x.UsingRabbitMq((_, cfg) => cfg.Host(rabbitUri));
                    });
                    services.AddScoped<BingoSim.Infrastructure.Simulation.MassTransitRunWorkPublisher>();
                }
            })
            .Build();

        // Ensure database is migrated
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        if (recoverBatchId is not null)
        {
            await host.StartAsync();
            try
            {
                return await RunRecoverBatchAsync(host, recoverBatchId);
            }
            finally
            {
                await host.StopAsync();
            }
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
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.IEventSnapshotRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ISimulationRunExecutor>();
        var bufferedPersister = scope.ServiceProvider.GetService<BingoSim.Application.Interfaces.IBufferedRunResultPersister>();
        var perfRecorder = scope.ServiceProvider.GetService<IPerfRecorder>() as PerfRecorder;
        var perfOpts = scope.ServiceProvider.GetService<BingoSim.Application.Interfaces.IPerfScenarioOptions>();

        var evt = await eventRepo.GetByNameAsync(eventName);
        if (evt is null)
        {
            Console.WriteLine($"Event '{eventName}' not found. Run dev seed first: dotnet run --project BingoSim.Seed");
            return 1;
        }

        Console.WriteLine($"Perf scenario: {runCount} runs, event '{eventName}', seed '{seed}'");
        if (maxDurationSeconds > 0)
            Console.WriteLine($"Max duration: {maxDurationSeconds}s (will stop and report partial results if exceeded)");
        if (perfOpts is { UseSyntheticSnapshot: true })
            Console.WriteLine("Snapshot: synthetic (PerfScenarioSnapshot)");
        if (perfOpts is { Verbose: true })
            Console.WriteLine("Verbose progress: enabled");
        if (perfOpts?.DumpSnapshotPath is { } dp)
            Console.WriteLine($"Dump snapshot: {dp}");

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

        var useSyntheticSnapshot = perfOpts?.UseSyntheticSnapshot ?? false;

        EventSnapshotDto snapshot;
        if (useSyntheticSnapshot)
        {
            var dto = EventSnapshotBuilder.Deserialize(PerfScenarioSnapshot.BuildJson());
            if (dto is null)
            {
                Console.WriteLine("Synthetic snapshot JSON invalid.");
                return 1;
            }
            SnapshotValidator.Validate(dto);
            snapshot = dto;
        }
        else
        {
            var snapshotEntity = await snapshotRepo.GetByBatchIdAsync(batch.Id);
            if (snapshotEntity is null)
            {
                Console.WriteLine("Snapshot not found for batch.");
                return 1;
            }
            var dto = EventSnapshotBuilder.Deserialize(snapshotEntity.EventConfigJson);
            if (dto is null)
            {
                Console.WriteLine("Snapshot JSON invalid.");
                return 1;
            }
            SnapshotValidator.Validate(dto);
            snapshot = dto;
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
                await executor.ExecuteAsync(runs[i], snapshot, ct);
                completed++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (BingoSim.Application.Simulation.SimulationNoProgressException ex)
            {
                Console.WriteLine();
                Console.WriteLine("=== Simulation No-Progress Guard Triggered ===");
                Console.WriteLine(ex.Message);
                Console.WriteLine($"  simTime={ex.SimTime}, nextSimTime={ex.NextSimTime}");
                Console.WriteLine($"  simTimeEt={ex.SimTimeEt}, nextSimTimeEt={ex.NextSimTimeEt}");
                Console.WriteLine($"  onlinePlayers={ex.OnlinePlayersCount}");
                if (!string.IsNullOrEmpty(ex.Diagnostics))
                    Console.WriteLine($"  diagnostics: {ex.Diagnostics}");
                return 1;
            }
        }
        totalSw.Stop();

        if (bufferedPersister is not null)
            await bufferedPersister.FlushAsync(ct);

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

        if (bufferedPersister is not null)
        {
            var stats = bufferedPersister.GetStats();
            Console.WriteLine("Buffered persist: {0} flushes, {1} rows inserted, {2} runs updated, {3} SaveChanges, {4}ms total",
                stats.FlushCount, stats.RowsInserted, stats.RowsUpdated, stats.SaveChangesCount, stats.ElapsedMsTotal);
        }

        return 0;
    }

    /// <summary>
    /// Resets runs stuck in Running to Pending and re-publishes them to RabbitMQ for retry.
    /// Use when BufferedRunResultPersister never flushed (e.g. last N runs of a batch).
    /// </summary>
    private static async Task<int> RunRecoverBatchAsync(IHost host, string batchIdStr)
    {
        if (!Guid.TryParse(batchIdStr, out var batchId))
        {
            Console.WriteLine($"Invalid batch ID: {batchIdStr}");
            return 1;
        }

        using var scope = host.Services.CreateScope();
        var runRepo = scope.ServiceProvider.GetRequiredService<BingoSim.Core.Interfaces.ISimulationRunRepository>();
        var runs = await runRepo.GetByBatchIdAsync(batchId);
        var stuck = runs.Where(r => r.Status == BingoSim.Core.Enums.RunStatus.Running).ToList();

        if (stuck.Count == 0)
        {
            Console.WriteLine($"No runs stuck in Running for batch {batchId}");
            return 0;
        }

        var reset = await runRepo.ResetStuckRunsToPendingAsync(batchId);
        Console.WriteLine($"Reset {reset} runs from Running to Pending");

        var publisher = scope.ServiceProvider.GetRequiredService<BingoSim.Infrastructure.Simulation.MassTransitRunWorkPublisher>();
        var runIds = stuck.Select(r => r.Id).ToList();
        await publisher.PublishRunWorkBatchAsync(runIds);
        Console.WriteLine($"Re-published {stuck.Count} run IDs to RabbitMQ. Worker will pick them up.");
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
        var strategyFactory = new TeamStrategyFactory();
        var runner = new SimulationRunner(strategyFactory);

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
