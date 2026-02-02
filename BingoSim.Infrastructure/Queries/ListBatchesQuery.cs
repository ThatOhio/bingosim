using System.Text.Json;
using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BingoSim.Infrastructure.Queries;

/// <summary>
/// Lists simulation batches with event name and run counts; implements IListBatchesQuery.
/// </summary>
public class ListBatchesQuery(AppDbContext context, ILogger<ListBatchesQuery> logger) : IListBatchesQuery
{
    private const int MaxCandidatesWhenFilteringByEventName = 200;

    public async Task<ListBatchesResult> ExecuteAsync(ListBatchesRequest request, CancellationToken cancellationToken = default)
    {
        var top = Math.Max(1, Math.Min(500, request.Top));
        var cap = !string.IsNullOrWhiteSpace(request.EventNameSearch)
            ? Math.Min(MaxCandidatesWhenFilteringByEventName, top * 4)
            : top;

        var batchWithSnapshot = await (
            from b in context.SimulationBatches
            where request.StatusFilter == null || b.Status == request.StatusFilter
            join s in context.EventSnapshots on b.Id equals s.SimulationBatchId into snapshots
            from s in snapshots.DefaultIfEmpty()
            orderby b.CreatedAt descending
            select new { Batch = b, EventConfigJson = s != null ? s.EventConfigJson : null }
        ).Take(cap).ToListAsync(cancellationToken);

        var eventNameSearch = request.EventNameSearch?.Trim();
        var rows = batchWithSnapshot
            .Select(x =>
            {
                var eventName = GetEventNameFromSnapshot(x.EventConfigJson) ?? (x.Batch.Name ?? string.Empty);
                return new { x.Batch, EventName = eventName };
            })
            .Where(x => string.IsNullOrWhiteSpace(eventNameSearch) ||
                       x.EventName.Contains(eventNameSearch, StringComparison.OrdinalIgnoreCase))
            .Take(top)
            .ToList();

        var batchIds = rows.Select(r => r.Batch.Id).ToList();
        var runCounts = batchIds.Count > 0
            ? await context.SimulationRuns
                .Where(r => batchIds.Contains(r.SimulationBatchId))
                .GroupBy(r => r.SimulationBatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    Completed = g.Count(r => r.Status == RunStatus.Completed),
                    Failed = g.Count(r => r.Status == RunStatus.Failed)
                })
                .ToDictionaryAsync(x => x.BatchId, x => (x.Completed, x.Failed), cancellationToken)
            : new Dictionary<Guid, (int Completed, int Failed)>();

        var items = rows.Select(r =>
        {
            var counts = runCounts.TryGetValue(r.Batch.Id, out var c)
                ? c
                : (Completed: 0, Failed: 0);
            return new BatchListRowDto
            {
                BatchId = r.Batch.Id,
                CreatedAt = r.Batch.CreatedAt,
                Status = r.Batch.Status,
                EventName = r.EventName,
                RunCount = r.Batch.RunsRequested,
                CompletedCount = counts.Completed,
                FailedCount = counts.Failed,
                Seed = r.Batch.Seed,
                ExecutionMode = r.Batch.ExecutionMode
            };
        }).ToList();

        logger.LogDebug("ListBatchesQuery returned {Count} batches (Top={Top}, StatusFilter={StatusFilter}, EventNameSearch={EventNameSearch})",
            items.Count, top, request.StatusFilter?.ToString() ?? "all", eventNameSearch ?? "(none)");

        return new ListBatchesResult { Items = items };
    }

    private static string? GetEventNameFromSnapshot(string? eventConfigJson)
    {
        if (string.IsNullOrWhiteSpace(eventConfigJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(eventConfigJson);
            if (doc.RootElement.TryGetProperty("EventName", out var prop))
                return prop.GetString();
        }
        catch
        {
            // Fallback to null; caller uses batch name or empty.
        }
        return null;
    }
}
