# Phase 2 Part 5: Implement Task Selection Logic for Row Unlocking Strategy — Completed

## Summary

Implemented `SelectTaskForPlayer` in `RowUnlockingStrategy` with a three-tier priority system and five helper methods. The strategy now assigns tasks based on the optimal tile combination for unlocking the next row.

## Priority / Fallback Chain

```
SelectTaskForPlayer
├── No unlocked rows? → return null
├── Get combinations for furthest row
├── No combinations? → FindFallbackTask
├── Select optimal combination (shortest EstimatedCompletionTime)
├── FindTaskInTiles(optimal combination) → found? return
└── FindFallbackTask
    ├── FindTaskInRow(furthest row) → found? return
    └── FindTaskInAllRows → return (or null)
```

## Helper Method Details

### SelectTaskForPlayer (main)

- Determines furthest unlocked row via `UnlockedRowIndices.Max()`
- Gets combinations via `GetCombinationsForRow` (cached, time-enriched)
- Selects optimal: `OrderBy(EstimatedCompletionTime).ThenBy(tile keys)` for determinism
- Tries `FindTaskInTiles` with optimal combination; falls back to `FindFallbackTask` if none

### FindTaskInTiles (Priority 1)

- Filters row tiles to `targetTileKeys`, excludes completed
- Orders by points descending, then tile key
- For each tile, calls `FindEligibleRule`; returns first match

### FindEligibleRule

- For each rule in `tile.AllowedActivities`:
  - Skips if `RequirementKeys` not satisfied by `PlayerCapabilities`
  - Skips if activity missing or has no attempts
- Returns first valid `(activityId, rule)`

### FindFallbackTask (Priority 2 & 3)

- Tries `FindTaskInRow(furthestRow)` first
- If none, calls `FindTaskInAllRows`

### FindTaskInRow (Priority 2)

- Gets row by index, excludes completed tiles
- Orders by points descending, then tile key
- Returns first tile with an eligible rule

### FindTaskInAllRows (Priority 3)

- Collects tiles from all unlocked rows
- Excludes completed
- Orders by points descending, row index, tile key
- Returns first tile with an eligible rule

## Edge Case Handling

| Edge Case | Handling |
|-----------|----------|
| No unlocked rows | Return null immediately |
| All tiles on furthest row completed | FindTaskInTiles returns null → FindFallbackTask → FindTaskInRow returns null → FindTaskInAllRows finds tiles on earlier rows |
| Player has no capabilities | FindEligibleRule skips rules with RequirementKeys; returns null if none match |
| Multiple tiles with same points | Deterministic: `ThenBy(t.Key, StringComparer.Ordinal)` |
| Optimal combination empty | FindFallbackTask |
| Optimal combination all completed | FindTaskInTiles returns null → FindFallbackTask |
| Row not found | FindTaskInTiles/FindTaskInRow return null |
| TileRowIndex missing key | `GetValueOrDefault(t.Key, -1)` so tiles sort last |

## Testing

### Tests Added

| Test | Scenario | Expected |
|------|----------|----------|
| `SelectTaskForPlayer_PlayerCanWorkOnTile_ReturnsTask` | Context with row, tile, activity, no requirements | Non-null (activityId, rule) |
| `SelectTaskForPlayer_AllTilesCompleted_ReturnsNull` | Same context with t1 in CompletedTiles | null |
| `RowUnlockingStrategy_SelectTaskForPlayer_DoesNotThrow` | Minimal context (empty rows) | null, no throw |
| `Simulation_WithRowUnlockingTeam_DoesNotCrash` | Full simulation with RowUnlocking team | Completes, team in results |

### Test Results

- All 10 RowUnlockingStrategy tests passed
- Build: Succeeded

## Example Walkthrough

**Setup**: Row 0 has tiles t1(1pt), t2(2pt), t3(3pt), t4(4pt). Threshold=5. Player has no capability requirements.

1. Furthest row = 0
2. Combinations: [t1,t4], [t2,t3], [t1,t2,t3], etc. Optimal = shortest time (e.g. [t2,t3])
3. FindTaskInTiles([t2,t3]): t3(3pt) before t2(2pt) → try t3
4. FindEligibleRule(t3): rule exists, activity valid → return (activityId, rule)
5. Player is assigned to work on t3

**Fallback**: If t3 required capability "elite" and player lacks it:
- FindTaskInTiles skips t3, tries t2 → same if t2 also requires "elite"
- FindFallbackTask → FindTaskInRow(0) → tries t4, t3, t2, t1 in that order
- FindTaskInAllRows if row 0 fully completed

## Performance Considerations

- **Combination cache**: `GetCombinationsForRow` caches per row; no recomputation
- **Optimal selection**: Single `OrderBy` over combinations (typically &lt;20)
- **Tile iteration**: Linear over row tiles; rows usually have 4–8 tiles
- **FindEligibleRule**: Linear over rules per tile (typically 1–3)

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs` | Implemented `SelectTaskForPlayer` and 5 helpers |
| `Tests/BingoSim.Application.UnitTests/Simulation/RowUnlockingStrategyRegistrationTests.cs` | Added `SelectTaskForPlayer_PlayerCanWorkOnTile_ReturnsTask`, `SelectTaskForPlayer_AllTilesCompleted_ReturnsNull`, `BuildTaskSelectionContextWithWorkableTile` |

## Challenges Encountered

1. **Determinism for optimal combination**: When `EstimatedCompletionTime` ties, added `ThenBy(tile keys)` so the same combination is always chosen.
2. **FindTaskInAllRows and TileRowIndex**: Some tiles might not be in `TileRowIndex` if built from a different source; used `GetValueOrDefault(t.Key, -1)` to avoid exceptions.
3. **Tuple return type**: `FindEligibleRule` returns `(Guid, TileActivityRuleSnapshotDto)?`; callers unwrap to `(Guid?, TileActivityRuleSnapshotDto?)?` for consistency.
