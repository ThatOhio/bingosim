# Cache Invalidation Implementation

## Overview

Cache invalidation is wired into `SimulationRunner` so that strategies with caches (ComboUnlockingStrategy, RowUnlockingStrategy) receive notifications when rows unlock. This ensures cached tile combinations remain correct as the simulation progresses.

## Approach Chosen: Type Checking

We use **type checking** rather than adding a method to `ITeamStrategy`. Rationale:

- **Minimal interface surface:** GreedyStrategy has no cache; adding `OnSimulationStateChanged` would require a no-op implementation in every strategy.
- **Explicit opt-in:** Only strategies that cache (ComboUnlocking, RowUnlocking) implement invalidation. New strategies without caches require no changes.
- **Simplicity:** No interface change, no new contracts to document or test.

If future strategies need more complex invalidation (e.g., tile completion, time-based), we can introduce an optional interface such as `ICacheInvalidationStrategy` and use `as`/pattern matching.

## Where Invalidation Is Triggered

**Location:** `SimulationRunner.Execute`, inside the grant processing loop (around line 217).

**Flow:**
1. Before `AddProgress`: capture `state.UnlockedRowIndices` into `prevUnlockedRows`.
2. Call `state.AddProgress(target, grant.Units, ...)`.
3. Access `state.UnlockedRowIndices` again (triggers lazy recompute if a tile completed).
4. Compute `newlyUnlockedRows = state.UnlockedRowIndices.Except(prevUnlockedRows)`.
5. For each `rowIndex` in `newlyUnlockedRows`, call `NotifyStrategyOfRowUnlock(strategy, rowIndex)`.

## How Row Unlocks Are Detected

`TeamRunState.UnlockedRowIndices` is computed lazily when `_unlockedRowsDirty` is true. When `AddProgress` completes a tile, it sets `_unlockedRowsDirty = true` and updates `_completedPointsByRow`. The next read of `UnlockedRowIndices` recomputes the set.

We detect newly unlocked rows by comparing the unlocked set before and after `AddProgress`:

```csharp
var prevUnlockedRows = new HashSet<int>(state.UnlockedRowIndices);
state.AddProgress(target, grant.Units, simTime, ...);
var newlyUnlockedRows = state.UnlockedRowIndices.Except(prevUnlockedRows).ToList();
```

## How Tile Completions Are Handled

Tile completion is not explicitly tracked for invalidation. When a tile completes:

1. If it unlocks a new row, that row appears in `newlyUnlockedRows` and we invalidate that row.
2. If it does not unlock a new row (e.g., tile on row 1 completes but row 2 stays locked), we do not invalidate.

For ComboUnlocking and RowUnlocking, combinations are based on tile keys and points, which do not change. Completed tiles are filtered out when selecting tasks. Therefore, invalidation on tile completion alone is not required for correctness. Row unlock invalidation covers the case where the set of locked rows changes.

## Notification Helper

```csharp
private static void NotifyStrategyOfRowUnlock(ITeamStrategy strategy, int rowIndex)
{
    if (strategy is ComboUnlockingStrategy comboStrategy)
        comboStrategy.InvalidateCacheForRow(rowIndex);
    if (strategy is RowUnlockingStrategy rowStrategy)
        rowStrategy.InvalidateCacheForRow(rowIndex);
}
```

- **ComboUnlockingStrategy:** Clears both `_combinationCache` and `_penalizedCombinationCache` for the row. Penalties depend on `unlockedRowIndices`; when a row unlocks, cached penalized combinations for that row are stale.
- **RowUnlockingStrategy:** Clears `_combinationCache` for the row. Base combinations are static, but invalidation avoids holding cache for rows no longer in use and keeps behavior consistent.

## Strategy Instance Sharing

Strategies are singletons per strategy key (e.g., one `ComboUnlockingStrategy` instance for all ComboUnlocking teams). When team A unlocks row 2, we invalidate row 2. Team B (also ComboUnlocking) might still be on row 1. We only invalidate the newly unlocked row(s), not rows other teams may still be using. This avoids clearing cache needed by other teams.

## Testing Approach

1. **Unit tests:** `ComboUnlockingStrategyTests.InvalidateCacheForRow_ClearsCache` verifies that `InvalidateCacheForRow` clears the cache and that subsequent calls recompute.
2. **Integration tests:** `Simulation_WithComboUnlockingTeam_DoesNotCrash` and similar runs ensure the full simulation path works with invalidation.
3. **Multi-row unlock test:** `Simulation_WithComboUnlocking_MultiRowUnlock_CacheInvalidationWorks` runs a simulation with 2 rows (5 points to unlock row 1) and asserts `RowReached > 0`, confirming that row unlock occurs and cache invalidation is exercised during the run.
4. **Reproducibility:** `SimulationRunnerReproducibilityTests` confirm that the same seed produces identical results; cache invalidation should not change determinism.

## Performance Impact

- **Overhead:** One `HashSet` allocation and `Except` per grant that completes a tile. Negligible compared to grant processing and strategy logic.
- **Cache behavior:** Invalidation avoids using stale penalized combinations. Without it, ComboUnlocking could choose suboptimal combinations after row unlocks.
- **Memory:** Clearing cache for unlocked rows reduces memory use for rows no longer in use.

## Design Decisions

1. **Row unlock only:** We invalidate only when rows unlock, not on every tile completion. This is sufficient for correctness and keeps the logic simple.
2. **Per-row invalidation:** We invalidate only the newly unlocked row(s), not the entire cache, to avoid affecting other teams sharing the same strategy instance.
3. **No tile completion tracking:** Tile completion alone does not require invalidation for current strategies; row unlock covers the necessary cases.
4. **Type checking over interface:** Keeps `ITeamStrategy` focused on task selection and grant allocation; invalidation remains an implementation detail of caching strategies.
