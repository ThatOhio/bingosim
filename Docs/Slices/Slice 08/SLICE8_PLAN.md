# Slice 8: Apply ActivityModifierRule in Snapshot + Simulation — Plan Only

**Scope:** Ensure EventSnapshot includes full modifier definitions and that the simulation engine applies capability-based time and probability multipliers when a player executes an activity attempt.

**Source of truth:**
- `Docs/06_Acceptance_Tests.md` — Simulation correctness expectations
- `Docs/02_Domain.md` — TileActivityRule.Modifiers meaning and capability linkage
- `Docs/08_Feature_Audit_2025.md` — Modifiers exist in config but are not applied in simulation

**Constraints:** Clean Architecture; simulation engine stays in Application; snapshot generation occurs when starting a batch; snapshot is persisted and is source of truth for that batch. Do NOT implement group play or schedules in this slice.

---

## 1) Snapshot Build Location and JSON Model Extension

### 1.1 Where Snapshot is Built

- **Class:** `BingoSim.Application.Simulation.Snapshot.EventSnapshotBuilder`
- **Method:** `BuildSnapshotJsonAsync(Guid eventId, CancellationToken cancellationToken)`
- **Invoked by:** `SimulationBatchService.StartBatchAsync` (line ~57) when a batch is started
- **Flow:** EventSnapshotBuilder loads Event, Teams, Activities, PlayerProfiles; builds `EventSnapshotDto`; serializes to JSON; JSON is stored in `EventSnapshot.EventConfigJson` (one snapshot per batch)
- **Deserialization:** `EventSnapshotBuilder.Deserialize(string json)` — used by `SimulationRunner.Execute` to parse the stored JSON

### 1.2 Current Snapshot Structure

- `EventSnapshotDto` → `Rows` → `TileSnapshotDto` → `AllowedActivities` → `TileActivityRuleSnapshotDto`
- `TileActivityRuleSnapshotDto` currently has: `ActivityDefinitionId`, `ActivityKey`, `AcceptedDropKeys`, `RequirementKeys`
- **Gap:** `TileActivityRuleSnapshotDto` does NOT include `Modifiers`

### 1.3 JSON Model Extension

Extend `TileActivityRuleSnapshotDto` to include a `Modifiers` list. Each modifier in the snapshot will be a lightweight DTO containing only the data needed for simulation (no display names):

- `CapabilityKey`: string (stable identifier for capability matching)
- `TimeMultiplier`: decimal? (optional; null means do not apply time effect)
- `ProbabilityMultiplier`: decimal? (optional; null means do not apply probability effect)

---

## 2) Snapshot DTO Changes

### 2.1 New DTO: `ActivityModifierRuleSnapshotDto`

**Location:** `BingoSim.Application/Simulation/Snapshot/ActivityModifierRuleSnapshotDto.cs`

**Properties:**
| Property | Type | Required | Description |
|----------|------|----------|-------------|
| CapabilityKey | string | yes | Stable identifier for matching against `PlayerSnapshotDto.CapabilityKeys` |
| TimeMultiplier | decimal? | no | Multiplier for attempt time; null = no effect |
| ProbabilityMultiplier | decimal? | no | Multiplier for outcome probability; null = no effect |

**Invariant:** At least one of `TimeMultiplier` or `ProbabilityMultiplier` must be non-null (same as domain `ActivityModifierRule`).

### 2.2 Extend `TileActivityRuleSnapshotDto`

**Location:** `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs`

**Add property:**
- `Modifiers`: `List<ActivityModifierRuleSnapshotDto>` — default to empty list `[]` for backward compatibility with existing snapshot JSON (omit or empty).

### 2.3 EventSnapshotBuilder Changes

**Location:** `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs`

When building `TileActivityRuleSnapshotDto` from `rule` (TileActivityRule):

