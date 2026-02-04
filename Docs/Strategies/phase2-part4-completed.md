# Phase 2 Part 4: Implement Tile Completion Time Estimation — Completed

## Summary

Created `TileCompletionEstimator` to estimate the time required to complete tiles based on activity configurations. Integrated with `RowUnlockingStrategy` via `EnrichCombinationsWithTimeEstimates` to populate `EstimatedCompletionTime` on combinations.

## Implementation Approach

### Formula

For each activity rule on a tile:

1. **Expected progress per run** = sum over all attempts of (sum over outcomes of P(outcome) × progress_from_outcome)
   - P(outcome) = `WeightNumerator / totalWeight` (totalWeight = sum of all outcome WeightNumerators in the attempt)
   - progress_from_outcome = sum of `grant.Units` for grants where `grant.DropKey` is in `rule.AcceptedDropKeys`

2. **Time per run** = max of `BaselineTimeSeconds` across all attempts in the activity

3. **Runs needed** = ceil(`RequiredCount` / expected_progress_per_run)

4. **Total time** = runs_needed × time_per_run

5. **Best estimate** = minimum total time over all valid rules on the tile

### Algorithm

- Iterate over `tile.AllowedActivities` (rules)
- For each rule: get activity from `snapshot.ActivitiesById`
- Compute expected progress and time; skip if progress ≤ 0 or time ≤ 0
- Return the minimum time, or `double.MaxValue` if no valid rule

## Example Calculations

### Simple case: 100% drop, 1 unit, 60s per attempt, RequiredCount=1

- Expected progress per run = 1.0
- Runs needed = 1
- **Estimated time = 60 seconds**

### 50% drop rate

- Two outcomes: 50% chance of 1 unit (drop), 50% chance of 0 (other)
- Expected progress per run = 0.5
- RequiredCount=1 → runs needed = ceil(1/0.5) = 2
- **Estimated time = 2 × 60 = 120 seconds**

### Two activities, choose fastest

- Activity A: 120s per run, 1 unit per run → 120s for RequiredCount=1
- Activity B: 60s per run, 1 unit per run → 60s for RequiredCount=1
- **Estimated time = 60 seconds** (minimum)

## Edge Case Handling

| Edge Case | Handling |
|-----------|----------|
| RequiredCount = 0 | Return 0.0 |
| Activity not found in snapshot | Skip rule, return double.MaxValue if no valid rules |
| No outcomes in attempt | Skip attempt, 0 progress from that attempt |
| totalWeight = 0 | Skip attempt |
| No grants matching AcceptedDropKeys | progress_from_outcome = 0 |
| Zero expected progress | Skip rule |
| No valid rules | Return double.MaxValue |

## Assumptions

- **Variance ignored**: Uses `BaselineTimeSeconds` only; `VarianceSeconds` not used
- **Skill multipliers ignored**: No `SkillTimeMultiplier` applied
- **Group size effects ignored**: No `GroupScalingBands` or group probability scaling
- **Modifiers ignored**: No `ModifierApplicator` probability scaling
- **Solo assumption**: One roll per attempt (no per-player vs per-group distinction for estimation)

## Files Created

| File | Description |
|------|-------------|
| `BingoSim.Application/Simulation/Strategies/TileCompletionEstimator.cs` | Static class with `EstimateCompletionTime` and helpers |
| `Tests/BingoSim.Application.UnitTests/Simulation/Strategies/TileCompletionEstimatorTests.cs` | 7 unit tests |

## Files Modified

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs` | Added `EnrichCombinationsWithTimeEstimates`; `GetCombinationsForRow` now enriches before caching |

## Test Results

| Test | Result |
|------|--------|
| `EstimateCompletionTime_RequiredCountZero_ReturnsZero` | Pass |
| `EstimateCompletionTime_ActivityNotFound_ReturnsMaxValue` | Pass |
| `EstimateCompletionTime_100PercentDropOneUnit_ReturnsAttemptTime` | Pass |
| `EstimateCompletionTime_RequiredCountTwo_ReturnsDoubleAttemptTime` | Pass |
| `EstimateCompletionTime_50PercentDrop_ReturnsDoubleTime` | Pass |
| `EstimateCompletionTime_NoMatchingGrants_ReturnsMaxValue` | Pass |
| `EstimateCompletionTime_TwoActivities_ChoosesFastest` | Pass |

All 22 strategy-related tests passed.

## Integration Points

- **TileCompletionEstimator.EstimateCompletionTime**: Called from `EnrichCombinationsWithTimeEstimates`
- **EnrichCombinationsWithTimeEstimates**: Called from `GetCombinationsForRow` after `RowCombinationCalculator.CalculateCombinations`
- **GetCombinationsForRow**: Used by `SelectTaskForPlayer` (Phase 2.5) to pick the optimal combination by `EstimatedCompletionTime`

## Data Structure Mapping

The snapshot uses:
- `OutcomeSnapshotDto`: `WeightNumerator`, `WeightDenominator`, `Grants`
- `ProgressGrantSnapshotDto`: `DropKey`, `Units`
- `AttemptSnapshotDto`: `BaselineTimeSeconds`, `Outcomes`
- `TileActivityRuleSnapshotDto`: `AcceptedDropKeys`, `ActivityDefinitionId`

The user's prompt referenced `PossibleDrops` with `Rate` and `Units`; the actual model uses outcome weights and grants. The implementation maps: probability = WeightNumerator/totalWeight, progress = sum of grant.Units for matching DropKeys.
