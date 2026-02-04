# Phase 2 Part 1: Create Row Unlocking Strategy Shell — Completed

## Summary

Created the `RowUnlockingStrategy` class structure with skeleton implementations, added it to the strategy catalog and factory, and verified registration and invocation.

## New Files Created

| File | Description |
|------|-------------|
| `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs` | Strategy shell implementing `ITeamStrategy`; both methods return null (TODO for next phase) |
| `Docs/Strategies/row-unlocking-implementation-plan.md` | Detailed implementation plan for task selection, grant allocation, caching, and data structures |
| `Tests/BingoSim.Application.UnitTests/Simulation/RowUnlockingStrategyRegistrationTests.cs` | Tests for catalog, factory, and simulation with RowUnlocking team |

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/StrategyKeys/StrategyCatalog.cs` | Added `RowUnlocking` constant and included it in `AllKeys` |
| `BingoSim.Application/Simulation/Allocation/TeamStrategyFactory.cs` | Registered `RowUnlockingStrategy`; updated XML doc to mention RowUnlocking |

## Strategy Registration Details

- **Key**: `"RowUnlocking"`
- **Factory**: `[StrategyCatalog.RowUnlocking] = new RowUnlockingStrategy()`
- **Namespace**: `BingoSim.Application.Simulation.Strategies` (new folder)

## Default Fallback Decision

**Decision**: Keep RowRush as the default fallback for unknown strategy keys.

**Rationale**:
- RowRush is the baseline strategy and has been the default since inception
- It is simple and predictable
- Changing the default could affect existing configurations that use typos or legacy keys
- RowUnlocking is new and not yet fully implemented; it should not be the default until it is production-ready

## Testing Results

| Test | Result |
|------|--------|
| `StrategyCatalog_ContainsRowUnlocking` | Pass |
| `Factory_GetStrategy_RowUnlocking_ReturnsRowUnlockingStrategy` | Pass |
| `RowUnlockingStrategy_SelectTargetTileForGrant_DoesNotThrow` | Pass |
| `RowUnlockingStrategy_SelectTaskForPlayer_DoesNotThrow` | Pass |
| `Simulation_WithRowUnlockingTeam_DoesNotCrash` | Pass |

- **Build**: Succeeded (0 errors, 0 warnings)
- **Simulation**: A run with a team using RowUnlocking completes without crashing; the team makes no progress (both methods return null) but the simulation flow remains intact

## Implementation Plan Document

See [row-unlocking-implementation-plan.md](./row-unlocking-implementation-plan.md) for:

- Task selection algorithm (optimal combination, fallbacks)
- Grant allocation algorithm (filter by furthest row, fallbacks)
- Caching strategy (per-row cache, invalidation)
- Data structures (OptimalCombination, helper methods)
- Phased implementation plan (2.2–2.5)

## Architectural Notes

- **Strategies folder**: `RowUnlockingStrategy` lives in `Simulation/Strategies/` to separate it from the simpler allocators in `Allocation/`. Future strategies can follow this pattern.
- **Null safety**: Returning null from both methods is safe: `SelectTaskForPlayer` → player skipped; `SelectTargetTileForGrant` → grant dropped. No crashes or null reference exceptions.
