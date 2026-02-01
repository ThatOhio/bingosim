using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Application service for ActivityDefinition operations.
/// </summary>
public class ActivityDefinitionService(
    IActivityDefinitionRepository repository,
    ILogger<ActivityDefinitionService> logger) : IActivityDefinitionService
{
    public async Task<IReadOnlyList<ActivityDefinitionResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.GetAllAsync(cancellationToken);
        return entities.Select(ActivityDefinitionMapper.ToResponse).ToList();
    }

    public async Task<ActivityDefinitionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ActivityDefinitionMapper.ToResponse(entity);
    }

    public async Task<Guid> CreateAsync(CreateActivityDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByKeyAsync(request.Key, cancellationToken);
        if (existing is not null)
            throw new ActivityDefinitionKeyAlreadyExistsException(request.Key);

        var modeSupport = ActivityDefinitionMapper.ToEntity(request.ModeSupport);
        var entity = new ActivityDefinition(request.Key, request.Name, modeSupport);

        var attempts = request.Attempts.Select(ActivityDefinitionMapper.ToEntity).ToList();
        entity.SetAttempts(attempts);

        var bands = request.GroupScalingBands?.Select(ActivityDefinitionMapper.ToEntity).ToList() ?? [];
        entity.SetGroupScalingBands(bands);

        await repository.AddAsync(entity, cancellationToken);

        logger.LogInformation(
            "Created ActivityDefinition {ActivityDefinitionId} with key '{ActivityDefinitionKey}'",
            entity.Id,
            entity.Key);

        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateActivityDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new ActivityDefinitionNotFoundException(id);

        var existingByKey = await repository.GetByKeyAsync(request.Key, cancellationToken);
        if (existingByKey is not null && existingByKey.Id != id)
            throw new ActivityDefinitionKeyAlreadyExistsException(request.Key);

        entity.UpdateKey(request.Key);
        entity.UpdateName(request.Name);
        entity.SetModeSupport(ActivityDefinitionMapper.ToEntity(request.ModeSupport));

        var attempts = request.Attempts.Select(ActivityDefinitionMapper.ToEntity).ToList();
        entity.SetAttempts(attempts);

        var bands = request.GroupScalingBands?.Select(ActivityDefinitionMapper.ToEntity).ToList() ?? [];
        entity.SetGroupScalingBands(bands);

        await repository.UpdateAsync(entity, cancellationToken);

        logger.LogInformation(
            "Updated ActivityDefinition {ActivityDefinitionId} with key '{ActivityDefinitionKey}'",
            entity.Id,
            entity.Key);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await repository.ExistsAsync(id, cancellationToken);
        if (!exists)
            throw new ActivityDefinitionNotFoundException(id);

        await repository.DeleteAsync(id, cancellationToken);

        logger.LogInformation("Deleted ActivityDefinition {ActivityDefinitionId}", id);
    }
}
