# ComboUnlocking Strategy Testing Results

Comprehensive test results and validation for the ComboUnlocking strategy.

---

## 1. Test Results Summary

### Unit Tests (ComboUnlockingStrategyTests)

| Test | Status | Description |
|------|--------|-------------|
| StrategyCatalog_ContainsComboUnlocking | ✅ Pass | Catalog includes ComboUnlocking key |
| Factory_GetStrategy_ComboUnlocking_ReturnsComboUnlockingStrategy | ✅ Pass | Factory returns correct strategy type |
| SelectTargetTileForGrant_NoEligible_ReturnsNull | ✅ Pass | Returns null when no eligible tiles |
| SelectTargetTileForGrant_Phase1_MultipleTilesOnFurthestRow_SelectsHighestPoint | ✅ Pass | Phase 1 grant: highest point on furthest row |
| SelectTargetTileForGrant_Phase2_AllRowsUnlocked_SelectsHighestPoint | ✅ Pass | Phase 2 grant: highest point anywhere |
| SelectTaskForPlayer_NoUnlockedRows_ReturnsNull | ✅ Pass | Returns null when no rows unlocked |
| SelectTaskForPlayer_Phase1_PlayerCanWorkOnTile_ReturnsTask | ✅ Pass | Phase 1 task selection works |
| SelectTaskForPlayer_Phase2_AllRowsUnlocked_PlayerCanWorkOnTile_ReturnsTask | ✅ Pass | Phase 2 task selection works |
| SelectTaskForPlayer_AllTilesCompleted_ReturnsNull | ✅ Pass | Returns null when all tiles done |
| InvalidateCacheForRow_ClearsCache | ✅ Pass | Cache invalidation works |
| Simulation_WithComboUnlockingTeam_DoesNotCrash | ✅ Pass | Full simulation completes |
| Simulation_WithComboUnlocking_MultiRowUnlock_CacheInvalidationWorks | ✅ Pass | Row unlock triggers cache invalidation |

### Scenario Tests (ComboUnlockingStrategyScenarioTests)

| Test | Status | Description |
|------|--------|-------------|
| Phase1_NoLockedShares_BehavesLikeRowUnlocking | ✅ Pass | Unique activities → RowUnlocking-like behavior |
| Phase1_PrefersTilesWithoutLockedShares | ✅ Pass | Selects uniqueC (Activity3) over shared-activity tiles |
| Phase1_AllRowsUnlocked_DetectsCorrectly | ✅ Pass | Phase detection correct |
| Phase2_SingleRow_ImmediatelyUsesPhase2 | ✅ Pass | Single-row event uses Phase 2 from start |
| Phase2_PrefersTilesWithMoreSharedActivities | ✅ Pass | Selects sharedA (virtual 5) over soloE (virtual 4) |
| Phase2_AllTilesCompleted_ReturnsNull | ✅ Pass | Returns null when all complete |
| ThreeWay_ActivityOverlapBoard_ComboUnlockingMayOutperform | ✅ Pass | Three-way simulation completes |
| EdgeCase_SingleTileOnRow_Works | ✅ Pass | Single tile per row handled |
| EdgeCase_PlayerWithNoValidTiles_ReturnsNull | ✅ Pass | Capability filtering works |
| EdgeCase_Phase2_NoSharedActivities_BehavesLikeGreedy | ✅ Pass | Unique activities → Greedy-like Phase 2 |

### Integration Tests (StrategyComparisonIntegrationTests)

| Test | Status | Description |
|------|--------|-------------|
| ThreeWay_RowUnlocking_Greedy_ComboUnlocking_AllComplete | ✅ Pass | All three strategies complete successfully |

---

## 2. Behavioral Analysis

### Phase 1: Penalty-Driven Selection

When row 0 is unlocked and rows 1–2 are locked:

- **Tiles with unique activities (no locked shares):** Penalty = baseTime × 1
- **Tiles sharing activities with locked rows:** Penalty = baseTime × (1 + lockedShareCount)

ComboUnlocking selects the combination with the **lowest total penalized time**, then assigns the player to the **highest-point tile** in that combination.

**Observed:** In `Phase1_PrefersTilesWithoutLockedShares`, the strategy correctly selects `uniqueC` (Activity3, 4pts) because the combination [uniqueC, sharedD] has lower penalized time than [sharedA, sharedB].

### Phase 2: Shared Activity Maximization

When all rows are unlocked:

- **Virtual score** = points + (1 × shared incomplete tile count)
- Sort: virtual score (desc) → completion time (asc) → tile key (asc)

**Observed:** In `Phase2_PrefersTilesWithMoreSharedActivities`, sharedA (2pts, 3 shares) has virtual score 5 and is selected over soloE (4pts, 0 shares) with virtual score 4.

### Phase Transition

- **Single-row event:** Phase 2 from the start (all rows unlocked immediately)
- **Multi-row event:** Phase 1 until last row unlocks, then Phase 2

