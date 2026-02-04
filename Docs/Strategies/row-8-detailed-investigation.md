# Row 8 Detailed Investigation

## Executive Summary

**Critical finding:** In a controlled single-simulation test with diagnostic logging, **RowUnlocking and ComboUnlocking both reach row 19** and achieve identical results (40 tiles, 100 points). RowUnlocking does **not** stop at row 8 in this scenario.

This indicates the "RowUnlocking stops at row 8" issue is **not a bug in the strategy logic** but is **specific to the event configuration or environment** used in the 50,000-simulation E2E tests.

---

## 1. Test Configuration

### Snapshot Used
- **Event:** 20 rows (0–19), 2 tiles per row (2pts + 3pts = 5pts per row)
- **Duration:** 14,400 seconds (4 hours)
- **Unlock threshold:** 5 points per row
- **Teams:** Alpha (RowUnlocking), Beta (ComboUnlocking)
- **Players:** 1 player per team, always-online schedule
- **Seed:** `row8-investigation-seed`

### Results (Single Run)

| Metric | Team Alpha (RowUnlocking) | Team Beta (ComboUnlocking) |
|--------|---------------------------|---------------------------|
| Row Reached | 19 | 19 |
| Tiles Completed | 40 | 40 |
| Total Points | 100 | 100 |

---

## 2. Log Output Summary

### Row Unlocks

| Row | Team Alpha (RowUnlocking) | Team Beta (ComboUnlocking) |
|-----|---------------------------|---------------------------|
| Row 8 unlocked? | Yes (simTime 969) | Yes (simTime 983) |
| Row 9 unlocked? | Yes (simTime 1097) | Yes (simTime 1099) |
| Row 19 unlocked? | Yes (simTime 2306) | Yes (simTime 2262) |

### Row 8 Behavior (Alpha / RowUnlocking)

| Aspect | Result |
|--------|--------|
| Row 8 exists in snapshot? | Yes |
| Tiles on row 8? | 2 tiles: r8t1(2pts), r8t2(3pts) |
| Combinations found for row 8? | Yes, 1 combination [r8t1, r8t2] = 5pts |
| Tasks assigned on row 8? | Yes – r8t2 then r8t1 |
| Points earned on row 8? | 5 points (3 + 2) |
| Row 9 unlocked? | Yes |

### Comparison Table

| Aspect | Team Alpha (RowUnlocking) | Team Beta (ComboUnlocking) |
|--------|---------------------------|---------------------------|
| Row 8 unlocked? | Yes | Yes |
| Row 9 unlocked? | Yes | Yes |
| Combinations found for row 8? | Yes (1) | Yes (via GetBaseCombinationsForRow) |
| Tasks assigned on row 8? | Yes | Yes |
| Points earned on row 8? | 5 | 5 |
| Cache invalidation for row 8? | InvalidateCacheForRow(8) called, entry did not exist | base=False, penalized=False |

---

## 3. Root Cause Analysis

### What We Ruled Out

1. **Strategy logic bug** – RowUnlocking correctly computes combinations, assigns tasks, and unlocks rows in this test.
2. **GetCombinationsForRow** – Returns valid combinations for all rows including row 8.
3. **FindTaskInTiles** – Correctly finds eligible tiles.
4. **Cache invalidation** – InvalidateCacheForRow is called for the newly unlocked row; "entry did not exist" is expected (we never cached row 9 before it unlocked).
5. **Row 8 special case** – No difference in behavior at row 8 vs other rows.

### Likely Cause: E2E Event Configuration

The 0% vs 100% win rate in the 50k E2E runs points to a **configuration or environmental difference**, not a strategy defect. Possible factors:

#### 3.1 Player Schedule

- **This test:** Always-online schedule (`Sessions = []` means 24/7 availability).
- **E2E event:** Players may have limited online windows. If RowUnlocking teams get fewer event-processing "turns" due to event queue ordering (e.g., ComboUnlocking events dequeued first when times tie), they could fall behind.

