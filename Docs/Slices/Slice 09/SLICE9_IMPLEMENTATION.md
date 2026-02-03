# Slice 9: Group Play — Implementation

**Completed:** February 2, 2025  
**Review/Fixes:** February 2, 2025 — determinism, null-safety, multiple groups test  
**Refactor:** February 2, 2025 — centralize multiplier stacking, reuse grants buffer

## Summary

Group formation, PerGroup/PerPlayer roll scope, and GroupScalingBands are now implemented in the simulation engine. Activities with `ModeSupport.SupportsGroup` form groups greedily from eligible players; PerPlayer loot lines roll once per player, PerGroup lines roll once for the group; scaling bands apply time and probability multipliers based on group size. Modifiers (Slice 8) stack multiplicatively with group scaling.

---

## Review Fixes (Feb 2, 2025)

- **PriorityQueue determinism:** Changed from `int` (endTime only) to `(int EndTime, int TeamIndex, int FirstPlayerIndex)` so events with the same endTime are dequeued in deterministic order (team index, then first player index). Ensures seed reproducibility when multiple events complete at the same sim time.
- **ModeSupport null-safety:** Added `activity.ModeSupport ?? new ActivityModeSupportSnapshotDto()` when forming groups, for older snapshots that may deserialize with null ModeSupport.
- **Multiple concurrent groups test:** Added `GroupPlay_EightPlayersMaxFourPerGroup_FormsMultipleConcurrentGroups` — 8 players with maxGroupSize=4 forms 2 groups; verifies concurrent group scheduling.

### Refactor (Feb 2, 2025)

- **Centralize multiplier stacking:** `GroupScalingBandSelector.ComputeEffectiveMultipliers(bands, groupSize, rule, caps)` returns `(effectiveTime, effectiveProb)` = group × modifier. Single pure function for stacking; used by both duration sampling and outcome rolling.
- **Extract CollectGrantsFromAttempts:** Moved PerPlayer/PerGroup roll logic into `CollectGrantsFromAttempts`, which fills a provided list. Reduces inline duplication.
- **Reuse grants buffer:** `grantsBuffer` list created once per run, cleared per event. Avoids per-event `new List<ProgressGrantSnapshotDto>()`.
- **RollOutcome signature:** Removed unused capability-keys parameter; now takes only `effectiveProbabilityMultiplier`.

---

## Files Changed / Added

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivityModeSupportSnapshotDto.cs` | Snapshot DTO for SupportsSolo, SupportsGroup, MinGroupSize, MaxGroupSize |
| `BingoSim.Application/Simulation/GroupScalingBandSelector.cs` | Pure functions: Select (bands, groupSize) ⇒ (time, prob); ComputeEffectiveMultipliers ⇒ group × modifier stacking |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupScalingBandSelectorTests.cs` | Unit tests for band selection and ComputeEffectiveMultipliers stacking |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlaySimulationTests.cs` | Unit tests: skill assumption (slowest dominates), determinism |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlayIntegrationTests.cs` | Integration tests: PerPlayer+PerGroup, 1 vs 4 players, scaling, backward compat |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs` | Added `ModeSupport` property (default for backward compat) |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `ModeSupport` when building ActivitySnapshotDto |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Group formation; SimEvent with PlayerIndices; max time model; CollectGrantsFromAttempts; reused grantsBuffer; v1 skill assumption (slowest dominates) with code comment |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Added `DistributedBatch_GroupSnapshotFields_PresentAndUsed_CompletesSuccessfully` |

---

## Implementation Details

### 1. Snapshot Model

- **ActivityModeSupportSnapshotDto**: SupportsSolo, SupportsGroup, MinGroupSize?, MaxGroupSize?
- **ActivitySnapshotDto.ModeSupport**: Default `{ SupportsSolo: true, SupportsGroup: false }` for older snapshots

### 2. Time Model (Max Across Attempts)

- Run duration uses **max** of `BaselineTimeSeconds` and `BaselineTimeSeconds + VarianceSeconds` across all attempts
- Variance for scheduling: `max(VarianceSeconds)` across attempts
- One logical run encompasses all loot lines; duration reflects longest phase

### 3. Group Skill Multiplier (v1 Assumption)

- **Slowest member dominates**: `skillMultiplier = max(players.SkillTimeMultiplier)`
- Group waits for slowest player; higher SkillTimeMultiplier = slower
- Documented in code comment and `GroupPlaySimulationTests.GroupSkillMultiplier_SlowestMemberDominates_GroupTakesLongerThanFastestSolo`

### 4. Multiplier Stacking

- **Time:** `effectiveTime = baseTime * skillMultiplier * groupTimeMultiplier * modifierTimeMultiplier`
- **Probability:** `effectiveProb = groupProbMultiplier * modifierProbMultiplier` (combined before weight scaling)

### 5. Group Formation

- Players sorted by PlayerId for determinism
- Greedy: form groups from players with same (activityId, rule) and activity SupportsGroup
- Multiple groups may run concurrently (e.g. 8 players, maxGroupSize=4 → 2 groups of 4)
- Solo fallback when SupportsSolo and group size 1
- MinGroupSize/MaxGroupSize respected; ModeSupport null coalesced for backward compat

### 6. Roll Scope

- **PerPlayer (0):** Roll once per player; each uses own capability set for modifiers
- **PerGroup (1):** Roll once for group; union of members' capabilities for modifiers

### 7. Determinism

- Event queue uses compound priority `(endTime, teamIndex, firstPlayerIndex)` for tie-breaking when multiple events share the same endTime
- ModifierApplicator falls back to original weights when total adjusted weight ≤ 0 (no negative/NaN weights in selection)

---

## How to Run Tests

```bash
# Application unit tests (includes group play tests)
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj

# Integration tests (requires Docker for Postgres)
dotnet test Tests/BingoSim.Infrastructure.IntegrationTests/BingoSim.Infrastructure.IntegrationTests.csproj

# All tests
dotnet test
```

---

## Manual Verification (Seeded Data)

1. **Seed the database:**
   ```bash
   dotnet run --project BingoSim.Seed
   ```

2. **Start the app:**
   ```bash
   dotnet run --project BingoSim.Web
   ```

3. **Create or use an Event** with group-capable activities (e.g. CoX, Zulrah from seed). Ensure:
   - Activity has `SupportsGroup = true`, `MinGroupSize`, `MaxGroupSize`
   - Activity has `GroupScalingBands` (e.g. 1–1, 2–4, 5–8)
   - At least one attempt has `RollScope = PerGroup`

4. **Create two teams:**
   - **Team A:** 1 player
   - **Team B:** 4 players (same activity eligibility)

5. **Run a batch** (e.g. 100 runs) with a fixed seed (e.g. `group-test-123`).

6. **Verify:**
   - Batch completes successfully
   - Team B (4 players) typically has higher mean points due to group scaling
   - Re-run with same seed → identical results (determinism)

7. **Distributed mode:** Start Worker and run with Distributed execution; batch should complete and results reflect group behavior.

---

## Version Tolerance

- Snapshots without `ModeSupport` deserialize with default `{ SupportsSolo: true, SupportsGroup: false }` → solo-only behavior
- Empty or null `GroupScalingBands` → identity multipliers (1.0)

---

*End of implementation document.*
