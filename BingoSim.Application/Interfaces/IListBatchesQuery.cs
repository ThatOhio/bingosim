using BingoSim.Application.DTOs;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Application query for listing simulation batches (discovery); implemented in Infrastructure.
/// </summary>
public interface IListBatchesQuery
{
    Task<ListBatchesResult> ExecuteAsync(ListBatchesRequest request, CancellationToken cancellationToken = default);
}
