using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of ITeamRepository.
/// </summary>
public class TeamRepository(AppDbContext context) : ITeamRepository
{
    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await context.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Team team, StrategyConfig strategyConfig, IEnumerable<TeamPlayer> teamPlayers, CancellationToken cancellationToken = default)
    {
        await context.Teams.AddAsync(team, cancellationToken);
        await context.StrategyConfigs.AddAsync(strategyConfig, cancellationToken);
        await context.TeamPlayers.AddRangeAsync(teamPlayers, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Team team, StrategyConfig strategyConfig, IReadOnlyList<TeamPlayer> teamPlayers, CancellationToken cancellationToken = default)
    {
        var existingTeamPlayers = await context.TeamPlayers
            .Where(tp => tp.TeamId == team.Id)
            .ToListAsync(cancellationToken);

        context.TeamPlayers.RemoveRange(existingTeamPlayers);
        context.Teams.Update(team);
        context.StrategyConfigs.Update(strategyConfig);
        await context.TeamPlayers.AddRangeAsync(teamPlayers, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await context.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (team is not null)
        {
            context.TeamPlayers.RemoveRange(team.TeamPlayers);
            if (team.StrategyConfig is not null)
                context.StrategyConfigs.Remove(team.StrategyConfig);
            context.Teams.Remove(team);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAllByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var teams = await context.Teams
            .Include(t => t.StrategyConfig)
            .Include(t => t.TeamPlayers)
            .Where(t => t.EventId == eventId)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            context.TeamPlayers.RemoveRange(team.TeamPlayers);
            if (team.StrategyConfig is not null)
                context.StrategyConfigs.Remove(team.StrategyConfig);
        }

        context.Teams.RemoveRange(teams);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Teams.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
