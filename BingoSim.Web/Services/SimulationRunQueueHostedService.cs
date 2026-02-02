using BingoSim.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Web.Services;

/// <summary>
/// Hosted service that dequeues simulation run ids and executes them with configurable concurrency.
/// </summary>
public class SimulationRunQueueHostedService(
    IServiceProvider serviceProvider,
    ISimulationRunQueue queue,
    IOptions<LocalSimulationOptions> options,
    ILogger<SimulationRunQueueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxConcurrent = options.Value.MaxConcurrentRuns;
        var delayMs = options.Value.SimulationDelayMs;
        var semaphore = new SemaphoreSlim(maxConcurrent);

        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = await queue.DequeueAsync(stoppingToken);
            if (runId is null)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            await semaphore.WaitAsync(stoppingToken);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0)
                        await Task.Delay(delayMs, stoppingToken);
                    using var scope = serviceProvider.CreateScope();
                    var executor = scope.ServiceProvider.GetRequiredService<ISimulationRunExecutor>();
                    await executor.ExecuteAsync(runId.Value, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Run {RunId} failed in hosted service", runId);
                }
                finally
                {
                    semaphore.Release();
                }
            }, stoppingToken);
        }
    }
}

/// <summary>
/// Options for local simulation execution (concurrency and optional delay for test throttle).
/// </summary>
public class LocalSimulationOptions
{
    public const string SectionName = "LocalSimulation";

    public int MaxConcurrentRuns { get; set; } = 4;
    public int SimulationDelayMs { get; set; } = 0;
}
