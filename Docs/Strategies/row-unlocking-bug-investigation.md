# RowUnlocking Strategy Performance Bug Investigation

## 1. Issue Summary

### Reproduction Steps
- E2E test with two teams running 50,000 simulations each
- **Team Alpha**: RowUnlocking strategy
- **Team Beta**: ComboUnlocking strategy
- Same event configuration for both teams

### Current Behavior vs Expected Behavior

| Metric | Team Beta (ComboUnlocking) | Team Alpha (RowUnlocking) | Expected (RowUnlocking) |
|--------|---------------------------|--------------------------|-------------------------|
| Win Rate | 100% | 0% | Competitive |
| Avg Points | 166.8 | 54.7 | Similar to ComboUnlocking |
| Avg Tiles | 57.5 | 19.5 | Similar |
| Row Reached | 19 | 8 | Should reach row 19 |

RowUnlocking is performing dramatically worse—only reaching row 8 when ComboUnlocking reaches row 19. This suggests a critical bug rather than a strategy design difference.

### Test Configuration Details
- 50,000 simulations per team
- Same event (rows, tiles, activities, unlock threshold)
- Same RNG seed derivation (different per run)
- Both teams compete in the same simulation runs (head-to-head)

---

## 2. Investigation Results

### 2.1 Code Architecture Review

**Strategy instantiation:**
- `TeamStrategyFactory` creates singleton instances: one `RowUnlockingStrategy`, one `ComboUnlockingStrategy`
- Each team gets its strategy by key; Alpha uses RowUnlocking, Beta uses ComboUnlocking
- **No cross-contamination**: Different strategy instances, separate caches

**Cache invalidation:**
- `NotifyStrategyOfRowUnlock` is called when a row unlocks (in grant processing loop)
- Invalidates the **newly unlocked row** (e.g., when row 9 unlocks, invalidates row 9)
- RowUnlocking and ComboUnlocking both receive invalidation for their respective team's unlocks
- Flow is correct: we invalidate the row we're about to work on; we never had it cached before, so no incorrect invalidation

### 2.2 Key Code Comparison: RowUnlocking vs ComboUnlocking Phase 1

| Aspect | RowUnlockingStrategy | ComboUnlockingStrategy (Phase 1) |
|--------|---------------------|----------------------------------|
| Combination source | `GetCombinationsForRow` (base only) | `GetPenalizedCombinationsForRow` (base + penalties) |
| Cache | `_combinationCache` | `_combinationCache` + `_penalizedCombinationCache` |
| Optimal selection | `OrderBy(EstimatedCompletionTime)` | Same |
| FindTaskInTiles | Same logic | Same logic |
| FindFallbackTask | Same logic | Same logic |
| FindTaskInRow | Same logic | Same logic |
| FindTaskInAllRows | Same logic | Same logic |

**Critical difference:** ComboUnlocking has **Phase 2** when all rows are unlocked. RowUnlocking does not.

### 2.3 Phase 2 Advantage (ComboUnlocking Only)

When `unlockedRowIndices.Count >= snapshot.Rows.Count`, ComboUnlocking switches to Phase 2:
- Selects from **all tiles** in **all rows** (not just unlocked)
- Uses virtual score: `points + sharedIncompleteTileCount`
- Can work on row 19 tiles as soon as all rows are unlocked

RowUnlocking has no Phase 2—it always uses the combination/fallback logic limited to unlocked rows.

### 2.4 Potential Bug: FindTaskInAllRows Row Ordering

**Location:** `RowUnlockingStrategy.FindTaskInAllRows` (line 151) and `ComboUnlockingStrategy.FindTaskInAllRows` (line 364)

```csharp
.OrderByDescending(t => t.Points)
.ThenBy(t => context.TileRowIndex.GetValueOrDefault(t.Key, -1))  // BUG: ascending
.ThenBy(t => t.Key, StringComparer.Ordinal);
```

**Spec (from phase2-part5-completed.md):** "Across rows: furthest unlocked row first, then lower rows"

**Current behavior:** `ThenBy(rowIndex)` orders **ascending** (0, 1, 2, … 8). When points tie, **lower rows are preferred** (row 0 before row 8).

