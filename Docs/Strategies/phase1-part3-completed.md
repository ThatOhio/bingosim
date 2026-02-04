# Phase 1 Part 3: Integrate Strategy-Driven Task Selection — Completed

## Summary

Replaced the fixed `GetFirstEligibleActivity` logic in `SimulationRunner` with strategy-driven task selection. Strategies now control which tile/activity each player works on via `SelectTaskForPlayer`.

## Changes to SimulationRunner

### 1. GetFirstEligibleActivity → GetTaskForPlayer

- **Renamed** `GetFirstEligibleActivity` to `GetTaskForPlayer`
- **New parameters**: `ITeamStrategy strategy`, `TeamRunState state`, and tile dictionaries (`tileRowIndex`, `tilePoints`, `tileRequiredCount`, `tileToRules`)
- **Removed** fixed iteration logic
- **New behavior**: Builds `TaskSelectionContext`, calls `strategy.SelectTaskForPlayer(context)`, returns result or `(null, null)` when strategy returns null

### 2. BuildTaskSelectionContext Helper

Added private static method:

```csharp
private static TaskSelectionContext BuildTaskSelectionContext(
    EventSnapshotDto snapshot,
    TeamSnapshotDto team,
    int playerIndex,
    TeamRunState state,
    HashSet<string> playerCapabilities,
    IReadOnlyDictionary<string, int> tileRowIndex,
    IReadOnlyDictionary<string, int> tilePoints,
    IReadOnlyDictionary<string, int> tileRequiredCount,
    IReadOnlyDictionary<string, List<TileActivityRuleSnapshotDto>> tileToRules)
```

- Converts `tileToRules` to `IReadOnlyDictionary<string, IReadOnlyList<TileActivityRuleSnapshotDto>>` for `TaskSelectionContext`
- Populates all required context properties from `state`, `snapshot`, `team`, and tile dictionaries

### 3. ScheduleEventsForPlayers Updates

- Obtains strategy via `strategyFactory.GetStrategy(team.StrategyKey)` at the start
- Replaces `GetFirstEligibleActivity(...)` with `GetTaskForPlayer(strategy, snapshot, team, pi, state, caps, tileRowIndex, tilePoints, tileRequiredCount, tileToRules)`
- Passes tile dictionaries through to `GetTaskForPlayer` for context building

## Updated Strategy Implementations

### RowRushAllocator.SelectTaskForPlayer

- **Logic**: Rows in order (lowest first), tiles by points (lowest first) — same as original `GetFirstEligibleActivity`
- Iterates `context.EventSnapshot.Rows`, then `row.Tiles.OrderBy(t => t.Points)`
- Skips completed tiles and rows not in `UnlockedRowIndices`
- Checks `rule.RequirementKeys` against `context.PlayerCapabilities`
- Returns first eligible `(activityId, rule)` or null

### GreedyPointsAllocator.SelectTaskForPlayer

- **Logic**: Rows in order (lowest first), tiles by points (highest first) — different from RowRush
- Uses `row.Tiles.OrderByDescending(t => t.Points)` to prefer high-point tiles
- Same eligibility checks as RowRush
- Returns first eligible `(activityId, rule)` or null

## Test Results

- **Build**: Succeeded (0 errors, 0 warnings)
- **Unit tests**: 89 Application simulation tests passed, including:
  - RowRushAllocatorTests (3 tests)
  - GreedyPointsAllocatorTests (3 tests)
  - SimulationRunnerReproducibilityTests
  - GroupPlaySimulationTests
  - ScheduleSimulationIntegrationTests
  - ModifierSimulationIntegrationTests
  - GroupPlayIntegrationTests

## Behavioral Notes

- **RowRush**: Task selection matches previous fixed logic (lowest row → lowest points). Simulation results for RowRush teams are unchanged.
- **GreedyPoints**: Task selection now prefers high-point tiles within each row, so GreedyPoints teams may produce different results than before.
- **Null handling**: When a strategy returns null, the player is skipped (no assignment), preserving existing behavior.

## Issues Encountered

1. **Type conversion**: `TaskSelectionContext.TileToRules` expects `IReadOnlyDictionary<string, IReadOnlyList<T>>` but `SimulationRunner` uses `Dictionary<string, List<T>>`. Resolved by building a converted dictionary in `BuildTaskSelectionContext` via `ToDictionary` with an explicit cast.