```csharp
// Existing: ActivityDefinitionId, ActivityKey, AcceptedDropKeys, RequirementKeys
// Add:
Modifiers = rule.Modifiers.Select(m => new ActivityModifierRuleSnapshotDto
{
    CapabilityKey = m.Capability.Key,
    TimeMultiplier = m.TimeMultiplier,
    ProbabilityMultiplier = m.ProbabilityMultiplier
}).ToList()
```

---

## 3) Simulation Changes

### 3.1 Data Flow: Rule Context for Modifiers

**Current flow:** `GetFirstEligibleActivity` returns `(activityId, attemptKey)` — the first eligible tile’s activity. The simulation does not know which tile/rule the player is working on when sampling duration or rolling outcome.

**Required change:** `GetFirstEligibleActivity` must also return the `TileActivityRuleSnapshotDto` (the rule) for the first eligible tile. Modifiers are per-rule; the same activity can have different modifiers on different tiles.

**Signature change:**
- From: `(Guid? activityId, string? attemptKey)`
- To: `(Guid? activityId, string? attemptKey, TileActivityRuleSnapshotDto? rule)`

### 3.2 Where Attempt Time is Computed

**Method:** `SampleAttemptDuration` (private static in `SimulationRunner`)

**Current logic:**
- Inputs: `snapshot`, `activityId`, `attemptKey`, `skillMultiplier`, `rng`
- Formula: `time = baseline + variance`; then `time = Max(1, (int)(time * skillMultiplier))`

**New logic:**
- Add parameters: `rule` (nullable), `playerCapabilityKeys` (e.g. `IReadOnlySet<string>`)
- Compute `combinedTimeMultiplier` from applicable modifiers (see §4)
- Formula: `time = baseline + variance`; then `time = time * skillMultiplier * combinedTimeMultiplier`; clamp to `>= 1` second

### 3.3 Where Probability is Applied

**Method:** `RollOutcome` (private static in `SimulationRunner`)

**Current logic:** Weighted random selection: `roll = rng.Next(0, totalWeight)`; iterate outcomes, sum weights until `roll < sum`; return that outcome.

**New logic:**
- Add parameters: `rule` (nullable), `playerCapabilityKeys`
- Compute `combinedProbabilityMultiplier` from applicable modifiers (see §4)
- Apply multiplier to outcome weights (see §4.2 for exact rule)
- Use adjusted weights for the roll

### 3.4 Call Sites in SimulationRunner

| Location | Change |
|----------|--------|
| Initial event queue population (lines ~69–78) | Pass `rule` from `GetFirstEligibleActivity`; pass `rule` and `player.CapabilityKeys` to `SampleAttemptDuration` |
| Main loop after outcome (lines ~129–135) | Same: pass `rule` and `player.CapabilityKeys` to `SampleAttemptDuration` |
| `RollOutcome` call (line ~105) | Pass `rule` and `player.CapabilityKeys`; `GetFirstEligibleActivity` must return the rule for the tile the player is working on |

**Note:** When the player rolls an outcome, they are doing an attempt for the activity they were assigned. The rule comes from the tile they are “working on” (the first eligible tile). The same rule is used for both duration and outcome for that attempt.

---

## 4) Combination Rules, Clamping, and Units

### 4.1 Applicable Modifiers

**Null/empty rule:** If `rule` is null or `rule.Modifiers` is null/empty, use `combinedTimeMultiplier = 1.0` and `combinedProbabilityMultiplier = 1.0` (no modifier effect).

A modifier is **applicable** if and only if:
- The player has the modifier’s `CapabilityKey` in their `CapabilityKeys` list (case-insensitive or ordinal per existing `GetFirstEligibleActivity` usage — use `StringComparer.Ordinal` for consistency with `HashSet<string>`).

### 4.2 Combined Multipliers

