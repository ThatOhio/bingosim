using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service interface for ActivityDefinition operations.
/// </summary>
public interface IActivityDefinitionService
{
    Task<IReadOnlyList<ActivityDefinitionResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ActivityDefinitionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateActivityDefinitionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateActivityDefinitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
