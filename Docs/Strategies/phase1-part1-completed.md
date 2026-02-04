# Phase 1 Part 1: Rename and Expand Strategy Interface — Completed

## Summary

Renamed `IProgressAllocator` to `ITeamStrategy`, added `SelectTaskForPlayer` method, introduced `TaskSelectionContext`, and renamed `AllocatorContext` to `GrantAllocationContext`. All references updated across the codebase.

## Files Renamed

| Old Path | New Path |
|----------|----------|
| `BingoSim.Application/Simulation/Allocation/IProgressAllocator.cs` | **Deleted** — replaced by `ITeamStrategy.cs` |
| `BingoSim.Application/Simulation/Allocation/AllocatorContext.cs` | **Deleted** — replaced by `GrantAllocationContext.cs` |

## Files Created

| File | Description |
|------|--------------|
| `BingoSim.Application/Simulation/Allocation/ITeamStrategy.cs` | New interface with `SelectTargetTileForGrant` and `SelectTaskForPlayer` |
| `BingoSim.Application/Simulation/Allocation/TaskSelectionContext.cs` | Context for task selection: player index, capabilities, event/team snapshots, tile state |
| `BingoSim.Application/Simulation/Allocation/GrantAllocationContext.cs` | Renamed from AllocatorContext; context for grant allocation only |

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Allocation/RowRushAllocator.cs` | Implements `ITeamStrategy`; `SelectTargetTile` → `SelectTargetTileForGrant`; added placeholder `SelectTaskForPlayer` returning null |
| `BingoSim.Application/Simulation/Allocation/GreedyPointsAllocator.cs` | Same as RowRushAllocator |
| `BingoSim.Application/Simulation/Allocation/IProgressAllocatorFactory.cs` | `IProgressAllocator` → `ITeamStrategy` in return type |
| `BingoSim.Application/Simulation/Allocation/ProgressAllocatorFactory.cs` | Dictionary and return type use `ITeamStrategy` |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | `AllocatorContext` → `GrantAllocationContext`; `SelectTargetTile` → `SelectTargetTileForGrant` |
| `Tests/BingoSim.Application.UnitTests/Simulation/RowRushAllocatorTests.cs` | Updated context type and method names in all 3 tests |
| `Tests/BingoSim.Application.UnitTests/Simulation/GreedyPointsAllocatorTests.cs` | Updated context type and method names in all 3 tests |

## Files Not Modified (Per Prompt)

- `BingoSim.Infrastructure/DependencyInjection.cs` — still registers `IProgressAllocatorFactory` (to be renamed in next prompt)
- `ProgressAllocatorFactory.cs` / `IProgressAllocatorFactory.cs` — factory names unchanged (to be renamed in next prompt)

## TaskSelectionContext Properties

- `PlayerIndex` — 0-based player index within team
- `PlayerCapabilities` — `IReadOnlySet<string>` for capability lookup
- `EventSnapshot` — full event snapshot reference
- `TeamSnapshot` — team snapshot reference
- `UnlockedRowIndices` — row indices unlocked for the team
- `TileProgress` — tile key → current progress
- `TileRequiredCount` — tile key → required count
- `CompletedTiles` — set of completed tile keys
- `TileRowIndex` — tile key → row index
- `TilePoints` — tile key → points (1–4)
- `TileToRules` — tile key → activity rules

## Issues Encountered

None. Build and tests completed successfully.

## Build and Test Results

- **Build**: Succeeded (0 errors, 0 warnings)
- **Tests**: All passed (Application unit tests: 201 passed; allocator tests updated and passing)
