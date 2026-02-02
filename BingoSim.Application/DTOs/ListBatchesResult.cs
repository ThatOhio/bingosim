namespace BingoSim.Application.DTOs;

/// <summary>
/// Result of listing simulation batches (top N; no TotalCount in v1).
/// </summary>
public sealed class ListBatchesResult
{
    public IReadOnlyList<BatchListRowDto> Items { get; init; } = [];
}
