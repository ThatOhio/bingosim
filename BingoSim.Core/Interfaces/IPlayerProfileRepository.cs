using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for PlayerProfile persistence operations.
/// </summary>
public interface IPlayerProfileRepository
{
    Task<PlayerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlayerProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
