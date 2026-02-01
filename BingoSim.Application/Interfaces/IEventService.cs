using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service interface for Event operations.
/// </summary>
public interface IEventService
{
    Task<IReadOnlyList<EventResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EventResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
