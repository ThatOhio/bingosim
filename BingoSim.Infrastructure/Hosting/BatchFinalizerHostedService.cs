using BingoSim.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BingoSim.Infrastructure.Hosting;

/// <summary>
/// Periodically scans batches needing finalization (all runs terminal, batch still Pending/Running)
/// and computes aggregates. Safe under concurrency with multiple workers.
/// </summary>
public class BatchFinalizerHostedService(
    IServiceProvider serviceProvider,
    ILogger<BatchFinalizerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Batch finalizer started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var unfinalizedQuery = scope.ServiceProvider.GetRequiredService<IUnfinalizedBatchesQuery>();
                var finalizationService = scope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();

                var batches = await unfinalizedQuery.GetBatchIdsAsync(stoppingToken);
                foreach (var batchId in batches)
                {
                    try
                    {
                        await finalizationService.TryFinalizeAsync(batchId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to finalize batch {BatchId}", batchId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Batch finalizer scan failed");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }
}
