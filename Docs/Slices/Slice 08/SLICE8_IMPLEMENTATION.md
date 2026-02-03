# Slice 8: Apply ActivityModifierRule in Snapshot + Simulation — Implementation

**Completed:** February 2, 2025  
**Review:** February 2, 2025 — gaps addressed (time flooring, null handling, distributed worker test)  
**Refactor:** February 2, 2025 — centralize multiplier logic, reduce hot-loop allocations

## Summary

ActivityModifierRule (capability-based time and probability multipliers) is now included in EventSnapshot JSON and applied during simulation. Players with modifier capabilities complete attempts faster and have improved drop probability for outcomes relevant to the tile they are working on.

---

## Files Changed / Added

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivityModifierRuleSnapshotDto.cs` | Snapshot DTO for modifier (CapabilityKey, TimeMultiplier?, ProbabilityMultiplier?) |
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | Static helper for computing combined multipliers and applying probability scaling |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierApplicationTests.cs` | Unit tests for modifier combination, clamping, capability filtering |
| `Tests/BingoSim.Application.UnitTests/Simulation/EventSnapshotBuilderModifierTests.cs` | Snapshot builder tests: modifiers included / empty list |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierSimulationIntegrationTests.cs` | Integration tests: modifier effect, determinism, backward compat |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs` | Added `Modifiers` property (default `[]` for version tolerance) |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `Modifiers` when building `TileActivityRuleSnapshotDto` |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | `GetFirstEligibleActivity` returns rule; `SampleAttemptDuration` and `RollOutcome` apply modifiers; `SimEvent` stores rule; `GetEligibleTileKeys` fixed; time uses `Math.Floor`; `BuildPlayerCapabilitySets` caches capability HashSets once per run |
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | Null-safe: `rule?.Modifiers ?? []`; `ComputeCombinedMultipliers` centralizes logic; `ApplyProbabilityMultiplier` accepts `IReadOnlyCollection<string>?` to avoid HashSet allocation |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Added `DistributedBatch_SnapshotWithModifiers_CompletesSuccessfully` |

---

## Implementation Details

### 1. Snapshot Model

- **ActivityModifierRuleSnapshotDto**: `CapabilityKey`, `TimeMultiplier?`, `ProbabilityMultiplier?`
- **TileActivityRuleSnapshotDto.Modifiers**: `List<ActivityModifierRuleSnapshotDto>` with default `[]` for backward compatibility with older snapshot JSON

### 2. Modifier Application

- **Centralized computation**: `ModifierApplicator.ComputeCombinedMultipliers(rule, caps)` returns `(Time, Probability)` in a single pass. `ComputeCombinedTimeMultiplier` and `ComputeCombinedProbabilityMultiplier` delegate to it.
- **Time**: `combinedTimeMultiplier = product of applicable modifiers' TimeMultiplier` (player has capability). Applied: `time = rawTime * skillMultiplier * combinedTimeMultiplier`; **floored** to ≥ 1 second via `Math.Max(1, (int)Math.Floor(time))`.
- **Probability**: Scale weights only for outcomes whose grant DropKey is in `TileActivityRule.AcceptedDropKeys`. Outcomes with matching grants get `WeightNumerator * combinedProbabilityMultiplier`; others unchanged. Clamp adjusted weights to [0, MaxAdjustedWeight]. Effective probabilities remain in [0, 1].

### 3. Determinism

- Modifiers and player capabilities come from the snapshot. Same seed + same snapshot ⇒ same outcome.

### 4. Null Safety (JSON Deserialization)

- `ModifierApplicator` uses `rule?.Modifiers ?? []` so old snapshots (missing or null `Modifiers`) are handled without error.

### 5. Bug Fix (GetEligibleTileKeys)

- Original logic iterated over `state.TileProgress`, which is empty before any allocation, so the first drop was never allocated.
- Updated to iterate over `tileToRules.Keys` so all eligible tiles (unlocked, not completed, accept drop) are considered.

### 6. Refactoring (Complexity Reduction)

- **Centralized multiplier computation**: Single `ComputeCombinedMultipliers` pure function; time and prob delegates call it.
- **Reduced hot-loop allocations**:
  - Player capability sets built once at run start via `BuildPlayerCapabilitySets`; reused for all attempts.
  - `ApplyProbabilityMultiplier` accepts `IReadOnlyCollection<string>?`; passes `rule?.AcceptedDropKeys` directly (no per-attempt HashSet for accepted keys).

---

## How to Run Tests

```bash
# Application unit tests (includes modifier tests)
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj

# All tests (requires Docker for integration tests)
dotnet test
```

---

## Manual Verification (UI)

1. **Start the app** with seeded data (e.g. `dotnet run --project BingoSim.Web` after `BingoSim.Seed`).
2. **Create or use an Event** with tiles that have modifiers (e.g. Winter Bingo 2025 from seed).
3. **Create two teams**:
   - Team A: players with capability (e.g. "quest.ds2", "item.dragon_hunter_lance")
   - Team B: players without those capabilities
4. **Run a batch** (e.g. 100 runs) with a fixed seed.
5. **Compare results**: Team A should show higher mean points/tiles than Team B.

---

## Version Tolerance

- Snapshots without `Modifiers` deserialize with `Modifiers = []` or null; `ModifierApplicator` treats null as empty.
- Simulation treats null/empty modifiers as no effect (multipliers = 1.0).

---

## Verification Checklist (Review)

| Requirement | Status |
|-------------|--------|
| Snapshot JSON includes modifiers and persists correctly | ✅ EventSnapshotBuilder populates; stored in EventConfigJson |
| Simulation uses snapshot modifiers (not live config) | ✅ Runner receives snapshot JSON; all data from snapshot |
| Modifiers only apply when player has capability | ✅ `playerCapabilityKeys.Contains(mod.CapabilityKey)` |
| Multipliers combine correctly (multiplicative) | ✅ Product of applicable modifiers |
| Probability clamped to [0, 1] | ✅ Per-outcome weights clamped; total > 0; distribution valid |
| Time floored to ≥ 1 second | ✅ `Math.Max(1, (int)Math.Floor(time))` |
| Seed determinism | ✅ Same seed + snapshot ⇒ identical results |
| Unit + integration tests pass | ✅ ModifierApplicationTests, EventSnapshotBuilderModifierTests, ModifierSimulationIntegrationTests |
| Works with distributed workers | ✅ DistributedBatch_SnapshotWithModifiers_CompletesSuccessfully |

---

*End of implementation document.*
