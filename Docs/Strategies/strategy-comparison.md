# Strategy Comparison: Row Unlocking

This document describes the **Row Unlocking Strategy**, the sole strategy available in BingoSim. It explains how it works and how it differs from the previous placeholder strategies (RowRush, GreedyPoints) that were removed in Phase 3.

---

## Overview

The **Row Unlocking Strategy** (`RowUnlocking`) focuses on unlocking the next row as quickly as possible. It uses a combination-based approach for both task selection and grant allocation to optimize for row progression.

---

## How Row Unlocking Works

### Task Selection (What Players Work On)

Unlike fixed-order strategies, Row Unlocking uses **optimal combination selection**:

1. **Identify the furthest unlocked row** — Players focus on the row closest to unlocking the next one.
2. **Calculate valid tile combinations** — For the current row, compute all combinations of tiles whose total points meet the row unlock threshold (e.g., 5 points).
3. **Select the optimal combination** — Choose the combination with the **shortest estimated completion time** (based on activity durations and player capabilities).
4. **Assign tasks from that combination** — Players are assigned to work on tiles within the optimal combination, preferring highest-point tiles first.
5. **Fallback logic** — If all tiles in the optimal combination are completed, fall back to highest-point tiles on the furthest row, then any unlocked row.

**Example:** If row 0 requires 5 points to unlock row 1, and tiles have points 1, 2, 3, 4, valid combinations might be `[1,4]`, `[2,3]`, `[1,2,2]`, etc. The strategy picks the combination with the shortest estimated time (e.g., two quick tiles vs. three slow ones).

### Grant Allocation (Where Progress Goes)

When an activity attempt produces a progress grant and multiple tiles could accept it:

1. **Primary:** Highest-point tile on the **furthest unlocked row** that accepts the grant.
2. **Fallback:** Highest-point tile from all eligible tiles (when no tiles on the furthest row accept the grant).
3. **Tie-break:** Alphabetical by tile key for determinism.

This keeps progress focused on the row that matters most for unlocking the next one.

---

## Comparison with Removed Strategies

| Aspect | RowRush (removed) | GreedyPoints (removed) | Row Unlocking |
|--------|-------------------|------------------------|---------------|
| **Task selection** | Fixed order: rows 0→N, tiles by points ascending (1,2,3,4) | Fixed order: rows 0→N, tiles by points descending (4,3,2,1) | **Optimal combination** — picks tiles that unlock next row fastest |
| **Grant allocation** | Lowest row, then lowest points | Highest points, then lowest row | **Furthest row focus** — highest points on furthest unlocked row |
| **Optimization goal** | Complete rows in order | Maximize total points | **Unlock next row fastest** |
| **Combination awareness** | None | None | Yes — considers which tile set meets threshold with shortest time |

---

## Diagrams

### Task Selection Flow (Row Unlocking)

```
Furthest unlocked row (e.g., row 0)
         ↓
Get all tile combinations that meet unlock threshold
         ↓
Enrich with estimated completion times
         ↓
Select combination with shortest time
         ↓
Assign players to tiles in that combination (highest points first)
         ↓
If combination complete → fallback to highest-point tile on row
         ↓
If row complete → move to next row
```

### Grant Allocation Flow

```
Activity attempt produces grant
         ↓
Get eligible tiles (accept drop, unlocked, not completed)
         ↓
Filter to tiles on furthest unlocked row
         ↓
If any → select highest-point tile (tie-break: tile key)
         ↓
Else → select highest-point tile from all eligible
```

---

## Configuration

- **Strategy key:** `"RowUnlocking"`
- **ParamsJson:** Currently `"{}"` — no configurable parameters.
- **UI:** When creating or editing a team, select "RowUnlocking" from the strategy dropdown. It is the only option and the default.

---

## User Guide: Selecting the Strategy

1. Navigate to **Events** → select an event → **Draft teams** (or edit an existing team).
2. In the team form, the **Strategy** dropdown shows **RowUnlocking**.
3. Save the team. All players on that team will use the Row Unlocking strategy during simulations.
4. **Expected behavior:** The team will prioritize tiles that unlock the next row fastest. Progress grants go to high-point tiles on the furthest unlocked row. Simulations should complete with efficient row progression.

---

## Technical Notes

- **Caching:** `RowCombinationCalculator` results are cached per row index during a simulation run. Row structure is static, so cache invalidation is not needed.
- **Capability restrictions:** If a player lacks capabilities for tiles in the optimal combination, the strategy falls back to tiles the player can work on.
- **Edge cases:** If a player has no valid tiles (all require capabilities they lack), `SelectTaskForPlayer` returns null.
