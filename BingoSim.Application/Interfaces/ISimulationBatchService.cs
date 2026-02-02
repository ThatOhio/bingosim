using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Application service for starting simulation batches and querying batch/results.
/// </summary>
public interface ISimulationBatchService
{
    Task<SimulationBatchResponse> StartBatchAsync(StartSimulationBatchRequest request, CancellationToken cancellationToken = default);
    Task<SimulationBatchResponse?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<BatchProgressResponse> GetProgressAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatchTeamAggregateResponse>> GetBatchAggregatesAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamRunResultResponse>> GetRunResultsAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamRunResultResponse>> GetBatchRunResultsForTeamAsync(Guid batchId, Guid teamId, int limit, CancellationToken cancellationToken = default);
}
