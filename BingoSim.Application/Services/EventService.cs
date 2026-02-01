using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Application service for Event operations. Resolves ActivityKey from ActivityDefinition at create/update.
/// </summary>
public class EventService(
    IEventRepository eventRepository,
    IActivityDefinitionRepository activityDefinitionRepository,
    ILogger<EventService> logger) : IEventService
{
    public async Task<IReadOnlyList<EventResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await eventRepository.GetAllAsync(cancellationToken);
        var activityLookup = await ResolveActivityLookupForEvents(entities, cancellationToken);
        return entities.Select(e => EventMapper.ToResponse(e, activityLookup)).ToList();
    }

    public async Task<EventResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await eventRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return null;

        var activityLookup = await ResolveActivityLookupForEvent(entity, cancellationToken);
        return EventMapper.ToResponse(entity, activityLookup);
    }

    public async Task<Guid> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken = default)
    {
        var activityKeyById = await ResolveActivityKeysAsync(request.Rows, cancellationToken);
        var entity = EventMapper.ToEntity(request, activityKeyById);
        await eventRepository.AddAsync(entity, cancellationToken);

        logger.LogInformation(
            "Created Event {EventId} with name '{EventName}', {RowCount} row(s)",
            entity.Id,
            entity.Name,
            entity.Rows.Count);

        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await eventRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new EventNotFoundException(id);

        var activityKeyById = await ResolveActivityKeysAsync(request.Rows, cancellationToken);
        EventMapper.ApplyToEntity(entity, request, activityKeyById);
        await eventRepository.UpdateAsync(entity, cancellationToken);

        logger.LogInformation(
            "Updated Event {EventId} with name '{EventName}', {RowCount} row(s)",
            entity.Id,
            entity.Name,
            entity.Rows.Count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await eventRepository.ExistsAsync(id, cancellationToken);
        if (!exists)
            throw new EventNotFoundException(id);

        await eventRepository.DeleteAsync(id, cancellationToken);

        logger.LogInformation("Deleted Event {EventId}", id);
    }

    private async Task<IReadOnlyDictionary<Guid, ActivityDefinition>> ResolveActivityLookupForEvent(
        Event evt,
        CancellationToken cancellationToken)
    {
        var ids = evt.Rows
            .SelectMany(r => r.Tiles.SelectMany(t => t.AllowedActivities.Select(a => a.ActivityDefinitionId)))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, ActivityDefinition>();

        var activities = await activityDefinitionRepository.GetByIdsAsync(ids, cancellationToken);
        return activities.ToDictionary(a => a.Id);
    }

    private async Task<IReadOnlyDictionary<Guid, ActivityDefinition>> ResolveActivityLookupForEvents(
        IReadOnlyList<Event> events,
        CancellationToken cancellationToken)
    {
        var ids = events
            .SelectMany(e => e.Rows.SelectMany(r => r.Tiles.SelectMany(t => t.AllowedActivities.Select(a => a.ActivityDefinitionId))))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, ActivityDefinition>();

        var activities = await activityDefinitionRepository.GetByIdsAsync(ids, cancellationToken);
        return activities.ToDictionary(a => a.Id);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActivityKeysAsync(
        List<RowDto> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows?
            .SelectMany(r => r.Tiles.SelectMany(t => t.AllowedActivities.Select(a => a.ActivityDefinitionId)))
            .Distinct()
            .ToList() ?? [];

        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var activities = await activityDefinitionRepository.GetByIdsAsync(ids, cancellationToken);
        return activities.ToDictionary(a => a.Id, a => a.Key);
    }
}
