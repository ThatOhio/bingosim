using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service interface for PlayerProfile operations.
/// </summary>
public interface IPlayerProfileService
{
    Task<IReadOnlyList<PlayerProfileResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlayerProfileResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePlayerProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdatePlayerProfileRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