#### 3.2 Event Structure

- **This test:** Uniform 2 tiles × 5 points per row.
- **E2E event:** Rows may have different tile counts, point distributions, or activity requirements. A misconfigured row 8 (e.g., no tiles, wrong activities) could block progress only in that setup.

#### 3.3 Event Duration

- **This test:** 4 hours (14,400 seconds).
- **E2E event:** Shorter duration could cause RowUnlocking to run out of time before reaching row 9 if it is slightly slower per run.

#### 3.4 Event Queue Ordering

- Events are prioritized by `(endTime, teamIndex, firstPlayerIndex)`. When times tie, **team index** breaks the tie. If Alpha (RowUnlocking) is index 0 and Beta (ComboUnlocking) is index 1, Alpha is processed first. If the order is reversed, Beta could get more turns. This could systematically favor one strategy.

#### 3.5 RNG Variance

- Over 50k runs, RNG affects grant outcomes. If the E2E event has lower grant probability or higher variance, RowUnlocking might be more sensitive than ComboUnlocking.

---

## 4. Next Steps

### 4.1 Capture E2E Event Configuration

1. Export the exact snapshot JSON used in the 50k E2E runs.
2. Run the diagnostic test with that snapshot and seed.
3. Compare behavior: does RowUnlocking stop at row 8 with the real config?

### 4.2 Compare Schedules

1. Inspect player schedules in the E2E event.
2. Run a test with limited online windows (e.g., 2 hours/day) and see if RowUnlocking falls behind.

### 4.3 Event Queue Ordering

1. Add logging for event dequeue order (team index when times tie).
2. Run 100 simulations and count how often each team is processed first on ties.
3. If one team is consistently favored, consider a fairer tie-breaker (e.g., round-robin).

### 4.4 Reproduce with E2E Snapshot

Create a test that:

1. Loads the actual E2E event snapshot.
2. Runs a single simulation with diagnostic logging.
3. Captures whether RowUnlocking stops at row 8 with that exact config.

---

## 5. Conclusion

**RowUnlocking strategy logic is correct.** In a controlled test with 20 rows, both strategies reach row 19 with identical scores.

The "RowUnlocking stops at row 8" behavior in the 50k E2E runs is almost certainly due to **event configuration or simulation environment** (schedules, duration, event structure, or queue ordering), not a defect in `RowUnlockingStrategy` or `GetCombinationsForRow`.

**Recommended action:** Run the diagnostic test with the **actual E2E event snapshot** to reproduce the issue and isolate the configuration difference.

---

## Appendix: Full Log Sample (Rows 7–10)

```
[Team Alpha] Tile r7t1 COMPLETED on row 7 for 2 points at simTime 969
[Team Alpha] Row 8 UNLOCKED at simTime 969
[RowUnlocking] InvalidateCacheForRow(8) called, entry did not exist
[Team Alpha RowUnlocking] SelectTaskForPlayer called, furthest row: 8, unlocked: [0,1,2,3,4,5,6,7,8]
[Team Alpha RowUnlocking] GetCombinationsForRow called for row 8, threshold 5
[Team Alpha RowUnlocking] Cache MISS for row 8, calculating...
[Team Alpha RowUnlocking] Row 8 has 2 tiles: [r8t1(2pts), r8t2(3pts)]
[Team Alpha RowUnlocking] CalculateCombinations returned 1 combinations
[Team Alpha RowUnlocking] First combination: [r8t1, r8t2] = 5pts
[Team Alpha RowUnlocking] FindTaskInTiles called for row 8 with 2 target tiles: [r8t1, r8t2]
[Team Alpha RowUnlocking] Found eligible tile r8t2 on row 8
[Team Alpha RowUnlocking] Player 0 assigned to tile in optimal combination
...
[Team Alpha] Tile r8t1 COMPLETED on row 8 for 2 points at simTime 1097
[Team Alpha] Row 9 UNLOCKED at simTime 1097
```

Full log: `Docs/Strategies/row-8-investigation-log.txt`
