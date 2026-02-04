# ComboUnlocking Strategy Implementation

## Overview

The ComboUnlocking strategy is an evolution of RowUnlocking that optimizes for two goals:

1. **Before all rows unlocked:** Unlock rows efficiently while avoiding activities that will be needed for locked tiles later.
2. **After all rows unlocked:** Prioritize tiles that share activities with the most other incomplete tiles to maximize activity efficiency.

## Implementation Approach

### Two-Phase Behavior

The strategy operates in two distinct phases, determined by `AreAllRowsUnlocked`:

```csharp
var allRowsUnlocked = unlockedRowIndices.Count >= snapshot.Rows.Count;
```

- **Phase 1 (Row Unlocking Mode):** When `allRowsUnlocked` is false, the strategy focuses on unlocking the next row while penalizing tile combinations that use activities shared with locked tiles.
- **Phase 2 (Shared Activity Maximization Mode):** When `allRowsUnlocked` is true, the strategy prioritizes tiles by virtual score (points + shared activity bonus).

### Phase 1: Penalty Calculation

For each tile in a combination, we calculate:

```
penalizedTime = baseTime × (1 + lockedShareCount)
```

Where:
- `baseTime` = `TileCompletionEstimator.EstimateCompletionTime(tile, snapshot)`
- `lockedShareCount` = number of locked tiles (on rows not yet unlocked) that share at least one activity with this tile

**Example:** Tile A has base time 100s and shares activities with 2 locked tiles. Penalized time = 100 × (1 + 2) = 300s. This discourages selecting Tile A because completing it would "burn" an activity needed by 2 locked tiles.

**Combination penalty:** Sum of all tile penalties in the combination. The strategy selects the combination with the lowest total penalized time.

### Phase 2: Shared Activity Bonus

For each incomplete tile:

```
virtualScore = actualPoints + (1 × sharedIncompleteTileCount)
```

Where `sharedIncompleteTileCount` = number of other incomplete tiles that share at least one activity with this tile.

**Example:** Tile B (2 points) shares activities with 3 other incomplete tiles. Virtual score = 2 + 3 = 5. Tile C (4 points) shares with 0 others. Virtual score = 4. Tile B would be prioritized despite lower points because completing it helps progress multiple tiles.

**Sort order:** virtual score (desc) → estimated completion time (asc) → tile key (asc)

## Helper Method Descriptions

### CountLockedTilesWithSharedActivities

Counts locked tiles (on rows not yet unlocked) that share at least one activity with the target tile. Used in Phase 1 penalty calculation.

- **Input:** Tile, event snapshot, unlocked row indices
- **Output:** Count of locked tiles sharing activities
- **Edge case:** Tiles with no activities return 0

### CountIncompleteTilesWithSharedActivities

Counts other incomplete tiles that share at least one activity with the target tile. Used in Phase 2 virtual score calculation.

- **Input:** Target tile, event snapshot, completed tile keys
- **Output:** Count of incomplete tiles sharing activities
- **Edge case:** Excludes the target tile itself and completed tiles

### ApplyPenaltiesToCombinations

Creates new `TileCombination` instances with `EstimatedCompletionTime` replaced by the penalized sum. Does not mutate the input list.

### AreAllRowsUnlocked

Returns true when `unlockedRowIndices.Count >= snapshot.Rows.Count`. Handles empty event (0 rows) as "all unlocked."

### GetPenalizedCombinationsForRow

1. Checks `_penalizedCombinationCache` for row
2. If miss: gets base combinations via `GetBaseCombinationsForRow`, applies penalties, caches result
3. Returns penalized combinations

### GetBaseCombinationsForRow

1. Checks `_combinationCache` for row
2. If miss: uses `RowCombinationCalculator.CalculateCombinations`, enriches with time estimates, caches
3. Returns base combinations (no penalties)

## Code Reuse from RowUnlocking

The following logic is shared/copied from `RowUnlockingStrategy`:

- `FindTaskInTiles` – finds eligible task from a list of tile keys, ordered by points
- `FindEligibleRule` – validates player capabilities and activity existence
- `FindFallbackTask` – tries furthest row, then any unlocked row
- `FindTaskInRow` – highest point tile in a row the player can work on
- `FindTaskInAllRows` – highest point tile anywhere in unlocked rows
- `EnrichCombinationsWithTimeEstimates` – populates `EstimatedCompletionTime` on combinations
- `FindTileByKey` / `GetEstimatedCompletionTime` – used for grant allocation tie-breaking (same pattern as GreedyStrategy)

**Future refactor:** Consider extracting these to a shared base class or static helper to reduce duplication between RowUnlocking and ComboUnlocking.

## Cache Management Strategy

### Caches

1. **`_combinationCache`** – Base combinations per row (tile keys, points, base time). Key = row index. Static during run (row structure doesn't change).
2. **`_penalizedCombinationCache`** – Penalized combinations per row. Key = row index. **Depends on `unlockedRowIndices`** – when a row unlocks, penalties change.

### Invalidation

- **`InvalidateCacheForRow(rowIndex)`** – Clears both caches for the given row. Call when:
  - The row unlocks
  - A tile on the row completes
- **`InvalidateAllPenalizedCache()`** – Clears the entire penalized cache. Call when any row unlocks, since all rows' penalties depend on unlocked state.

### Wiring

Cache invalidation is wired into `SimulationRunner`. When a row unlocks (detected by comparing `UnlockedRowIndices` before and after `AddProgress`), the runner calls `NotifyStrategyOfRowUnlock`, which invokes `InvalidateCacheForRow` on ComboUnlockingStrategy and RowUnlockingStrategy. See `Docs/Strategies/cache-invalidation-implementation.md` for details.

## Testing Recommendations

1. **Phase 1 penalty behavior:** Create a scenario with 2+ rows, row 0 unlocked, row 1 locked. Add tiles on row 0 that share activities with row 1 tiles. Verify the strategy prefers combinations with fewer shared activities.
2. **Phase 2 shared bonus:** Create a scenario with all rows unlocked. One tile shares activities with many others. Verify it is prioritized over a higher-point tile with no shared activities.
3. **Phase transition:** Verify behavior at the exact moment the last row unlocks.
4. **Cache invalidation:** Call `InvalidateCacheForRow` and verify subsequent calls return fresh data.
5. **Edge cases:** Empty rows, single row, tiles with no activities, all tiles completed.

## Known Limitations and Edge Cases

1. **Cache staleness:** Without SimulationRunner integration, the penalized cache can become stale when `unlockedRowIndices` changes mid-run. Impact is limited for typical run lengths.
2. **Strategy instance sharing:** If the same strategy instance is used across multiple simulation runs (e.g., singleton), caches should be cleared between runs. The factory creates new instances per registration; verify lifecycle.
3. **Empty event:** `AreAllRowsUnlocked` with 0 rows returns true (0 >= 0), so Phase 2 logic runs. This is acceptable for empty events.
4. **Single row:** With one row, Phase 1 runs until that row is unlocked, then Phase 2.
5. **Performance:** Same order as RowUnlocking for combination enumeration. Phase 2 iterates all incomplete tiles and counts shared activities per tile – O(tiles²) in worst case for large boards.
