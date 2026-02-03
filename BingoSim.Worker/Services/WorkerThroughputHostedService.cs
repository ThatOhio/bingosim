using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BingoSim.Worker.Services;

/// <summary>
/// Logs worker throughput every 10 seconds: runs completed and phase timings (claim, sim, persist).
/// </summary>
public class WorkerThroughputHostedService(
    IWorkerRunThroughputRecorder throughputRecorder,
    IServiceProvider serviceProvider,
    ILogger<WorkerThroughputHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan LogInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker throughput logger started (interval {Interval}s)", LogInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(LogInterval, stoppingToken);

            var runsInPeriod = throughputRecorder.TakeAndReset();
            if (runsInPeriod == 0)
                continue;

            var (phaseTotals, perfRecorderInstance) = GetPhaseTotals();
            logger.LogInformation(
                "Worker throughput: {RunsCompleted} runs in last {Interval}s. Phase totals (ms, count): {PhaseTotals}",
                runsInPeriod, LogInterval.TotalSeconds, FormatPhaseTotals(phaseTotals));
            perfRecorderInstance?.Reset();
        }
    }

    private (IReadOnlyDictionary<string, (long TotalMs, int Count)> Totals, PerfRecorder? Recorder) GetPhaseTotals()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var perfRecorder = scope.ServiceProvider.GetService<IPerfRecorder>() as PerfRecorder;
            var totals = perfRecorder?.GetTotals() ?? new Dictionary<string, (long TotalMs, int Count)>();
            return (totals, perfRecorder);
        }
        catch
        {
            return (new Dictionary<string, (long TotalMs, int Count)>(), null);
        }
    }

    private static string FormatPhaseTotals(IReadOnlyDictionary<string, (long TotalMs, int Count)> totals)
    {
        if (totals.Count == 0)
            return "none";
        return string.Join("; ", totals.Select(kv => $"{kv.Key}={kv.Value.TotalMs}ms({kv.Value.Count})"));
    }
}
