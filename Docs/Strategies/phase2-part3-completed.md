# Phase 2 Part 3: Implement Tile Combination Calculator — Completed

## Summary

Created `TileCombination` and `RowCombinationCalculator` to find all valid tile combinations that meet the row unlock threshold. Added caching and `GetCombinationsForRow` to `RowUnlockingStrategy`.

## Algorithm Implementation Details

### Backtracking Approach

1. **Ordering**: Tiles are sorted by points ascending, then by key for determinism.
2. **Recursion**: For each tile at `currentIndex`, we either include it or skip it.
3. **Recording**: A combination is added only when **including** a tile pushes `currentSum >= threshold`. This avoids duplicates that occurred when adding at the start of every recursive call (the "skip" path would re-enter with the same combination).
4. **Pruning**: Combinations are limited to `MaxTilesPerCombination` (8) tiles.
5. **Base case**: Return when `currentIndex >= tiles.Count` or combination size limit reached.

### Duplicate Prevention

The initial implementation added combinations at the start of every call when `currentSum >= threshold`. This caused duplicates: when we "skip" a tile and recurse, the recursive call had the same combination and would add it again. The fix: add only when we **include** a tile that pushes us over the threshold.

### Time Complexity

- **Worst case**: O(2^n) for n tiles (each tile included or skipped).
- **Typical**: Rows have 4–8 tiles; 2^8 = 256 recursive paths is negligible (< 1 ms).

## Files Created

| File | Description |
|------|-------------|
| `BingoSim.Application/Simulation/Strategies/TileCombination.cs` | Holds `TileKeys`, `TotalPoints`, `EstimatedCompletionTime` (for next phase) |
| `BingoSim.Application/Simulation/Strategies/RowCombinationCalculator.cs` | Static class with `CalculateCombinations` and backtracking logic |
| `Tests/BingoSim.Application.UnitTests/Simulation/Strategies/RowCombinationCalculatorTests.cs` | 7 unit tests |

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs` | Added `_combinationCache`, `GetCombinationsForRow` helper |

## Test Results

| Test | Result |
|------|--------|
| `CalculateCombinations_EmptyTiles_ReturnsEmpty` | Pass |
| `CalculateCombinations_ThresholdZero_ReturnsEmpty` | Pass |
| `CalculateCombinations_FourTilesThreshold5_FindsValidCombinations` | Pass |
| `CalculateCombinations_AllOnesThreshold5_FindsFiveTileCombination` | Pass |
| `CalculateCombinations_SingleTileMeetsThreshold_ReturnsThatTile` | Pass |
| `CalculateCombinations_NoValidCombination_ReturnsEmpty` | Pass |
| `CalculateCombinations_NoDuplicateCombinations` | Pass |

All 15 RowUnlocking + RowCombination tests passed.

## Example Combinations (Sample Board)

For tiles `{t1:1, t2:2, t3:3, t4:4}` with threshold 5:

| Combination | TotalPoints |
|-------------|-------------|
| [t1, t4] | 5 |
| [t2, t3] | 5 |
| [t1, t2, t3] | 6 |
| [t1, t2, t4] | 7 |
| [t1, t3, t4] | 8 |
| [t2, t3, t4] | 9 |
| [t1, t2, t3, t4] | 10 |

## Integration with RowUnlockingStrategy

```csharp
private readonly Dictionary<int, List<TileCombination>> _combinationCache = new();

private List<TileCombination> GetCombinationsForRow(
    int rowIndex,
    EventSnapshotDto snapshot,
    int threshold)
{
    if (_combinationCache.TryGetValue(rowIndex, out var cached))
        return cached;

    var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
    if (row is null)
        return [];

    var tiles = row.Tiles.ToDictionary(t => t.Key, t => t.Points);
    var combinations = RowCombinationCalculator.CalculateCombinations(tiles, threshold);
    _combinationCache[rowIndex] = combinations;
    return combinations;
}
```

- **Cache key**: Row index.
- **Cache value**: List of `TileCombination` for that row.
- **Usage**: Will be used in `SelectTaskForPlayer` (Phase 2.4) to pick the optimal combination.

## Optimization Decisions

1. **Max 8 tiles per combination**: Avoids huge combinations on wide rows; 8 tiles is enough for typical unlock thresholds.
2. **Sort by points**: Improves pruning and keeps output order consistent.
3. **Add only on include**: Fixes duplicate combinations without extra deduplication.
4. **Instance-level cache**: Strategy is a singleton per factory; cache is shared across teams in a run. Row structure is the same for all teams.
