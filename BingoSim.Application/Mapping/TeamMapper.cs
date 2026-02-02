using BingoSim.Application.DTOs;
using BingoSim.Core.Entities;

namespace BingoSim.Application.Mapping;

/// <summary>
/// Manual mapping between Team entities and DTOs.
/// </summary>
public static class TeamMapper
{
    public static TeamResponse ToResponse(
        Team team,
        string? eventName = null)
    {
        var strategy = team.StrategyConfig;
        var playerIds = team.TeamPlayers
            .Select(tp => tp.PlayerProfileId)
            .ToList();

        return new TeamResponse(
            Id: team.Id,
            EventId: team.EventId,
            EventName: eventName,
            Name: team.Name,
            CreatedAt: team.CreatedAt,
            PlayerProfileIds: playerIds,
            StrategyKey: strategy?.StrategyKey ?? string.Empty,
            ParamsJson: strategy?.ParamsJson);
    }

    public static (Team team, StrategyConfig strategy, List<TeamPlayer> teamPlayers) ToEntity(CreateTeamRequest request)
    {
        var team = new Team(request.EventId, request.Name.Trim());
        var strategy = new StrategyConfig(team.Id, request.StrategyKey.Trim(), request.ParamsJson);
        var teamPlayers = request.PlayerProfileIds
            .Where(id => id != default)
            .Distinct()
            .Select(pid => new TeamPlayer(team.Id, pid))
            .ToList();

        return (team, strategy, teamPlayers);
    }

    public static void ApplyToEntity(
        Team team,
        StrategyConfig strategy,
        UpdateTeamRequest request,
        out List<TeamPlayer> teamPlayers)
    {
        team.UpdateName(request.Name.Trim());
        strategy.Update(request.StrategyKey.Trim(), request.ParamsJson);
        teamPlayers = request.PlayerProfileIds
            .Where(id => id != default)
            .Distinct()
            .Select(pid => new TeamPlayer(team.Id, pid))
            .ToList();
    }
}