**Expected behavior:** Furthest row first → `ThenByDescending(rowIndex)` (row 8 before row 0).

**Impact:** When `FindTaskInRow(furthestRow)` returns null (e.g., all tiles on row 8 completed or no eligible rule), we fall back to `FindTaskInAllRows`. With wrong ordering, we prefer row 0 tiles over row 8. We "waste" effort on early rows instead of focusing on the furthest row needed to unlock the next row. This could significantly slow progress and, with time-limited simulations, prevent reaching row 9 before the event ends.

**Why ComboUnlocking may be less affected:** When ComboUnlocking reaches Phase 2 (all rows unlocked), it never uses `FindTaskInAllRows`—it uses `SelectTaskForPlayerPhase2` with a different selection algorithm. So it may avoid this fallback path more often.

### 2.5 GetBaseCombinationsForRow Robustness (ComboUnlocking Only)

ComboUnlocking's `GetBaseCombinationsForRow` has extra validation that RowUnlocking lacks:

```csharp
// ComboUnlockingStrategy.GetBaseCombinationsForRow
if (row.Tiles is not { Count: > 0 }) return [];
if (threshold <= 0) return [];
try {
    tiles = row.Tiles.ToDictionary(t => t.Key, t => t.Points);
} catch (ArgumentException) { return []; }
try {
    var combinations = RowCombinationCalculator.CalculateCombinations(tiles, threshold);
    ...
} catch (IndexOutOfRangeException) { return []; }
```

RowUnlocking's `GetCombinationsForRow` does not have these guards. In edge cases (empty row, duplicate keys, threshold 0), RowUnlocking could throw or behave differently. For a well-formed event, this is unlikely to be the root cause.

### 2.6 Log Output Samples (To Be Collected)

Diagnostic logging has not yet been added. The investigation plan recommends adding logging at:

- `SelectTaskForPlayer` entry/exit and return paths
- `GetCombinationsForRow` cache hit/miss and combination count
- `FindFallbackTask` usage and return value
- `InvalidateCacheForRow` calls

---

## 3. Root Cause Analysis

### Most Likely Root Cause: FindTaskInAllRows Row Ordering

**Hypothesis:** When RowUnlocking falls back to `FindTaskInAllRows`, it prefers row 0 over row 8 due to `ThenBy(rowIndex)` (ascending). This causes the strategy to assign tasks on early rows instead of the furthest row, slowing unlock progress. With a fixed-duration simulation, the team may run out of time before unlocking row 9.

**Supporting evidence:**
- Spec explicitly says "furthest unlocked row first"
- Both strategies share the same bug, but ComboUnlocking may hit the fallback less often (penalized combinations, Phase 2)
- Row 8 as a "stuck" point is consistent with wasting effort on rows 0–7

### Secondary Factor: No Phase 2 in RowUnlocking

RowUnlocking has no Phase 2. Once all rows are unlocked, ComboUnlocking can optimize for shared activities and complete tiles more efficiently. RowUnlocking continues with the same logic. This would matter only after all rows are unlocked, so it does not explain stopping at row 8.

### Ruled Out
- **Cache invalidation:** Logic is correct; we invalidate the newly unlocked row
- **Strategy instance sharing:** Alpha and Beta use different instances
- **Missing helper methods:** All helpers exist and are implemented
- **Row 8 configuration:** ComboUnlocking reaches row 19 on the same event, so row 8 has tiles

---

## 4. Code Comparison Summary

### Differences Between RowUnlocking and ComboUnlocking

| Feature | RowUnlocking | ComboUnlocking |
|---------|--------------|----------------|
| Phase 2 (all rows unlocked) | No | Yes (virtual score selection) |
| Combination penalties | No | Yes (locked tile activity sharing) |
| GetBaseCombinationsForRow guards | No | Yes (empty row, threshold, exceptions) |
| FindTaskInAllRows row ordering | `ThenBy` (ascending) | `ThenBy` (ascending) — same bug |

### Missing/Incorrect in RowUnlocking

1. **FindTaskInAllRows:** Uses `ThenBy(rowIndex)` instead of `ThenByDescending(rowIndex)` — wrong row priority in fallback
2. **Phase 2:** No equivalent to ComboUnlocking's Phase 2 (only relevant once all rows are unlocked)