---

## 3. Scenario Analysis

### Scenario 1: Heavy Activity Overlap

**Setup:** Row 0 has tiles with Activity1, Activity2, Activity3; rows 1–2 have tiles with Activity1, Activity2.

**Result:** ComboUnlocking prefers Activity3 tiles (unique to row 0) to avoid burning Activity1/Activity2 for locked rows.

### Scenario 2: No Activity Overlap

**Setup:** Each tile has a unique activity (or all share one activity across all rows).

**Result:** Phase 1 behaves like RowUnlocking. Phase 2 behaves like Greedy (no shared-activity bonus).

### Scenario 3: High-Point Tiles Share Activities

**Setup:** 4pt tiles share activities with locked rows; 1pt tiles have unique activities.

**Result:** ComboUnlocking deprioritizes 4pt tiles initially. Greedy would complete 4pt tiles first and may suffer later.

### Scenario 4: Player Capability Restrictions

**Setup:** Tile requires capability "cap1"; player has no capabilities.

**Result:** Returns null. Fallback logic does not bypass capability checks.

---

## 4. Cache Performance

### Cache Invalidation

- **Row unlock:** `InvalidateCacheForRow` called for each newly unlocked row
- **Tile completion:** No separate invalidation (row unlock covers the main case)
- **Wiring:** SimulationRunner detects newly unlocked rows and notifies strategies

### Cache Behavior

- **Base combination cache:** Keyed by row index; static during run
- **Penalized combination cache:** Keyed by row index; invalidated when row unlocks
- **Reuse:** Cache is reused when no state changes between calls

---

## 5. Phase Transition

### Immediate Transition (Single Row)

- Event with 1 row → all rows unlocked at start
- Phase 2 logic used from first task selection
- Shared activity maximization applies

### Mid-Simulation Transition

- Multi-row event: Phase 1 until last row unlocks
- Task selection switches from penalized combinations to virtual score
- Grant allocation switches from row-focused to points-focused

### Behavioral Flip

- **Phase 1:** Prefer tiles that do *not* share activities with locked tiles
- **Phase 2:** Prefer tiles that *do* share activities with incomplete tiles
- Same underlying idea: maximize activity efficiency

---

## 6. Edge Cases

| Edge Case | Result |
|-----------|--------|
| All combinations heavily penalized | Selects least penalized combination |
| No incomplete tiles with shared activities | Phase 2 behaves like Greedy |
| Tile with many shared activities | Bonus applied correctly (1pt per share) |
| Empty row | Handled (no tiles to select) |
| Single tile on row | Works correctly |
| Player with no valid tiles | Returns null |

---

## 7. Strategy Selection Decision Tree

```
Does the board have significant activity overlap?
├── Yes → Use ComboUnlocking
└── No  → Row progression or total points?
          ├── Row progression → RowUnlocking
          └── Total points → Greedy (or RowUnlocking if high-point tiles are slow)
```

---

## 8. Performance Comparison

| Strategy | Relative Complexity | Caching | Typical Throughput |
|----------|---------------------|---------|--------------------|
| Greedy | Lowest | None | Fastest |
| RowUnlocking | Medium | Combination cache | Medium |
| ComboUnlocking | Highest | Combination + penalized cache | Medium (comparable to RowUnlocking) |

Cache invalidation adds minimal overhead (one HashSet + Except per grant that completes a tile).

---

## 9. Recommendations

### Default Strategy

- **Boards with activity overlap:** ComboUnlocking
- **Boards with minimal overlap:** RowUnlocking or Greedy
- **Baseline/debugging:** Greedy

### When to Override

- Use Greedy for short events or when simplicity is more important
- Use RowUnlocking when activity overlap is low and row progression matters

### Configuration

- No strategy-specific configuration (ParamsJson) in v1
- Strategy key only: `StrategyCatalog.ComboUnlocking`

---

## 10. Known Issues and Future Work

### Limitations

- ComboUnlocking assumes activities are the bottleneck (may not always hold)
- Penalty calculation does not account for player skill multipliers
- Phase 2 bonus is static (1pt per share); could be tunable
- No real-time adaptation based on team progress

### Future Enhancements

- Tunable penalty and bonus multipliers
- Hybrid strategies (e.g., "mostly ComboUnlocking but prefer high points")
- Dynamic phase detection (not just "all rows unlocked")
- Multi-team coordination (if teams share activity pool)
- Machine learning to optimize penalty/bonus weights

---

## 11. Sign-off

### Deployment Readiness

- ✅ All unit tests pass
- ✅ Scenario tests pass
- ✅ Integration tests pass
- ✅ Cache invalidation wired
- ✅ Phase transition correct
- ✅ Edge cases handled

### Monitoring Recommendations

- Track strategy usage per event
- Compare TotalPoints and RowReached across strategies for same events
- Monitor simulation run times by strategy
- Log cache hit/miss rates in debug builds if needed
