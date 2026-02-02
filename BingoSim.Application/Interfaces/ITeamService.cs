using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service interface for Team operations (draft teams for an event).
/// </summary>
public interface ITeamService
{
    Task<IReadOnlyList<TeamResponse>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<TeamResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAllByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
}
