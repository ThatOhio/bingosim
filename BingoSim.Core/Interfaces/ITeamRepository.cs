using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for Team persistence operations.
/// </summary>
public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, StrategyConfig strategyConfig, IEnumerable<TeamPlayer> teamPlayers, CancellationToken cancellationToken = default);
    Task UpdateAsync(Team team, StrategyConfig strategyConfig, IReadOnlyList<TeamPlayer> teamPlayers, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAllByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
