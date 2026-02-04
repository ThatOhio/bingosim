# Phase 1 Part 2: Rename and Update Strategy Factory — Completed

## Summary

Renamed the factory interface and implementation to reflect `ITeamStrategy` naming. Updated `SimulationRunner`, DI registration, and all direct usages in tests and Seed.

## Files Renamed

| Old Path | New Path |
|----------|----------|
| `BingoSim.Application/Simulation/Allocation/IProgressAllocatorFactory.cs` | **Deleted** — replaced by `ITeamStrategyFactory.cs` |
| `BingoSim.Application/Simulation/Allocation/ProgressAllocatorFactory.cs` | **Deleted** — replaced by `TeamStrategyFactory.cs` |

## Files Created

| File | Description |
|------|-------------|
| `BingoSim.Application/Simulation/Allocation/ITeamStrategyFactory.cs` | Factory interface with `GetStrategy(string strategyKey)` |
| `BingoSim.Application/Simulation/Allocation/TeamStrategyFactory.cs` | Implementation; internal `_strategies` dictionary; returns `ITeamStrategy` |

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Constructor: `allocatorFactory` → `strategyFactory`; `GetAllocator` → `GetStrategy`; `allocator` → `strategy` |
| `BingoSim.Infrastructure/DependencyInjection.cs` | `IProgressAllocatorFactory` → `ITeamStrategyFactory`; `ProgressAllocatorFactory` → `TeamStrategyFactory` |
| `BingoSim.Application/StrategyKeys/StrategyCatalog.cs` | Updated XML comments to reference "strategies"; clarified RowRush and GreedyPoints descriptions |
| `BingoSim.Seed/Program.cs` | `allocatorFactory` → `strategyFactory`; `ProgressAllocatorFactory` → `TeamStrategyFactory` |
| `Tests/BingoSim.Application.UnitTests/Simulation/SimulationPerfScenarioTests.cs` | 3 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/SimulationNoProgressGuardTests.cs` | 2 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierSimulationIntegrationTests.cs` | 2 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlaySimulationTests.cs` | 2 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleSimulationIntegrationTests.cs` | 4 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/SimulationRunnerReproducibilityTests.cs` | 2 occurrences updated |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlayIntegrationTests.cs` | 4 occurrences updated |

## DI Registration Changes

**Before:**
```csharp
services.AddSingleton<IProgressAllocatorFactory, ProgressAllocatorFactory>();
```

**After:**
```csharp
services.AddSingleton<ITeamStrategyFactory, TeamStrategyFactory>();
```

## Build and Test Results

- **Build**: Succeeded (0 errors, 0 warnings)
- **Tests**: All non-perf tests passed (Application: 198, Core: 151, Worker: 13, Web: 35)
- **Simulations**: Existing strategies (RowRush, GreedyPoints) run unchanged; no functionality changes
