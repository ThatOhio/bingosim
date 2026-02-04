# Greedy Strategy Implementation

This document describes the implementation of the Greedy strategy for BingoSim, including design decisions, code snippets, edge cases, and comparison to Row Unlocking.

---

## 1. Implementation Approach

The Greedy strategy prioritizes tiles purely by point value, without considering row unlock optimization. Both task selection and grant allocation use identical logic:

1. **Primary sort:** Points (descending) — highest point tiles first
2. **Secondary sort:** Estimated completion time (ascending) — fastest tiles first as tie-breaker
3. **Tertiary sort:** Tile key (alphabetical) — deterministic when points and time match

This is simpler than Row Unlocking, which computes optimal tile combinations for unlocking the next row and prioritizes the furthest row.

---

## 2. GrantAllocationContext Modification

**Decision:** Added `EventSnapshot` to `GrantAllocationContext`.

**Rationale:** The Greedy strategy uses `TileCompletionEstimator` for tie-breaking in both `SelectTaskForPlayer` and `SelectTargetTileForGrant`. For grant allocation, we need to look up tiles by key and compute their estimated completion times. The `GrantAllocationContext` originally had no reference to the event snapshot, so we added it.

**Changes:**
- `GrantAllocationContext.cs`: Added `public required EventSnapshotDto EventSnapshot { get; init; }`
- `SimulationRunner.cs`: Passes `EventSnapshot = snapshot` when constructing the context
- `RowUnlockingStrategyRegistrationTests.cs`: Added `BuildMinimalEventSnapshot()` helper and `EventSnapshot` to all test contexts

---

## 3. Code Snippets

### 3.1 Strategy Catalog

```csharp
// BingoSim.Application/StrategyKeys/StrategyCatalog.cs
public const string Greedy = "Greedy";
private static readonly string[] AllKeys = [RowUnlocking, Greedy];
```

### 3.2 SelectTaskForPlayer

```csharp
public (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context)
{
    if (context.UnlockedRowIndices.Count == 0)
        return null;

    var availableTiles = context.EventSnapshot.Rows
        .Where(r => context.UnlockedRowIndices.Contains(r.Index))
        .SelectMany(r => r.Tiles)
        .Where(t => !context.CompletedTiles.Contains(t.Key))
        .ToList();

    if (availableTiles.Count == 0)
        return null;

    var tilesWithEstimates = availableTiles
        .Select(tile => new
        {
            Tile = tile,
            EstimatedTime = TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot)
        })
        .ToList();

    var sortedTiles = tilesWithEstimates
        .OrderByDescending(t => t.Tile.Points)
        .ThenBy(t => t.EstimatedTime)
        .ThenBy(t => t.Tile.Key, StringComparer.Ordinal);

    foreach (var item in sortedTiles)
    {
        var eligibleRule = FindEligibleRule(context, item.Tile);
        if (eligibleRule.HasValue)
            return (eligibleRule.Value.activityId, eligibleRule.Value.rule);
    }

    return null;
}
```

### 3.3 SelectTargetTileForGrant

```csharp
public string? SelectTargetTileForGrant(GrantAllocationContext context)
{
    if (context.EligibleTileKeys.Count == 0)
        return null;

    var tilesWithEstimates = context.EligibleTileKeys
        .Select(key => new
        {
            TileKey = key,
            Points = context.TilePoints[key],
            EstimatedTime = GetEstimatedCompletionTime(key, context)
        })
        .ToList();

    return tilesWithEstimates
        .OrderByDescending(t => t.Points)
        .ThenBy(t => t.EstimatedTime)
        .ThenBy(t => t.TileKey, StringComparer.Ordinal)
        .First()
        .TileKey;
}
```

### 3.4 Helper: FindEligibleRule

```csharp
private static (Guid activityId, TileActivityRuleSnapshotDto rule)? FindEligibleRule(
    TaskSelectionContext context,
    TileSnapshotDto tile)
{
    foreach (var rule in tile.AllowedActivities)
    {
        if (rule.RequirementKeys.Count > 0 &&
            !rule.RequirementKeys.All(context.PlayerCapabilities.Contains))
            continue;

        var activity = context.EventSnapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
        if (activity is null || activity.Attempts.Count == 0)
            continue;

        return (rule.ActivityDefinitionId, rule);
    }
    return null;
}
```

