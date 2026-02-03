using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Infrastructure.Hosting;

/// <summary>
/// Periodically flushes the BufferedRunResultPersister so runs that complete near the end of a batch
/// (when buffer count &lt; BatchSize and no further AddAsync calls occur) get persisted.
/// Without this, the last N runs of a batch can remain in buffer indefinitely.
/// </summary>
public class BufferedPersisterFlushHostedService(
    IBufferedRunResultPersister persister,
    IOptions<SimulationPersistenceOptions> options,
    ILogger<BufferedPersisterFlushHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMs = options.Value.FlushIntervalMs;
        if (intervalMs <= 0)
        {
            logger.LogDebug("BufferedPersisterFlushHostedService disabled (FlushIntervalMs <= 0)");
            return;
        }

        var interval = TimeSpan.FromMilliseconds(intervalMs);
        logger.LogInformation("Buffered persister flush timer started (interval {IntervalMs}ms)", intervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await persister.FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Buffered persister flush failed");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