- **Time:** `combinedTimeMultiplier = product of all applicable modifiers’ TimeMultiplier values` (skip nulls). Default: `1.0m` if no applicable time modifiers.
- **Probability:** `combinedProbabilityMultiplier = product of all applicable modifiers’ ProbabilityMultiplier values` (skip nulls). Default: `1.0m` if no applicable probability modifiers.

### 4.3 Clamping Rules

| Quantity | Unit | Clamp Rule |
|----------|------|------------|
| Attempt time | seconds (int) | `>= 1` second. After all multipliers: `time = Max(1, (int)Math.Round(rawTime))` |
| Probability | — | Effective outcome probabilities must remain in [0, 1]. Apply via weight scaling (see §4.4). |

### 4.4 Probability Multiplier Application (Weight Scaling)

**Interpretation:** The probability multiplier increases the relative likelihood of outcomes that grant progress (i.e. outcomes with `Grants` where any grant has `Units > 0`).

**Rule:**
- For each outcome: if the outcome has at least one grant with `Units > 0`, multiply its `WeightNumerator` by `combinedProbabilityMultiplier` (use `decimal` for precision, then round to int).
- Outcomes with no grants (or only 0-unit grants) keep their original weight.
- Recompute `totalWeight` from the adjusted weights.
- Clamp: If the adjusted weight of any outcome would exceed `int.MaxValue` or cause overflow, cap at a safe maximum. Ensure `totalWeight > 0`; if it becomes 0, fall back to original weights.
- Final probability of each outcome = `adjustedWeight / totalWeight`; this remains in [0, 1] by construction.

**Alternative (simpler):** Multiply **all** outcome weights by `combinedProbabilityMultiplier`. This preserves the distribution (no effect). Rejected.

**Chosen approach:** Scale only outcomes that grant progress. This increases the chance of getting a drop when the player has the capability.

### 4.5 Units Summary

- **Time:** Integer seconds. Minimum 1 second after all multipliers.
- **Probability:** Applied via integer weight scaling; no separate probability unit.

---

## 5) Determinism

- Modifier definitions come from the snapshot (immutable for the batch).
- Player capability keys come from the snapshot (immutable for the batch).
- No additional randomness is introduced by modifier application.
- Combination and clamping are pure functions of snapshot data and RNG (which is already deterministic from seed).
- **Guarantee:** Same seed + same snapshot JSON ⇒ same outcome.

---

## 6) Test Plan and Exact Test Cases

### 6.1 Unit Tests — Modifier Combination and Clamping

**Location:** `Tests/BingoSim.Application.UnitTests/Simulation/` (new or extend existing)

| Test | Description |
|------|-------------|
| `ModifierCombination_NoApplicableModifiers_ReturnsOne` | Given rule with modifiers whose capability keys the player lacks; combined time and probability multipliers are 1.0 |
| `ModifierCombination_SingleApplicableTimeModifier_ReturnsMultiplier` | Player has capability; rule has one modifier with TimeMultiplier=0.9; combined time = 0.9 |
| `ModifierCombination_TwoApplicableModifiers_MultipliersMultiply` | Player has both capabilities; modifiers 0.9 and 0.8 for time; combined = 0.72 |
| `ModifierCombination_NullValuesSkipped` | One modifier has TimeMultiplier=null, ProbabilityMultiplier=1.2; only probability applies |
| `TimeClamping_ResultBelowOne_ClampsToOne` | Base 1s, skill 0.5, time modifier 0.5 → raw 0.25s → clamp to 1 |
| `ProbabilityClamping_WeightsRemainValid` | Multiplier 2.0 on granting outcomes; total weight stays positive; no overflow |

**Implementation note:** Extract modifier combination logic into a small helper (e.g. `ModifierApplicator` or static methods) so it can be unit-tested without running the full simulation. Keep it in Application layer.

### 6.2 Unit Tests — Capability Filtering

