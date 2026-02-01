using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Application service for PlayerProfile operations.
/// </summary>
public class PlayerProfileService(
    IPlayerProfileRepository repository,
    ILogger<PlayerProfileService> logger) : IPlayerProfileService
{
    public async Task<IReadOnlyList<PlayerProfileResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await repository.GetAllAsync(cancellationToken);
        return profiles.Select(PlayerProfileMapper.ToResponse).ToList();
    }

    public async Task<PlayerProfileResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetByIdAsync(id, cancellationToken);
        return profile is null ? null : PlayerProfileMapper.ToResponse(profile);
    }

    public async Task<Guid> CreateAsync(CreatePlayerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = new PlayerProfile(request.Name, request.SkillTimeMultiplier);

        var capabilities = request.Capabilities.Select(PlayerProfileMapper.ToEntity);
        profile.SetCapabilities(capabilities);

        var schedule = PlayerProfileMapper.ToEntity(request.WeeklySchedule);
        profile.SetWeeklySchedule(schedule);

        await repository.AddAsync(profile, cancellationToken);

        logger.LogInformation(
            "Created PlayerProfile {PlayerProfileId} with name '{PlayerProfileName}'",
            profile.Id,
            profile.Name);

        return profile.Id;
    }

    public async Task UpdateAsync(Guid id, UpdatePlayerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new PlayerProfileNotFoundException(id);

        profile.UpdateName(request.Name);
        profile.UpdateSkillTimeMultiplier(request.SkillTimeMultiplier);

        var capabilities = request.Capabilities.Select(PlayerProfileMapper.ToEntity);
        profile.SetCapabilities(capabilities);

        var schedule = PlayerProfileMapper.ToEntity(request.WeeklySchedule);
        profile.SetWeeklySchedule(schedule);

        await repository.UpdateAsync(profile, cancellationToken);

        logger.LogInformation(
            "Updated PlayerProfile {PlayerProfileId} with name '{PlayerProfileName}'",
            profile.Id,
            profile.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await repository.ExistsAsync(id, cancellationToken);
        if (!exists)
        {
            throw new PlayerProfileNotFoundException(id);
        }

        await repository.DeleteAsync(id, cancellationToken);

        logger.LogInformation("Deleted PlayerProfile {PlayerProfileId}", id);
    }
}
