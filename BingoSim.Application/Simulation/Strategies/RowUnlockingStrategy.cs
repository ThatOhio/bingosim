using System.Collections.Concurrent;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Strategy focused on unlocking the next row as quickly as possible.
/// Task assignment prioritizes tiles in the optimal combination for unlocking the next row.
/// Grant allocation prioritizes highest point tiles on the furthest unlocked row.
/// </summary>
public sealed class RowUnlockingStrategy : ITeamStrategy
{
    private readonly ConcurrentDictionary<int, List<TileCombination>> _combinationCache = new();

    /// <summary>
    /// Selects which activity/tile a player should work on next.
    /// Priority 1: Highest point tile in the optimal combination (fastest to unlock next row).
    /// Priority 2: Highest point tile on the furthest unlocked row.
    /// Priority 3: Highest point tile anywhere in unlocked rows.
    /// Returns null when no valid task exists (e.g. all tiles completed, player lacks capabilities).
    /// </summary>
    public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
    {
        var teamLabel = $"[Team {context.TeamSnapshot.TeamName} RowUnlocking]";
        var furthestRow = context.UnlockedRowIndices.Count > 0 ? context.UnlockedRowIndices.Max() : -1;
        SimulationDiagnostics.Log($"{teamLabel} SelectTaskForPlayer called, furthest row: {furthestRow}, unlocked: [{string.Join(",", context.UnlockedRowIndices.OrderBy(r => r))}]");

        if (context.UnlockedRowIndices.Count == 0)
            return null;

        var threshold = context.EventSnapshot.UnlockPointsRequiredPerRow;
        var combinations = GetCombinationsForRow(furthestRow, context.EventSnapshot, threshold, teamLabel);

        if (combinations.Count == 0)
        {
            SimulationDiagnostics.Log($"{teamLabel} No combinations for row {furthestRow}, using FindFallbackTask");
            return FindFallbackTask(context);
        }

        var optimalCombination = combinations
            .OrderBy(c => c.EstimatedCompletionTime)
            .ThenBy(c => string.Join(",", c.TileKeys.OrderBy(k => k, StringComparer.Ordinal)), StringComparer.Ordinal)
            .First();

        var taskFromOptimal = FindTaskInTiles(context, optimalCombination.TileKeys, furthestRow, teamLabel);
        if (taskFromOptimal.HasValue)
        {
            SimulationDiagnostics.Log($"{teamLabel} Player {context.PlayerIndex} assigned to tile in optimal combination");
            return taskFromOptimal;
        }

        SimulationDiagnostics.Log($"{teamLabel} No task from optimal combination, using FindFallbackTask");
        return FindFallbackTask(context);
    }

    /// <summary>
    /// Priority 1 helper: Finds the highest point tile from targetTileKeys that the player can work on.
    /// </summary>
    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindTaskInTiles(
        TaskSelectionContext context,
        IReadOnlyList<string> targetTileKeys,
        int rowIndex,
        string teamLabel)
    {
        SimulationDiagnostics.Log($"{teamLabel} FindTaskInTiles called for row {rowIndex} with {targetTileKeys.Count} target tiles: [{string.Join(", ", targetTileKeys)}]");

        var row = context.EventSnapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
        {
            SimulationDiagnostics.Log($"{teamLabel} Row {rowIndex} not found in snapshot");
            return null;
        }

        var targetSet = targetTileKeys.ToHashSet(StringComparer.Ordinal);
        var tiles = row.Tiles
            .Where(t => targetSet.Contains(t.Key))
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .OrderByDescending(t => t.Points)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var tile in tiles)
        {
            var eligibleRule = FindEligibleRule(context, tile);
            if (eligibleRule.HasValue)
            {
                SimulationDiagnostics.Log($"{teamLabel} Found eligible tile {tile.Key} on row {rowIndex}");
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
            }
        }