| Test | Description |
|------|-------------|
| `ApplicableModifiers_PlayerHasCapability_ModifierApplies` | Player has "quest.ds2"; rule has modifier with CapabilityKey="quest.ds2"; modifier is included |
| `ApplicableModifiers_PlayerLacksCapability_ModifierExcluded` | Player lacks "item.dhl"; modifier with that key is excluded |
| `ApplicableModifiers_PartialMatch_OnlyMatchingApply` | Rule has modifiers for "a" and "b"; player has "a"; only "a" modifier applies |

### 6.3 Integration Test — Modifier Effect on Results

**Location:** `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/`

| Test | Description |
|------|-------------|
| `ModifierIntegration_PlayerWithCapability_OutperformsPlayerWithout` | Create event with tile rule: modifier CapabilityKey="quest.ds2", TimeMultiplier=0.9, ProbabilityMultiplier=1.2. Two teams: Team A has player with capability; Team B has player without. Run batch (e.g. 100 runs) with fixed seed. Assert Team A has higher mean points/tiles than Team B (or at least different results). |

**Setup:** Use in-memory or test DB; build snapshot via EventSnapshotBuilder or hand-craft EventSnapshotDto JSON with modifiers; run SimulationRunner directly or via full batch flow.

### 6.4 Snapshot Test — Modifiers in Stored JSON

**Location:** `Tests/BingoSim.Application.UnitTests/` or `Tests/BingoSim.Infrastructure.IntegrationTests/`

| Test | Description |
|------|-------------|
| `EventSnapshotBuilder_EventWithModifiers_IncludesModifiersInJson` | Create Event (via repo or in-memory) with TileActivityRule that has Modifiers. Call `BuildSnapshotJsonAsync`. Deserialize result. Assert `TileActivityRuleSnapshotDto.Modifiers` is non-empty and contains expected CapabilityKey, TimeMultiplier, ProbabilityMultiplier. |
| `EventSnapshotBuilder_EventWithoutModifiers_SerializesEmptyList` | Event with no modifiers; snapshot has `Modifiers = []` or equivalent. |

---

## 7) Exact List of Files to Modify

### 7.1 New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivityModifierRuleSnapshotDto.cs` | Snapshot DTO for modifier (CapabilityKey, TimeMultiplier?, ProbabilityMultiplier?) |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierApplicationTests.cs` | Unit tests for combination, clamping, capability filtering |
| `Tests/BingoSim.Application.UnitTests/Simulation/EventSnapshotBuilderModifierTests.cs` | Snapshot builder tests for modifier inclusion |

### 7.2 Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs` | Add `Modifiers` property |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `Modifiers` when building `TileActivityRuleSnapshotDto` |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Extend `GetFirstEligibleActivity` to return rule; pass rule and capability keys to `SampleAttemptDuration` and `RollOutcome`; apply modifiers in both |

### 7.3 Optional: Extracted Helper

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | Static or small class: `ComputeCombinedTimeMultiplier(rule, capabilityKeys)`, `ComputeCombinedProbabilityMultiplier(rule, capabilityKeys)`, and optionally weight-adjustment logic. Enables focused unit testing. |

### 7.4 Integration Test (New or Extend)

| File | Changes |
|------|---------|
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/ModifierIntegrationTests.cs` | New integration test: event with modifiers, two teams (with/without capability), verify results differ |

### 7.5 Summary

- **New:** 3–4 files (ActivityModifierRuleSnapshotDto, ModifierApplicationTests, EventSnapshotBuilderModifierTests, ModifierIntegrationTests; optionally ModifierApplicator)
- **Modified:** 3 files (TileActivityRuleSnapshotDto, EventSnapshotBuilder, SimulationRunner)

---

## 8) Out of Scope (Explicitly Excluded)

- Group play, group formation, PerGroup roll scope
- GroupScalingBands (separate slice)
- WeeklySchedule / play sessions
- DropKeyWeights in allocation
- UI changes (modifiers already configurable in Event Create/Edit)

---

*End of plan.*
