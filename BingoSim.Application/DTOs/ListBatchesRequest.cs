using BingoSim.Core.Enums;

namespace BingoSim.Application.DTOs;

/// <summary>
/// Request for listing simulation batches (top N, optional filters).
/// </summary>
public sealed class ListBatchesRequest
{
    /// <summary>Maximum number of batches to return (e.g. 50 or 100).</summary>
    public int Top { get; init; } = 50;

    /// <summary>Optional filter by batch status.</summary>
    public BatchStatus? StatusFilter { get; init; }

    /// <summary>Optional search by event name (substring, case-insensitive).</summary>
    public string? EventNameSearch { get; init; }
}