        SimulationDiagnostics.Log($"{teamLabel} No eligible tiles found in target list for row {rowIndex}");
        return null;
    }

    /// <summary>
    /// Checks if the player can work on a tile and returns the first eligible (activityId, rule).
    /// Validates capability requirements and activity existence.
    /// </summary>
    private static (Guid activityId, TileActivityRuleSnapshotDto rule)? FindEligibleRule(
        TaskSelectionContext context,
        TileSnapshotDto tile)
    {
        foreach (var rule in tile.AllowedActivities)
        {
            if (rule.RequirementKeys.Count > 0 && !rule.RequirementKeys.All(context.PlayerCapabilities.Contains))
                continue;

            var activity = context.EventSnapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
            if (activity is null || activity.Attempts.Count == 0)
                continue;

            return (rule.ActivityDefinitionId, rule);
        }

        return null;
    }

    /// <summary>
    /// Priority 2 and 3 fallback: Tries furthest row first, then any unlocked row.
    /// </summary>
    private (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindFallbackTask(TaskSelectionContext context)
    {
        if (context.UnlockedRowIndices.Count == 0)
            return null;

        var furthestRow = context.UnlockedRowIndices.Max();
        var taskFromFurthestRow = FindTaskInRow(context, furthestRow);
        if (taskFromFurthestRow.HasValue)
            return taskFromFurthestRow;

        return FindTaskInAllRows(context);
    }

    /// <summary>
    /// Priority 2 helper: Highest point tile in a specific row that the player can work on.
    /// </summary>
    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindTaskInRow(
        TaskSelectionContext context,
        int rowIndex)
    {
        var row = context.EventSnapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
            return null;

        var tiles = row.Tiles
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .OrderByDescending(t => t.Points)
            .ThenBy(t => t.Key, StringComparer.Ordinal);

        foreach (var tile in tiles)
        {
            var eligibleRule = FindEligibleRule(context, tile);
            if (eligibleRule.HasValue)
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
        }

        return null;
    }

    /// <summary>
    /// Priority 3 helper: Highest point tile anywhere in unlocked rows.
    /// </summary>
    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindTaskInAllRows(TaskSelectionContext context)
    {
        var allTiles = context.EventSnapshot.Rows
            .Where(r => context.UnlockedRowIndices.Contains(r.Index))
            .SelectMany(r => r.Tiles)
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .OrderByDescending(t => t.Points)
            .ThenByDescending(t => context.TileRowIndex.GetValueOrDefault(t.Key, -1))
            .ThenBy(t => t.Key, StringComparer.Ordinal);

        foreach (var tile in allTiles)
        {
            var eligibleRule = FindEligibleRule(context, tile);
            if (eligibleRule.HasValue)
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
        }

        return null;
    }

    /// <summary>
    /// Selects the tile that should receive a progress grant.
    /// Primary: highest point tile on the furthest unlocked row that accepts the grant.
    /// Fallback: highest point tile from all eligible tiles (e.g. when no tiles on furthest row accept the grant).
    /// Returns null when there are no eligible tiles.
    /// Tie-breaking: by tile key (alphabetical) for determinism.
    /// </summary>
    /// <param name="context">Grant allocation context with eligible tiles and tile metadata.</param>
    /// <returns>The tile key to receive the grant, or null if no eligible tiles.</returns>
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        var furthestUnlockedRow = GetFurthestUnlockedRow(context.UnlockedRowIndices);

        var tilesOnFurthestRow = context.EligibleTileKeys
            .Where(key => context.TileRowIndex[key] == furthestUnlockedRow)
            .ToList();

        if (tilesOnFurthestRow.Count > 0)
        {
            return tilesOnFurthestRow
                .OrderByDescending(key => context.TilePoints[key])
                .ThenBy(key => key, StringComparer.Ordinal)
                .First();
        }

        return context.EligibleTileKeys
            .OrderByDescending(key => context.TilePoints[key])
            .ThenBy(key => context.TileRowIndex[key])
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }

    private static int GetFurthestUnlockedRow(IReadOnlySet<int> unlockedRowIndices)
    {
        return unlockedRowIndices.Count > 0 ? unlockedRowIndices.Max() : 0;
    }

    /// <summary>
    /// Invalidates cached combinations for a row. Called by SimulationRunner when a row unlocks.
    /// </summary>
    public void InvalidateCacheForRow(int rowIndex)
    {
        var removed = _combinationCache.TryRemove(rowIndex, out _);
        SimulationDiagnostics.Log($"[RowUnlocking] InvalidateCacheForRow({rowIndex}) called, entry {(removed ? "was removed" : "did not exist")}");
    }

    /// <summary>
    /// Gets tile combinations for a row that meet the unlock threshold.
    /// Results are cached per row index since row structure is static during a simulation run.
    /// Combinations are enriched with estimated completion times.
    /// </summary>
    private List<TileCombination> GetCombinationsForRow(
        int rowIndex,
        EventSnapshotDto snapshot,
        int threshold,
        string teamLabel)
    {
        SimulationDiagnostics.Log($"{teamLabel} GetCombinationsForRow called for row {rowIndex}, threshold {threshold}");

        if (_combinationCache.TryGetValue(rowIndex, out var cached))
        {
            SimulationDiagnostics.Log($"{teamLabel} Cache HIT for row {rowIndex}: {cached.Count} combinations");
            return cached;
        }

        SimulationDiagnostics.Log($"{teamLabel} Cache MISS for row {rowIndex}, calculating...");

        var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
        {
            SimulationDiagnostics.Log($"{teamLabel} ERROR: Row {rowIndex} not found in snapshot!");
            return [];
        }

        var tiles = row.Tiles.ToDictionary(t => t.Key, t => t.Points);
        SimulationDiagnostics.Log($"{teamLabel} Row {rowIndex} has {tiles.Count} tiles: [{string.Join(", ", tiles.Select(t => $"{t.Key}({t.Value}pts)"))}]");

        var combinations = RowCombinationCalculator.CalculateCombinations(tiles, threshold);
        SimulationDiagnostics.Log($"{teamLabel} CalculateCombinations returned {combinations.Count} combinations");

        if (combinations.Count == 0)
            SimulationDiagnostics.Log($"{teamLabel} WARNING: No combinations found for row {rowIndex}!");
        else
            SimulationDiagnostics.Log($"{teamLabel} First combination: [{string.Join(", ", combinations[0].TileKeys)}] = {combinations[0].TotalPoints}pts");

        EnrichCombinationsWithTimeEstimates(combinations, snapshot, rowIndex);
        if (combinations.Count > 0)
            SimulationDiagnostics.Log($"{teamLabel} After enrichment, first combination time: {combinations[0].EstimatedCompletionTime}");

        _combinationCache[rowIndex] = combinations;
        return combinations;
    }

    /// <summary>
    /// Populates EstimatedCompletionTime for each combination by summing tile completion estimates.
    /// </summary>
    private static void EnrichCombinationsWithTimeEstimates(
        List<TileCombination> combinations,
        EventSnapshotDto snapshot,
        int rowIndex)
    {
        var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
            return;

        foreach (var combination in combinations)
        {
            var totalTime = 0.0;
            foreach (var tileKey in combination.TileKeys)
            {
                var tile = row.Tiles.FirstOrDefault(t => t.Key == tileKey);
                if (tile is not null)
                    totalTime += TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
            }
            combination.EstimatedCompletionTime = totalTime;
        }
    }
}
