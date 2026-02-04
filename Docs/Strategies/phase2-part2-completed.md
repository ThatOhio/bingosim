# Phase 2 Part 2: Implement Grant Allocation Logic for Row Unlocking Strategy — Completed

## Summary

Implemented `SelectTargetTileForGrant` in `RowUnlockingStrategy` according to the specification: prioritize highest point tiles on the furthest unlocked row, with fallback to highest point tile anywhere.

## Implementation Details

### Final Implementation

```csharp
public string? SelectTargetTileForGrant(GrantAllocationContext context)
{
    if (context.EligibleTileKeys.Count == 0)
        return null;

    var furthestUnlockedRow = GetFurthestUnlockedRow(context.UnlockedRowIndices);

    var tilesOnFurthestRow = context.EligibleTileKeys
        .Where(key => context.TileRowIndex[key] == furthestUnlockedRow)
        .ToList();

    if (tilesOnFurthestRow.Count > 0)
    {
        return tilesOnFurthestRow
            .OrderByDescending(key => context.TilePoints[key])
            .ThenBy(key => key, StringComparer.Ordinal)
            .First();
    }

    return context.EligibleTileKeys
        .OrderByDescending(key => context.TilePoints[key])
        .ThenBy(key => context.TileRowIndex[key])
        .ThenBy(key => key, StringComparer.Ordinal)
        .First();
}

private static int GetFurthestUnlockedRow(IReadOnlySet<int> unlockedRowIndices)
{
    return unlockedRowIndices.Count > 0 ? unlockedRowIndices.Max() : 0;
}
```

### Logic Flow

1. **Early exit**: Return null if no eligible tiles.
2. **Furthest row**: Use `GetFurthestUnlockedRow` to get the max unlocked row index (0 if empty).
3. **Primary**: Filter eligible tiles to those on the furthest row; select highest points, tie-break by tile key.
4. **Fallback**: If no tiles on furthest row, select from all eligible tiles by highest points, then row index, then tile key.

### Helper Method

`GetFurthestUnlockedRow` is extracted for reuse in task selection (Phase 2.3). Returns 0 when the set is empty (defensive; in practice row 0 is always unlocked at sim start).

## Edge Cases Handled

| Edge Case | Handling |
|-----------|----------|
| Empty eligible tiles | Return null immediately |
| Single unlocked row (row 0 only) | `GetFurthestUnlockedRow` returns 0; primary path filters to row 0 tiles |
| All tiles on furthest row completed | Eligible tiles exclude completed; if none on furthest row, fallback selects from earlier rows |
| Ties in point values | Primary: `ThenBy(key, StringComparer.Ordinal)`. Fallback: `ThenBy(TileRowIndex)` then `ThenBy(key)` |
| Empty `UnlockedRowIndices` | `GetFurthestUnlockedRow` returns 0; `TileRowIndex` lookup may fail only if eligible tiles exist for a non-unlocked row (runner pre-filters, so this should not occur) |

## Testing

### Tests Added

| Test | Scenario | Expected |
|------|----------|----------|
| `SelectTargetTileForGrant_NoEligible_ReturnsNull` | Empty `EligibleTileKeys` | null |
| `SelectTargetTileForGrant_MultipleTilesOnFurthestRow_SelectsHighestPoint` | a(0,4), b(1,2), c(1,4); furthest row 1 | "c" |
| `SelectTargetTileForGrant_OnlyTilesOnEarlierRows_FallbackToHighestPoint` | a(0,1), b(0,4); furthest row 1, none on row 1 | "b" |
| `SelectTargetTileForGrant_TieBreakByTileKey` | x(0,4), a(0,4); same row, same points | "a" |

### Test Results

- All 8 RowUnlockingStrategy tests passed (4 grant allocation + 4 registration/simulation).
- Build: Succeeded (0 errors, 0 warnings).

## Deviations from Original Plan

None. Implementation matches the specification. The fallback tie-break uses `TileRowIndex` then `TileKey` (rather than tile key only) to prefer lower rows when points tie; this is a reasonable secondary criterion and was included in the implementation.

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs` | Implemented `SelectTargetTileForGrant`; added `GetFurthestUnlockedRow` helper; added XML docs |
| `Tests/BingoSim.Application.UnitTests/Simulation/RowUnlockingStrategyRegistrationTests.cs` | Replaced placeholder test with 4 grant allocation tests |
