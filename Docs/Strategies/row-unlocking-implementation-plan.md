# Row Unlocking Strategy Implementation Plan

## Overview

The RowUnlockingStrategy focuses on unlocking the next row as quickly as possible. It differs from RowRush (which completes rows in order) and GreedyPoints (which maximizes total points) by explicitly optimizing for the tile combination that unlocks the next row fastest.

## Task Selection Algorithm

### High-Level Flow

1. **Determine the furthest unlocked row**  
   Use `context.UnlockedRowIndices.Max()` (or 0 if empty). This is the row we are working to complete enough points to unlock the next row.

2. **Calculate optimal tile combinations to unlock the next row**  
   - The next row unlocks when `sum(completed tile points in furthest row) >= UnlockPointsRequiredPerRow`
   - Find combinations of tiles in the furthest unlocked row whose points sum to at least `UnlockPointsRequiredPerRow`
   - Rank combinations by estimated completion time (see Caching Strategy)
   - Use a cached result when available

3. **For the given player:**
   - **Primary**: Find the highest-point tile in the optimal combination that the player can work on (capability requirements satisfied, not completed)
   - **Fallback 1**: Highest-point tile on the furthest unlocked row they can work on
   - **Fallback 2**: Highest-point tile anywhere in unlocked rows they can work on
   - **Return null** if no valid tiles

### Player Capability Filtering

- A player can work on a tile if, for the chosen rule, `rule.RequirementKeys.All(playerCapabilities.Contains)`
- Activity must exist in `EventSnapshot.ActivitiesById` and have at least one attempt

### Tile Ordering for Fallbacks

- Within a row: order by points descending (highest first)
- Across rows: furthest unlocked row first, then lower rows

---

## Grant Allocation Algorithm

### High-Level Flow

1. **Filter eligible tiles** to those on the furthest unlocked row  
   - `context.UnlockedRowIndices.Max()` gives the furthest row  
   - `context.EligibleTileKeys` filtered by `context.TileRowIndex[tileKey] == furthestRow`

2. **Select highest-point tile** from the filtered set  
   - Order by `context.TilePoints[tileKey]` descending, then by tile key for determinism

3. **Fallback**: If the filtered set is empty, select highest-point tile from all `context.EligibleTileKeys`

4. **Return null** if no eligible tiles

---

## Caching Strategy

### Rationale

- Computing optimal tile combinations is expensive (subset enumeration, time estimates)
- The furthest unlocked row changes only when a row is unlocked (infrequent)
- Per-row results can be reused across many task selection calls

### Cache Design

- **Cache key**: Row index (the furthest unlocked row we are optimizing for)
- **Cache value**: Optimal combination metadata (tiles involved, total time estimate, etc.)
- **Storage**: `Dictionary<int, CachedRowUnlockCombination>` (or similar) inside the strategy instance

### Cache Invalidation

- When `UnlockedRowIndices` changes (a new row unlocks), the "furthest row" changes
- The strategy is obtained per-team per-call from the factory; state is not shared across teams
- **Problem**: `RowUnlockingStrategy` is a singleton in the factory. Multiple teams may use it. The cache key should include team identity or the cache should be computed per-call from context.
- **Resolution**: Make the cache **context-based** rather than instance-based. Compute the combination on each `SelectTaskForPlayer` call but memoize within a single call using the row index from context. Alternatively, use a cache key of `(teamId, rowIndex)` if we have access to team ID in the context. `TaskSelectionContext` has `TeamSnapshot` with `TeamId`. So we can use `(context.TeamSnapshot.TeamId, furthestRow)` as cache key.
- **Simpler approach**: Cache by row index only. When the furthest row advances, we are optimizing for a new row; the old cache entries are unused. We can limit cache size (e.g., last N rows) or clear when row advances. For simplicity: cache by row index, no invalidation needed (old rows are never queried again for "optimal combination for unlocking row N").

### Recommended Cache Structure

```csharp
private readonly Dictionary<int, OptimalCombination> _combinationCache = new();

private sealed class OptimalCombination
{
    public IReadOnlySet<string> TileKeys { get; init; }
    public decimal EstimatedTotalTime { get; init; }
}
```

---

## Data Structures Needed

### OptimalCombination (or CachedRowUnlockCombination)

- `TileKeys`: Set of tile keys in the optimal combination
- `EstimatedTotalTime`: Sum of estimated completion times for those tiles (or another metric)
- Optional: `TotalPoints` to verify the combination meets unlock threshold

### Helper Methods

1. **GetFurthestUnlockedRow(context)**  
   Returns `context.UnlockedRowIndices.Max()` (or 0 if empty).

2. **GetOptimalCombinationForRow(context, rowIndex, unlockPointsRequired)**  
   - Enumerate tile subsets in the row whose points sum to >= unlockPointsRequired  
   - Estimate completion time per tile (e.g., from `TileRequiredCount` and `TileProgress`)  
   - Return the combination with lowest total estimated time  
   - Use cache for `rowIndex`

3. **GetTilesPlayerCanWorkOn(context)**  
   - Iterate tiles in unlocked rows  
   - For each tile, find rules where `rule.RequirementKeys.All(context.PlayerCapabilities.Contains)`  
   - Return collection of (tileKey, rule) the player can work on

4. **FilterTilesByRow(tileKeys, rowIndex, context)**  
   - Return tile keys where `context.TileRowIndex[tileKey] == rowIndex`

### Completion Time Estimate

- Simple model: `(requiredCount - currentProgress)` as a proxy for "work remaining"
- Refined: Use activity baseline time if available; for now, `requiredCount - progress` is sufficient for relative ordering of combinations

---

## Implementation Phases

### Phase 2.2: Grant Allocation

1. Implement `SelectTargetTileForGrant` with the algorithm above
2. Add unit tests for grant allocation (filter by row, fallback, null)

### Phase 2.3: Task Selection (Basic)

1. Implement `GetFurthestUnlockedRow` and `GetTilesPlayerCanWorkOn`
2. Implement fallback chain: optimal combination → furthest row → any unlocked
3. Use a simplified "optimal combination" (e.g., highest-point tiles that sum to threshold) before full enumeration

### Phase 2.4: Optimal Combination Calculation

1. Implement subset enumeration for a row
2. Add completion time estimation
3. Add caching by row index
4. Integrate with task selection

### Phase 2.5: Testing and Tuning

1. Integration tests with RowUnlockingStrategy in full simulation
2. Compare unlock times vs RowRush and GreedyPoints
3. Performance profiling of combination calculation and cache
