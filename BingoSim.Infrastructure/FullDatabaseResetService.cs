using BingoSim.Application.Interfaces;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BingoSim.Infrastructure;

/// <summary>
/// Deletes all application data in FK-safe order. For development/testing only.
/// </summary>
public class FullDatabaseResetService(AppDbContext context, ILogger<FullDatabaseResetService> logger) : IFullDatabaseResetService
{
    public async Task ResetAllDataAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Full database reset: deleting all application data");

        // Delete in dependency order (children first) so FK constraints are satisfied.
        await context.StrategyConfigs.ExecuteDeleteAsync(cancellationToken);
        await context.TeamPlayers.ExecuteDeleteAsync(cancellationToken);
        await context.TeamRunResults.ExecuteDeleteAsync(cancellationToken);
        await context.BatchTeamAggregates.ExecuteDeleteAsync(cancellationToken);
        await context.EventSnapshots.ExecuteDeleteAsync(cancellationToken);
        await context.SimulationRuns.ExecuteDeleteAsync(cancellationToken);
        await context.SimulationBatches.ExecuteDeleteAsync(cancellationToken);
        await context.Teams.ExecuteDeleteAsync(cancellationToken);
        await context.Events.ExecuteDeleteAsync(cancellationToken);
        await context.ActivityDefinitions.ExecuteDeleteAsync(cancellationToken);
        await context.PlayerProfiles.ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Full database reset: completed");
    }
}