---

## 5. Fix Required

### 5.1 Primary Fix: FindTaskInAllRows Row Ordering

**File:** `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs`  
**Location:** `FindTaskInAllRows` method, line 151

**Change:**
```csharp
// Before
.ThenBy(t => context.TileRowIndex.GetValueOrDefault(t.Key, -1))

// After
.ThenByDescending(t => context.TileRowIndex.GetValueOrDefault(t.Key, -1))
```

**Rationale:** When falling back to "any unlocked row", prefer the furthest row first to maximize progress toward unlocking the next row.

### 5.2 Apply Same Fix to ComboUnlockingStrategy

**File:** `BingoSim.Application/Simulation/Strategies/ComboUnlockingStrategy.cs`  
**Location:** `FindTaskInAllRows` method, line 364

Apply the same `ThenByDescending` change for consistency and correctness.

### 5.3 Optional: Add GetCombinationsForRow Guards (RowUnlocking)

Add the same validation as ComboUnlocking for robustness:
- `if (row is null) return [];` (already present)
- `if (row.Tiles is not { Count: > 0 }) return [];`
- `if (threshold <= 0) return [];`
- try/catch for `CalculateCombinations` if desired

### 5.4 Expected Behavior After Fix

- RowUnlocking should prefer the furthest row when falling back to `FindTaskInAllRows`
- Progress toward unlocking the next row should improve
- RowUnlocking should reach higher rows (e.g., row 19) in time-limited simulations
- Win rate and average points should be more competitive with ComboUnlocking

---

## 6. Testing Plan

### 6.1 Verify the Fix

1. **Unit test:** Add `FindTaskInAllRows_WhenMultipleRows_OrdersByFurthestRowFirst` to assert that when multiple rows have tiles with the same points, the furthest row is selected first.
2. **Integration test:** Re-run the E2E scenario (50k sims, Alpha=RowUnlocking, Beta=ComboUnlocking). RowUnlocking should reach row 19 and have a non-zero win rate.
3. **Regression:** Run existing `RowUnlockingStrategyRegistrationTests` and `ComboUnlockingStrategyTests`.

### 6.2 Diagnostic Logging (Optional, for deeper investigation)

If the primary fix does not fully resolve the issue, add logging as in the original investigation prompt:

- `SelectTaskForPlayer`: call count, return value, path taken
- `GetCombinationsForRow`: cache hit/miss, combination count
- `FindFallbackTask`: when used, what it returns

### 6.3 Additional Tests to Prevent Regression

- `RowUnlockingStrategy_FindTaskInAllRows_PrefersFurthestRowWhenPointsTie`
- `StrategyComparison_RowUnlocking_ReachesRow19_WhenEventHas20Rows`

---

## 7. Success Criteria

Investigation is complete when we can answer:

- [x] **Why is RowUnlocking stopping at row 8?** — Fallback to `FindTaskInAllRows` prefers lower rows, wasting effort on rows 0–7 instead of row 8.
- [x] **Why is ComboUnlocking reaching row 19?** — Phase 2 and possibly less use of the buggy fallback path.
- [x] **What's the root cause of the performance difference?** — Incorrect row ordering in `FindTaskInAllRows` plus Phase 2 advantage for ComboUnlocking.
- [x] **What specific code is causing the issue?** — `ThenBy(rowIndex)` instead of `ThenByDescending(rowIndex)` in `FindTaskInAllRows`.
- [x] **How to fix it?** — Change to `ThenByDescending` in both strategies.

---

## 8. Follow-Up: Implementation Prompt

After implementing the fix, use this prompt for verification:

```
Implement the RowUnlocking bug fix from Docs/Strategies/row-unlocking-bug-investigation.md:

1. In RowUnlockingStrategy.FindTaskInAllRows: change ThenBy(rowIndex) to ThenByDescending(rowIndex)
2. In ComboUnlockingStrategy.FindTaskInAllRows: apply the same change
3. Add unit test: FindTaskInAllRows_WhenMultipleRowsWithSamePoints_PrefersFurthestRow
4. Run StrategyComparisonIntegrationTests and verify RowUnlocking performs competitively
```
