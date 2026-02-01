using BingoSim.Application.DTOs;
using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;

namespace BingoSim.Application.Mapping;

/// <summary>
/// Manual mapping between Event entities and DTOs.
/// ActivityKey is persisted on the entity; ActivityName is resolved from lookup when building response.
/// </summary>
public static class EventMapper
{
    public static EventResponse ToResponse(
        Event entity,
        IReadOnlyDictionary<Guid, ActivityDefinition>? activityLookup = null)
    {
        return new EventResponse(
            Id: entity.Id,
            Name: entity.Name,
            Duration: entity.Duration,
            UnlockPointsRequiredPerRow: entity.UnlockPointsRequiredPerRow,
            Rows: entity.Rows.Select(r => ToDto(r, activityLookup)).ToList(),
            CreatedAt: entity.CreatedAt);
    }

    public static RowDto ToDto(Row row, IReadOnlyDictionary<Guid, ActivityDefinition>? activityLookup = null)
    {
        return new RowDto(
            row.Index,
            row.Tiles.Select(t => ToDto(t, activityLookup)).ToList());
    }

    public static TileDto ToDto(Tile tile, IReadOnlyDictionary<Guid, ActivityDefinition>? activityLookup = null)
    {
        return new TileDto(
            tile.Key,
            tile.Name,
            tile.Points,
            tile.RequiredCount,
            tile.AllowedActivities.Select(r => ToDto(r, activityLookup)).ToList());
    }

    public static TileActivityRuleDto ToDto(
        TileActivityRule rule,
        IReadOnlyDictionary<Guid, ActivityDefinition>? activityLookup = null)
    {
        var activityName = activityLookup?.TryGetValue(rule.ActivityDefinitionId, out var activity) == true
            ? activity.Name
            : null;

        return new TileActivityRuleDto(
            rule.ActivityDefinitionId,
            rule.ActivityKey,
            activityName,
            rule.AcceptedDropKeys.ToList(),
            rule.Requirements.Select(PlayerProfileMapper.ToDto).ToList(),
            rule.Modifiers.Select(ToDto).ToList());
    }

    public static ActivityModifierRuleDto ToDto(ActivityModifierRule modifier)
    {
        return new ActivityModifierRuleDto(
            PlayerProfileMapper.ToDto(modifier.Capability),
            modifier.TimeMultiplier,
            modifier.ProbabilityMultiplier);
    }

    public static Event ToEntity(CreateEventRequest request, IReadOnlyDictionary<Guid, string> activityKeyById)
    {
        var evt = new Event(
            request.Name.Trim(),
            request.Duration,
            request.UnlockPointsRequiredPerRow);

        var rows = request.Rows.Select(r => ToEntity(r, activityKeyById)).ToList();
        evt.SetRows(rows);
        return evt;
    }

    public static void ApplyToEntity(Event evt, UpdateEventRequest request, IReadOnlyDictionary<Guid, string> activityKeyById)
    {
        evt.UpdateName(request.Name.Trim());
        evt.UpdateDuration(request.Duration);
        evt.SetUnlockPointsRequiredPerRow(request.UnlockPointsRequiredPerRow);
        var rows = request.Rows.Select(r => ToEntity(r, activityKeyById)).ToList();
        evt.SetRows(rows);
    }

    public static Row ToEntity(RowDto dto, IReadOnlyDictionary<Guid, string> activityKeyById)
    {
        var tiles = dto.Tiles.Select(t => ToEntity(t, activityKeyById)).ToList();
        return new Row(dto.Index, tiles);
    }

    public static Tile ToEntity(TileDto dto, IReadOnlyDictionary<Guid, string> activityKeyById)
    {
        var rules = dto.AllowedActivities.Select(r => ToEntity(r, activityKeyById)).ToList();
        return new Tile(dto.Key, dto.Name, dto.Points, dto.RequiredCount, rules);
    }

    public static TileActivityRule ToEntity(TileActivityRuleDto dto, IReadOnlyDictionary<Guid, string> activityKeyById)
    {
        var activityKey = activityKeyById.TryGetValue(dto.ActivityDefinitionId, out var key) ? key : dto.ActivityKey ?? string.Empty;
        var requirements = dto.Requirements?.Select(PlayerProfileMapper.ToEntity).ToList() ?? [];
        var modifiers = dto.Modifiers?.Select(ToEntity).ToList() ?? [];
        return new TileActivityRule(
            dto.ActivityDefinitionId,
            activityKey,
            dto.AcceptedDropKeys ?? [],
            requirements,
            modifiers);
    }

    public static ActivityModifierRule ToEntity(ActivityModifierRuleDto dto)
    {
        return new ActivityModifierRule(
            PlayerProfileMapper.ToEntity(dto.Capability),
            dto.TimeMultiplier,
            dto.ProbabilityMultiplier);
    }
}
