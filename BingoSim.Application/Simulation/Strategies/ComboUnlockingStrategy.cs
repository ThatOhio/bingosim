using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Two-phase strategy that optimizes for unlocking rows while avoiding activities needed for locked tiles,
/// then switches to maximizing shared activities across incomplete tiles once all rows are unlocked.
/// </summary>
/// <remarks>
/// <para><b>Phase 1 (Row Unlocking Mode):</b> Before all rows are unlocked, selects tile combinations that unlock
/// the next row while penalizing tiles whose activities are shared with locked tiles. Penalty = EstimatedTime × (1 + count of locked tiles sharing activities).
/// This discourages "burning" activities that will be needed later for locked tiles.</para>
/// <para><b>Phase 2 (Shared Activity Maximization Mode):</b> After all rows are unlocked, prioritizes tiles that share
/// activities with the most other incomplete tiles. Virtual score = points + (1 × shared incomplete tile count).
/// This maximizes the value gained from each activity by ensuring activities are used efficiently across the board.</para>
/// <para><b>When to use:</b> Use ComboUnlocking when you want row unlock optimization plus activity efficiency.
/// Use RowUnlocking for simpler row-focused behavior. Use Greedy for straightforward point maximization.</para>
/// <para><b>Cache invalidation:</b> Call <see cref="InvalidateCacheForRow"/> when a row unlocks or a tile on the row completes.
/// Wire this into SimulationRunner in a follow-up task.</para>
/// </remarks>
public sealed class ComboUnlockingStrategy : ITeamStrategy
{
    private readonly Dictionary<int, List<TileCombination>> _combinationCache = new();
    private readonly Dictionary<int, List<TileCombination>> _penalizedCombinationCache = new();

    /// <summary>
    /// Selects which activity/tile a player should work on next.
    /// Phase 1: Optimal combination (lowest penalized time) for unlocking next row.
    /// Phase 2: Highest virtual score (points + shared activity bonus) tile the player can work on.
    /// </summary>
    public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
    {
        if (context.UnlockedRowIndices.Count == 0)
            return null;

        var allRowsUnlocked = AreAllRowsUnlocked(context.EventSnapshot, context.UnlockedRowIndices);

        if (allRowsUnlocked)
            return SelectTaskForPlayerPhase2(context);
        return SelectTaskForPlayerPhase1(context);
    }

