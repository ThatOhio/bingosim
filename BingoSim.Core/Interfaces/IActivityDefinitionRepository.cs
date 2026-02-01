using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for ActivityDefinition persistence operations.
/// </summary>
public interface IActivityDefinitionRepository
{
    Task<ActivityDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityDefinition>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<ActivityDefinition?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ActivityDefinition entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(ActivityDefinition entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
