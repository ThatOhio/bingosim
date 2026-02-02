using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Application service for Team operations. Validates event and player existence.
/// </summary>
public class TeamService(
    ITeamRepository teamRepository,
    IEventRepository eventRepository,
    IPlayerProfileRepository playerProfileRepository,
    ILogger<TeamService> logger) : ITeamService
{
    public async Task<IReadOnlyList<TeamResponse>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var teams = await teamRepository.GetByEventIdAsync(eventId, cancellationToken);
        var eventEntity = await eventRepository.GetByIdAsync(eventId, cancellationToken);
        var eventName = eventEntity?.Name;

        return teams.Select(t => TeamMapper.ToResponse(t, eventName)).ToList();
    }

    public async Task<TeamResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(id, cancellationToken);
        if (team is null)
            return null;

        var eventEntity = await eventRepository.GetByIdAsync(team.EventId, cancellationToken);
        return TeamMapper.ToResponse(team, eventEntity?.Name);
    }

    public async Task<Guid> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var eventExists = await eventRepository.ExistsAsync(request.EventId, cancellationToken);
        if (!eventExists)
            throw new EventNotFoundException(request.EventId);

        await ValidatePlayerProfileIdsAsync(request.PlayerProfileIds, cancellationToken);

        var (team, strategy, teamPlayers) = TeamMapper.ToEntity(request);
        await teamRepository.AddAsync(team, strategy, teamPlayers, cancellationToken);

        logger.LogInformation(
            "Created Team {TeamId} '{TeamName}' for Event {EventId} with {PlayerCount} player(s), strategy {StrategyKey}",
            team.Id,
            team.Name,
            team.EventId,
            teamPlayers.Count,
            strategy.StrategyKey);

        return team.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var team = await teamRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new TeamNotFoundException(id);

        await ValidatePlayerProfileIdsAsync(request.PlayerProfileIds, cancellationToken);

        var strategy = team.StrategyConfig
            ?? throw new InvalidOperationException($"Team {id} has no StrategyConfig.");

        TeamMapper.ApplyToEntity(team, strategy, request, out var teamPlayers);
        await teamRepository.UpdateAsync(team, strategy, teamPlayers, cancellationToken);

        logger.LogInformation(
            "Updated Team {TeamId} '{TeamName}', {PlayerCount} player(s), strategy {StrategyKey}",
            team.Id,
            team.Name,
            teamPlayers.Count,
            strategy.StrategyKey);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await teamRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            throw new TeamNotFoundException(id);

        await teamRepository.DeleteAsync(id, cancellationToken);

        logger.LogInformation("Deleted Team {TeamId}", id);
    }

    public async Task DeleteAllByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var eventExists = await eventRepository.ExistsAsync(eventId, cancellationToken);
        if (!eventExists)
            throw new EventNotFoundException(eventId);

        await teamRepository.DeleteAllByEventIdAsync(eventId, cancellationToken);

        logger.LogInformation("Deleted all teams for Event {EventId}", eventId);
    }

    private async Task ValidatePlayerProfileIdsAsync(IEnumerable<Guid> playerIds, CancellationToken cancellationToken)
    {
        var ids = playerIds.Where(id => id != default).Distinct().ToList();
        if (ids.Count == 0)
            return;

        foreach (var id in ids)
        {
            var exists = await playerProfileRepository.ExistsAsync(id, cancellationToken);
            if (!exists)
                throw new PlayerProfileNotFoundException(id);
        }
    }
}