### 3.5 Helper: GetEstimatedCompletionTime

```csharp
private static double GetEstimatedCompletionTime(string tileKey, GrantAllocationContext context)
{
    var tile = FindTileByKey(tileKey, context.EventSnapshot);
    return tile is null
        ? double.MaxValue
        : TileCompletionEstimator.EstimateCompletionTime(tile, context.EventSnapshot);
}
```

---

## 4. Edge Cases Handled

| Edge Case | Behavior |
|-----------|----------|
| No unlocked rows | Returns `null` from both methods |
| All tiles completed | Returns `null` from both methods |
| Player has no capabilities | `SelectTaskForPlayer` returns `null` (no eligible rule) |
| Player lacks required capability for a tile | Tile is skipped; next tile in sort order is tried |
| Single tile available | Selects it |
| Multiple tiles with same points and same estimated time | Deterministic selection by tile key (alphabetical) |
| Tile not found in snapshot (grant context) | Uses `double.MaxValue` so it sorts last |
| Empty `EligibleTileKeys` | Returns `null` |
| Activity has no attempts | Rule is skipped |

---

## 5. Testing Approach and Results

### 5.1 Test Coverage

- **StrategyCatalog_ContainsGreedy** — Catalog includes Greedy key
- **Factory_GetStrategy_Greedy_ReturnsGreedyStrategy** — Factory returns correct type
- **SelectTargetTileForGrant_NoEligible_ReturnsNull** — Empty eligible list
- **SelectTargetTileForGrant_HighestPoint_Selected** — 4-point tile chosen over 2 and 3
- **SelectTargetTileForGrant_TieBreakByTileKey_Deterministic** — "a" chosen over "x" when both 4 points
- **SelectTargetTileForGrant_CrossRow_SelectsHighestPointRegardlessOfRow** — Row-0 4-point over row-1 2-point
- **SelectTaskForPlayer_NoUnlockedRows_ReturnsNull** — No unlocked rows
- **SelectTaskForPlayer_AllTilesCompleted_ReturnsNull** — All tiles done
- **SelectTaskForPlayer_PlayerCanWorkOnTile_ReturnsTask** — Returns valid task
- **SelectTaskForPlayer_HighestPointTile_Selected** — t2 (4 pts) chosen over t1 (2) and t3 (3)
- **Simulation_WithGreedyTeam_DoesNotCrash** — Full simulation run with Greedy team

### 5.2 Results

All 11 GreedyStrategy tests pass. Full test suite (including RowUnlocking and other tests) passes.

---

## 6. Comparison to Row Unlocking Strategy

| Aspect | Greedy | Row Unlocking |
|--------|--------|----------------|
| **Philosophy** | Maximize points immediately | Unlock next row as quickly as possible |
| **Task selection** | Highest point tile, then fastest completion | Optimal combination for row unlock, then furthest row, then fallback |
| **Grant allocation** | Highest point among eligible | Highest point on furthest row, then fallback |
| **Tie-breaking** | Completion time, then key | Key (alphabetical) |
| **Complexity** | Simple: sort and pick | Complex: combination calculation, row prioritization |
| **Use case** | Straightforward point maximization | Competitive row-unlock races |

**When to use Greedy:** When you want simple, predictable behavior focused on immediate point gains without row-unlock optimization.

**When to use Row Unlocking:** When unlocking the next row quickly is more important than raw point accumulation (e.g., races where row unlocks matter).

---

## 7. Challenges and Findings

1. **GrantAllocationContext extension:** The original context lacked `EventSnapshot`, which was needed for completion-time tie-breaking. Adding it required updates to `SimulationRunner` and all existing tests that construct `GrantAllocationContext`.

2. **Tile lookup for grants:** Eligible tiles are provided as keys; we need the full `TileSnapshotDto` for `TileCompletionEstimator`. Added `FindTileByKey` to search rows in the snapshot.

3. **Test assertion for task selection:** When multiple tiles share the same activity, we cannot infer which tile was selected from the returned `(activityId, rule)`. The highest-point test uses distinct activities per tile so we can map the returned activity ID back to the selected tile.

4. **Determinism:** Using `StringComparer.Ordinal` for tile keys ensures consistent ordering across runs.