    /// <summary>
    /// Phase 1: Unlock rows while avoiding locked activity conflicts.
    /// </summary>
    private (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayerPhase1(
        TaskSelectionContext context)
    {
        var furthestRow = context.UnlockedRowIndices.Max();
        var threshold = context.EventSnapshot.UnlockPointsRequiredPerRow;

        var combinations = GetPenalizedCombinationsForRow(
            furthestRow,
            context.EventSnapshot,
            context.UnlockedRowIndices,
            threshold);

        if (combinations.Count == 0)
            return FindFallbackTask(context);

        var optimalCombination = combinations
            .OrderBy(c => c.EstimatedCompletionTime)
            .ThenBy(c => string.Join(",", c.TileKeys.OrderBy(k => k, StringComparer.Ordinal)), StringComparer.Ordinal)
            .First();

        var taskFromOptimal = FindTaskInTiles(context, optimalCombination.TileKeys, furthestRow);
        if (taskFromOptimal.HasValue)
            return taskFromOptimal;

        return FindFallbackTask(context);
    }

    /// <summary>
    /// Phase 2: Maximize shared activities across incomplete tiles.
    /// </summary>
    private (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayerPhase2(
        TaskSelectionContext context)
    {
        var availableTiles = context.EventSnapshot.Rows
            .SelectMany(r => r.Tiles)
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .ToList();

        if (availableTiles.Count == 0)
            return null;

        var tilesWithScores = availableTiles
            .Select(tile =>
            {
                var sharedCount = CountIncompleteTilesWithSharedActivities(
                    tile,
                    context.EventSnapshot,
                    context.CompletedTiles);
                return new
                {
                    Tile = tile,
                    VirtualScore = tile.Points + sharedCount,
                    EstimatedTime = TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot)
                };
            })
            .OrderByDescending(t => t.VirtualScore)
            .ThenBy(t => t.EstimatedTime)
            .ThenBy(t => t.Tile.Key, StringComparer.Ordinal);

        foreach (var item in tilesWithScores)
        {
            var eligibleRule = FindEligibleRule(context, item.Tile);
            if (eligibleRule.HasValue)
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
        }

        return null;
    }

    /// <summary>
    /// Selects the tile that should receive a progress grant.
    /// Phase 1: Same as RowUnlocking - highest point tile on furthest unlocked row.
    /// Phase 2: Highest point tile anywhere, tie-break by completion time then tile key.
    /// </summary>
    public string? SelectTargetTileForGrant(GrantAllocationContext context)
    {
        if (context.EligibleTileKeys.Count == 0)
            return null;

        var allRowsUnlocked = AreAllRowsUnlocked(context.EventSnapshot, context.UnlockedRowIndices);

        if (allRowsUnlocked)
        {
            return context.EligibleTileKeys
                .OrderByDescending(key => context.TilePoints[key])
                .ThenBy(key => GetEstimatedCompletionTime(key, context))
                .ThenBy(key => key, StringComparer.Ordinal)
                .First();
        }

        var furthestRow = context.UnlockedRowIndices.Max();
        var tilesOnFurthestRow = context.EligibleTileKeys
            .Where(key => context.TileRowIndex[key] == furthestRow)
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

    /// <summary>
    /// Invalidates cached combinations for a row. Call when the row unlocks or a tile on the row completes.
    /// Wire into SimulationRunner in a follow-up task.
    /// </summary>
    public void InvalidateCacheForRow(int rowIndex)
    {
        _combinationCache.Remove(rowIndex);
        _penalizedCombinationCache.Remove(rowIndex);
    }

    /// <summary>
    /// Invalidates the entire penalized combination cache. Call when any row unlocks, since penalties depend on unlocked state.
    /// </summary>
    public void InvalidateAllPenalizedCache()
    {
        _penalizedCombinationCache.Clear();
    }

    /// <summary>
    /// Counts how many locked tiles (on rows not yet unlocked) share at least one activity with the given tile.
    /// </summary>
    private static int CountLockedTilesWithSharedActivities(
        TileSnapshotDto tile,
        EventSnapshotDto snapshot,
        IReadOnlySet<int> unlockedRowIndices)
    {
        var tileActivityIds = tile.AllowedActivities
            .Select(rule => rule.ActivityDefinitionId)
            .ToHashSet();

        if (tileActivityIds.Count == 0)
            return 0;

        var count = 0;
        foreach (var row in snapshot.Rows)
        {
            if (unlockedRowIndices.Contains(row.Index))
                continue;

            foreach (var lockedTile in row.Tiles)
            {
                var hasSharedActivity = lockedTile.AllowedActivities
                    .Any(rule => tileActivityIds.Contains(rule.ActivityDefinitionId));

                if (hasSharedActivity)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Counts how many other incomplete tiles share at least one activity with the given tile.
    /// Used in phase 2 (after all rows unlocked).
    /// </summary>
    private static int CountIncompleteTilesWithSharedActivities(
        TileSnapshotDto targetTile,
        EventSnapshotDto snapshot,
        IReadOnlySet<string> completedTiles)
    {
        var tileActivityIds = targetTile.AllowedActivities
            .Select(rule => rule.ActivityDefinitionId)
            .ToHashSet();

        if (tileActivityIds.Count == 0)
            return 0;

        var count = 0;
        foreach (var row in snapshot.Rows)
        {
            foreach (var tile in row.Tiles)
            {
                if (tile.Key == targetTile.Key || completedTiles.Contains(tile.Key))
                    continue;

                var hasSharedActivity = tile.AllowedActivities
                    .Any(rule => tileActivityIds.Contains(rule.ActivityDefinitionId));

                if (hasSharedActivity)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Applies penalties to tile combinations based on locked tiles with shared activities.
    /// Returns a new list with updated EstimatedCompletionTime values.
    /// </summary>
    private List<TileCombination> ApplyPenaltiesToCombinations(
        List<TileCombination> combinations,
        EventSnapshotDto snapshot,
        int rowIndex,
        IReadOnlySet<int> unlockedRowIndices)
    {
        var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
            return combinations;

        var penalizedCombinations = new List<TileCombination>();

        foreach (var combination in combinations)
        {
            var totalPenalizedTime = 0.0;

            foreach (var tileKey in combination.TileKeys)
            {
                var tile = row.Tiles.FirstOrDefault(t => t.Key == tileKey);
                if (tile is null)
                    continue;

                var baseTime = TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
                var lockedShareCount = CountLockedTilesWithSharedActivities(
                    tile,
                    snapshot,
                    unlockedRowIndices);
                var penalizedTime = baseTime * (1 + lockedShareCount);
                totalPenalizedTime += penalizedTime;
            }

            penalizedCombinations.Add(new TileCombination
            {
                TileKeys = combination.TileKeys,
                TotalPoints = combination.TotalPoints,
                EstimatedCompletionTime = totalPenalizedTime
            });
        }

        return penalizedCombinations;
    }

    /// <summary>
    /// Determines if all rows in the event are unlocked.
    /// </summary>
    private static bool AreAllRowsUnlocked(
        EventSnapshotDto snapshot,
        IReadOnlySet<int> unlockedRowIndices)
    {
        return unlockedRowIndices.Count >= snapshot.Rows.Count;
    }

    private List<TileCombination> GetPenalizedCombinationsForRow(
        int rowIndex,
        EventSnapshotDto snapshot,
        IReadOnlySet<int> unlockedRowIndices,
        int threshold)
    {
        if (_penalizedCombinationCache.TryGetValue(rowIndex, out var cached))
            return cached;

        var baseCombinations = GetBaseCombinationsForRow(rowIndex, snapshot, threshold);

        if (baseCombinations.Count == 0)
            return baseCombinations;

        var penalized = ApplyPenaltiesToCombinations(
            baseCombinations,
            snapshot,
            rowIndex,
            unlockedRowIndices);

        _penalizedCombinationCache[rowIndex] = penalized;
        return penalized;
    }

    private List<TileCombination> GetBaseCombinationsForRow(
        int rowIndex,
        EventSnapshotDto snapshot,
        int threshold)
    {
        if (_combinationCache.TryGetValue(rowIndex, out var cached))
            return cached;

        var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
            return [];

        if (row.Tiles is not { Count: > 0 })
            return [];

        if (threshold <= 0)
            return [];

        Dictionary<string, int> tiles;
        try
        {
            tiles = row.Tiles.ToDictionary(t => t.Key, t => t.Points);
        }
        catch (ArgumentException)
        {
            return [];
        }

        try
        {
            var combinations = RowCombinationCalculator.CalculateCombinations(tiles, threshold);
            EnrichCombinationsWithTimeEstimates(combinations, snapshot, rowIndex);
            _combinationCache[rowIndex] = combinations;
            return combinations;
        }
        catch (IndexOutOfRangeException)
        {
            return [];
        }
    }

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

    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindTaskInTiles(
        TaskSelectionContext context,
        IReadOnlyList<string> targetTileKeys,
        int rowIndex)
    {
        var row = context.EventSnapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
        if (row is null)
            return null;

        var targetSet = targetTileKeys.ToHashSet(StringComparer.Ordinal);
        var tiles = row.Tiles
            .Where(t => targetSet.Contains(t.Key))
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

    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule)? FindTaskInAllRows(TaskSelectionContext context)
    {
        var allTiles = context.EventSnapshot.Rows
            .Where(r => context.UnlockedRowIndices.Contains(r.Index))
            .SelectMany(r => r.Tiles)
            .Where(t => !context.CompletedTiles.Contains(t.Key))
            .OrderByDescending(t => t.Points)
            .ThenBy(t => context.TileRowIndex.GetValueOrDefault(t.Key, -1))
            .ThenBy(t => t.Key, StringComparer.Ordinal);

        foreach (var tile in allTiles)
        {
            var eligibleRule = FindEligibleRule(context, tile);
            if (eligibleRule.HasValue)
                return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
        }

        return null;
    }

    private static double GetEstimatedCompletionTime(string tileKey, GrantAllocationContext context)
    {
        var tile = FindTileByKey(tileKey, context.EventSnapshot);
        return tile is null
            ? double.MaxValue
            : TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot);
    }

    private static TileSnapshotDto? FindTileByKey(string tileKey, EventSnapshotDto snapshot)
    {
        foreach (var row in snapshot.Rows)
        {
            var tile = row.Tiles.FirstOrDefault(t => string.Equals(t.Key, tileKey, StringComparison.Ordinal));
            if (tile is not null)
                return tile;
        }

        return null;
    }
}
