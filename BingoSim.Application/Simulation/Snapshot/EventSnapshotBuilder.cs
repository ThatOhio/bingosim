using System.Text.Json;
using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Builds EventSnapshotDto from Event + Teams + resolved Activities and Players. Serializes to JSON for storage.
/// </summary>
public class EventSnapshotBuilder(
    IEventRepository eventRepo,
    ITeamRepository teamRepo,
    IActivityDefinitionRepository activityRepo,
    IPlayerProfileRepository playerRepo)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public async Task<string> BuildSnapshotJsonAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var evt = await eventRepo.GetByIdAsync(eventId, cancellationToken)
            ?? throw new InvalidOperationException($"Event {eventId} not found.");

        var teams = await teamRepo.GetByEventIdAsync(eventId, cancellationToken);
        if (teams.Count == 0)
            throw new InvalidOperationException($"No teams found for event {eventId}.");

        var activityIds = evt.Rows
            .SelectMany(r => r.Tiles.SelectMany(t => t.AllowedActivities.Select(a => a.ActivityDefinitionId)))
            .Distinct()
            .ToList();
        var activities = new Dictionary<Guid, ActivityDefinition>();
        foreach (var id in activityIds)
        {
            var activity = await activityRepo.GetByIdAsync(id, cancellationToken);
            if (activity is not null)
                activities[id] = activity;
        }

        var playerIds = teams.SelectMany(t => t.TeamPlayers.Select(tp => tp.PlayerProfileId)).Distinct().ToList();
        var players = new Dictionary<Guid, PlayerProfile>();
        foreach (var id in playerIds)
        {
            var profile = await playerRepo.GetByIdAsync(id, cancellationToken);
            if (profile is not null)
                players[id] = profile;
        }

        var rows = evt.Rows.OrderBy(r => r.Index).Select(r => new RowSnapshotDto
        {
            Index = r.Index,
            Tiles = r.Tiles.Select(t => new TileSnapshotDto
            {
                Key = t.Key,
                Name = t.Name,
                Points = t.Points,
                RequiredCount = t.RequiredCount,
                AllowedActivities = t.AllowedActivities.Select(rule => new TileActivityRuleSnapshotDto
                {
                    ActivityDefinitionId = rule.ActivityDefinitionId,
                    ActivityKey = rule.ActivityKey,
                    AcceptedDropKeys = rule.AcceptedDropKeys.ToList(),
                    RequirementKeys = rule.Requirements.Select(req => req.Key).ToList()
                }).ToList()
            }).ToList()
        }).ToList();

        var activitiesById = new Dictionary<Guid, ActivitySnapshotDto>();
        foreach (var (id, activity) in activities)
        {
            activitiesById[id] = new ActivitySnapshotDto
            {
                Id = activity.Id,
                Key = activity.Key,
                Attempts = activity.Attempts.Select(a => new AttemptSnapshotDto
                {
                    Key = a.Key,
                    RollScope = (int)a.RollScope,
                    BaselineTimeSeconds = a.TimeModel.BaselineTimeSeconds,
                    VarianceSeconds = a.TimeModel.VarianceSeconds,
                    Outcomes = a.Outcomes.Select(o => new OutcomeSnapshotDto
                    {
                        WeightNumerator = o.WeightNumerator,
                        WeightDenominator = o.WeightDenominator,
                        Grants = o.Grants.Select(g => new ProgressGrantSnapshotDto { DropKey = g.DropKey, Units = g.Units }).ToList()
                    }).ToList()
                }).ToList(),
                GroupScalingBands = activity.GroupScalingBands.Select(b => new GroupSizeBandSnapshotDto
                {
                    MinSize = b.MinSize,
                    MaxSize = b.MaxSize,
                    TimeMultiplier = b.TimeMultiplier,
                    ProbabilityMultiplier = b.ProbabilityMultiplier
                }).ToList()
            };
        }

        var teamDtos = new List<TeamSnapshotDto>();
        foreach (var team in teams)
        {
            var strategy = team.StrategyConfig;
            var playerDtos = new List<PlayerSnapshotDto>();
            foreach (var tp in team.TeamPlayers)
            {
                if (players.TryGetValue(tp.PlayerProfileId, out var profile))
                {
                    playerDtos.Add(new PlayerSnapshotDto
                    {
                        PlayerId = profile.Id,
                        Name = profile.Name,
                        SkillTimeMultiplier = profile.SkillTimeMultiplier,
                        CapabilityKeys = profile.Capabilities.Select(c => c.Key).ToList()
                    });
                }
            }

            teamDtos.Add(new TeamSnapshotDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                StrategyKey = strategy?.StrategyKey ?? "RowRush",
                ParamsJson = strategy?.ParamsJson,
                Players = playerDtos
            });
        }

        var dto = new EventSnapshotDto
        {
            EventName = evt.Name,
            DurationSeconds = (int)evt.Duration.TotalSeconds,
            UnlockPointsRequiredPerRow = evt.UnlockPointsRequiredPerRow,
            Rows = rows,
            ActivitiesById = activitiesById,
            Teams = teamDtos
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static EventSnapshotDto? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<EventSnapshotDto>(json, JsonOptions);
    }
}
