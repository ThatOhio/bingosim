# Refactor: Remove Backwards-Compatibility Paths — Plan

**Status:** Complete (implementation + Step 3 HARDEN done)  
**Date:** February 3, 2025  
**Goal:** Remove legacy/backwards-compat branches to simplify the codebase for performance and maintainability. Missing required data should fail fast and loudly, not silently default.

**Context:** Internal tool; database is frequently wiped. Data integrity across old DB/snapshots is NOT required.

---

## 1) Legacy Behaviors to Remove

### 1.1 Simulation Hot Path

| # | File(s) | Function(s) | Fallback/Default | Hot Path? |
|---|---------|-------------|------------------|-----------|
| 1 | `ModifierApplicator.cs` | `ComputeCombinedMultipliers` | `rule?.Modifiers ?? []` — null/empty Modifiers → multipliers 1.0 | **Yes** |
| 2 | `ModifierApplicator.cs` | `ApplyProbabilityMultiplier` | `acceptedDropKeys` null/empty → no scaling, return original weights | **Yes** |
| 3 | `ModifierApplicator.cs` | `ApplyProbabilityMultiplier` | `o.Grants?.Any(...) ?? false` — null Grants on outcome → treat as non-matching | **Yes** |
| 4 | `SimulationRunner.cs` | `ScheduleEventsForPlayers` | `activity.ModeSupport ?? new ActivityModeSupportSnapshotDto()` — null → assume solo (SupportsSolo=true, SupportsGroup=false) | **Yes** |
| 5 | `SimulationRunner.cs` | `ScheduleEventsForPlayers` | `modeSupport.MinGroupSize ?? 1`, `modeSupport.MaxGroupSize ?? int.MaxValue` | **Yes** |
| 6 | `SimulationRunner.cs` | `Execute` | `eventStartEt` null → skip all schedule checks (all players always online) | **Yes** |
| 7 | `SimulationRunner.cs` | `ScheduleEventsForPlayers` | `eventStartEt` null → no schedule filtering; `team.Players[pi].Schedule` null → IsOnlineAt treats as always online | **Yes** |
| 8 | `SimulationRunner.cs` | `SampleAttemptDuration` | `activity` null → return 60 (default duration) | **Yes** |
| 9 | `SimulationRunner.cs` | `GetMaxAttemptTimeModel` | `activity.Attempts.Count == 0` → return (60, 0) | **Yes** |
| 10 | `SimulationRunner.cs` | `GetMaxAttemptTimeModel` | `a.VarianceSeconds ?? 0` | **Yes** |
| 11 | `GroupScalingBandSelector.cs` | `Select`, `ComputeEffectiveMultipliers` | `bands` null/empty → (1.0, 1.0) | **Yes** |

### 1.2 Schedule Evaluation (Simulation Hot Path)

| # | File(s) | Function(s) | Fallback/Default | Hot Path? |
|---|---------|-------------|-----------------|-----------|
| 12 | `ScheduleEvaluator.cs` | `DailyWindows.Build`, `IsOnlineAt`, `GetCurrentSessionEnd` | `schedule?.Sessions` null/empty → always online | **Yes** |
| 13 | `ScheduleEvaluator.cs` | `GetNextSessionStart` | `schedule?.Sessions` null/empty → return null | **Yes** |

### 1.3 Batch Start Path (Snapshot Building)

| # | File(s) | Function(s) | Fallback/Default | Hot Path? |
|---|---------|-------------|-----------------|-----------|
| 14 | `EventSnapshotBuilder.cs` | `BuildSnapshotJsonAsync` | `strategy?.StrategyKey ?? "RowRush"` — team missing StrategyConfig → default strategy | **Batch start** |
| 15 | `EventSnapshotBuilder.cs` | `BuildSnapshotJsonAsync` | `eventStartTimeUtc` null → omit `EventStartTimeEt` from snapshot → schedule disabled | **Batch start** |
| 16 | `EventSnapshotBuilder.cs` | `MapSchedule` | `schedule.Sessions.Count == 0` → return null (player always online) | **Batch start** |

### 1.4 Snapshot DTO Defaults (JSON Deserialization)

| # | File(s) | Property | Fallback/Default | Hot Path? |
|---|---------|----------|-----------------|-----------|
| 17 | `TileActivityRuleSnapshotDto.cs` | `Modifiers` | `init; = []` — JSON deserialization: missing → empty list | **Yes** (deserialized) |
| 18 | `ActivitySnapshotDto.cs` | `ModeSupport` | `init; = new()` — missing → default SupportsSolo=true, SupportsGroup=false | **Yes** |

### 1.5 Application Layer (Non-Simulation)

| # | File(s) | Function(s) | Fallback/Default | Hot Path? |
|---|---------|-------------|-----------------|-----------|
| 19 | `EventMapper.cs` | `ToEntity` (TileActivityRule) | `dto.AcceptedDropKeys ?? []`, `dto.Requirements?.Select(...) ?? []`, `dto.Modifiers?.Select(...) ?? []` | **No** (UI/API) |
| 20 | `EventMapper.cs` | `ToEntity` (TileActivityRule) | `dto.ActivityKey ?? string.Empty` | **No** |
| 21 | `TeamMapper.cs` | `ToResponse` | `strategy?.StrategyKey ?? string.Empty` | **No** |
| 22 | `DevSeedService.cs` | Seed | `team.StrategyConfig ?? new StrategyConfig(...)` | **No** (seed only) |

**Note:** Items 19–22 are in API/UI/seed paths. They may be kept for API robustness (e.g. malformed JSON from client) or removed if we want strict validation at API boundary. The plan focuses on simulation and snapshot paths unless otherwise noted.

---

## 2) New Invariants

### 2.1 Snapshot Fields — Required (Must Exist)

| DTO | Property | Requirement |
|-----|----------|-------------|
| `EventSnapshotDto` | `EventStartTimeEt` | **Required** when any player has a non-empty schedule. If all players always online, may be optional (see below). |
| `EventSnapshotDto` | `EventName`, `DurationSeconds`, `UnlockPointsRequiredPerRow`, `Rows`, `ActivitiesById`, `Teams` | Already required |
| `PlayerSnapshotDto` | `Schedule` | **Required** — always present. Use `WeeklyScheduleSnapshotDto` with `Sessions = []` for "always online". |
| `TeamSnapshotDto` | `StrategyKey` | Already required; must be non-empty |
| `ActivitySnapshotDto` | `ModeSupport` | **Required** — always present. No default; must be explicit. |
| `TileActivityRuleSnapshotDto` | `Modifiers` | **Required** — always present (non-null). Use `[]` for no modifiers. |
| `TileActivityRuleSnapshotDto` | `AcceptedDropKeys`, `RequirementKeys` | Already required |
| `OutcomeSnapshotDto` | `Grants` | Already required; must be non-null |

**Schedule policy:** If `EventStartTimeEt` is null → **fail** (snapshot invalid). If `Schedule` is null on a player → **fail**. Rationale: "always online" is represented explicitly as `Schedule` with `Sessions = []` or null Sessions, not by omitting the field. For simplicity, we can require `EventStartTimeEt` always (batch creation time is always known); null means "legacy, skip schedule" — we remove that.

**Alternative (simpler):** Require `EventStartTimeEt` always. Snapshot builder always sets it from `batch.CreatedAt`. No optional path. `Schedule` on player: require `Schedule` object; `Sessions` null/empty = always online. So we keep the *structure* but not the "omit Schedule" legacy.

### 2.2 Snapshot Fields — Optional (Explicit)

| DTO | Property | Requirement |
|-----|----------|-------------|
| `PlayerSnapshotDto` | `Schedule` | **Required** — but `Schedule.Sessions` may be null/empty (always online). |
| `ActivityModeSupportSnapshotDto` | `MinGroupSize`, `MaxGroupSize` | Optional; null = 1 and int.MaxValue respectively. **Keep** these as optional (semantic). |
| `TeamSnapshotDto` | `ParamsJson` | Optional (already) |

### 2.3 DB Fields — No EF Changes Required

- Event Rows are stored as JSON; no schema change. Validation happens at snapshot build.
- Team.StrategyConfig: Required for draft teams (already enforced by TeamService when running). Team without StrategyConfig → fail at batch start.

### 2.4 Behaviors Removed

- `rule?.Modifiers ?? []` → remove; require `Modifiers` non-null (use `[]` if none).
- `activity.ModeSupport ?? new ActivityModeSupportSnapshotDto()` → remove; require `ModeSupport` non-null.
- `EventStartTimeEt` null → remove; always require (snapshot builder always sets it).
- `Schedule` null on player → remove; require `Schedule` present (Sessions can be empty).
- `schedule?.Sessions` null/empty = always online → keep as **explicit** semantics: `Schedule` present with `Sessions = []` or null = always online. We do NOT "omit" Schedule; we always include it.
- `strategy?.StrategyKey ?? "RowRush"` → remove; require `StrategyConfig` on team at batch start.
- `activity` null in `SampleAttemptDuration` → remove; fail if activity missing.
- `activity.Attempts.Count == 0` → fail; invalid activity definition.
- `o.Grants?.Any(...)` → require `Grants` non-null; remove null check.
- `bands` null/empty in GroupScalingBandSelector → keep (1.0, 1.0) as valid "no scaling" — not legacy, it's semantic.
- `ModifierApplicator` rule null → `RollOutcome` and `SampleAttemptDuration` receive rule from `GetFirstEligibleActivity`; rule can be null when no activity found. Keep rule null handling for "no applicable rule" case — but that's a control-flow case, not legacy. Re-evaluate: if rule is always passed when we have an attempt, we can require non-null.

---

## 3) Validation Approach

### 3.1 SnapshotValidator (Application Layer)

**Location:** `BingoSim.Application/Simulation/Snapshot/SnapshotValidator.cs` (new)

**Responsibilities:**
- Validate `EventSnapshotDto` after deserialization or after building.
- Check all required fields present and valid.
- Fail fast with clear error messages.

**Validation rules:**
1. `EventStartTimeEt` must be non-null, non-empty, parseable as DateTimeOffset.
2. Each `Team` must have `StrategyKey` non-null, non-empty.
3. Each `Player` must have `Schedule` non-null (object present; `Sessions` may be null/empty).
4. Each `Activity` in `ActivitiesById` must have `ModeSupport` non-null.
5. Each `TileActivityRuleSnapshotDto` must have `Modifiers` non-null (use `[]` if none).
6. Each `OutcomeSnapshotDto` must have `Grants` non-null.
7. Each `Activity` must have at least one `Attempt` with at least one `Outcome`.
8. Each tile referenced in rows must have at least one `AllowedActivity` with valid `ActivityDefinitionId` present in `ActivitiesById`.

### 3.2 When to Validate

- **Batch start:** After `EventSnapshotBuilder.BuildSnapshotJsonAsync` returns, before persisting snapshot and creating runs. Validate the deserialized DTO.
- **Simulation run:** Before `SimulationRunner.Execute` runs, validate the snapshot. If validation fails, throw.

**Flow:**
1. `SimulationBatchService.StartBatchAsync` → builds snapshot JSON → deserializes → **SnapshotValidator.Validate** → if invalid, throw (fail request immediately).
2. `SimulationRunner.Execute` → deserializes snapshot → **SnapshotValidator.Validate** → if invalid, throw (run fails).

### 3.3 Invalid Snapshot Handling

**Recommendation: Option A — Fail the request immediately**

- At batch start: validation fails → throw `InvalidOperationException` (or custom `SnapshotValidationException`) with clear message. Batch is NOT created. No runs created.
- At run execution (worker): validation fails → throw (already happens). Run marked Failed. User sees error in UI.

**Rationale:** Simpler. No "Error" batch state. User fixes data and retries.

**Option B:** Create batch in Error state with message. Allows batch to exist for audit. More complex.

**Decision:** Option A — fail immediately.

### 3.4 Distributed Workers

- Workers receive `EventConfigJson` from DB (same snapshot as local). They deserialize and run. Validation runs in `SimulationRunner.Execute` (or before it). If invalid, throw → run fails → worker reports failure. Same behavior as local.

---

## 4) DB/Migration Changes

### 4.1 No EF Schema Changes Required

- Event, Team, ActivityDefinition, PlayerProfile, etc. — no schema changes.
- Snapshot is stored as JSON in `EventSnapshot.EventConfigJson`. Validation is at application level; we never persist invalid snapshots.

### 4.2 EventSnapshotBuilder Changes

- Ensure `EventStartTimeEt` is **always** set (from `eventStartTimeUtc` parameter). Caller (`SimulationBatchService`) always passes `batch.CreatedAt`.
- Ensure `Schedule` is **always** set on each player: use `MapSchedule(profile.WeeklySchedule)` which returns `WeeklyScheduleSnapshotDto` with `Sessions = []` when empty. Change `MapSchedule` to always return a non-null DTO (empty Sessions for always online).
- Ensure `StrategyKey` is never defaulted: require `StrategyConfig` on team. If missing, throw at batch start (before building snapshot).
- Ensure `ModeSupport` is always set on each `ActivitySnapshotDto` (already done in builder).
- Ensure `Modifiers` is always set on each `TileActivityRuleSnapshotDto` (already done in builder).

### 4.3 Migration Impact

- **None.** Wiping DB is acceptable. No migration needed.

---

## 5) Test Updates

### 5.1 Tests to Remove or Update

| Test | File | Action |
|------|------|--------|
| `BackwardCompat_NoScheduleInSnapshot_AllPlayersAlwaysOnline` | `ScheduleSimulationIntegrationTests.cs` | **Remove** — no longer valid; snapshot without Schedule is invalid |
| `ScheduleSimulationIntegrationTests.BuildMinimalSnapshotNoSchedule` | Same | Remove helper; no longer used |
| `GroupPlay_SnapshotWithoutModeSupport_RunsAsSolo` | `GroupPlayIntegrationTests.cs` | **Remove** — ModeSupport required |
| `GroupPlayIntegrationTests.BuildMinimalSnapshotWithoutModeSupport` | Same | Remove helper |
| `ModifierSimulationIntegrationTests.BackwardCompat_*` (or similar) | `ModifierSimulationIntegrationTests.cs` | **Remove** — snapshot without Modifiers is invalid |
| `ModifierSimulationIntegrationTests.BuildMinimalSnapshotWithoutModifiers` | Same | Remove helper |
| `ModifierApplicationTests.ComputeCombinedTimeMultiplier_NullRule_ReturnsOne` | `ModifierApplicationTests.cs` | **Update or remove** — if we require rule non-null in hot path, remove; else keep for defensive callers |
| `ModifierApplicationTests.ApplyProbabilityMultiplier_EmptyAcceptedDropKeys_ReturnsOriginalWeights` | Same | **Keep** — empty is valid (no scaling); null is not |
| `ScheduleEvaluatorTests.IsOnlineAt_EmptySchedule_ReturnsTrue` | `ScheduleEvaluatorTests.cs` | **Update** — `Schedule` with `Sessions = []` or null Sessions = always online. Rename to clarify. |
| `ScheduleEvaluatorTests.IsOnlineAt(null, ...)` | Same | **Remove** — null Schedule is invalid |

### 5.2 New Tests to Add

| Test | File | Description |
|------|------|-------------|
| `SnapshotValidator_EventStartTimeEtMissing_Throws` | `SnapshotValidatorTests.cs` (new) | Snapshot with null/empty EventStartTimeEt → validation fails |
| `SnapshotValidator_ScheduleMissingOnPlayer_Throws` | Same | Player with null Schedule → validation fails |
| `SnapshotValidator_ModeSupportMissing_Throws` | Same | Activity with null ModeSupport → validation fails |
| `SnapshotValidator_ModifiersNull_Throws` | Same | TileActivityRule with null Modifiers → validation fails |
| `SnapshotValidator_StrategyKeyMissing_Throws` | Same | Team with null/empty StrategyKey → validation fails |
| `SnapshotValidator_ValidSnapshot_Passes` | Same | Full valid snapshot → no exception |
| `SimulationRunner_InvalidSnapshot_Throws` | `SimulationRunnerTests.cs` or integration | Runner receives invalid snapshot → throws before execution |
| `SimulationBatchService_TeamWithoutStrategyConfig_Throws` | Integration | Start batch with team missing StrategyConfig → throws |

### 5.3 Tests to Update (Keep, Adjust)

| Test | Change |
|------|--------|
| `IsOnlineAt_EmptySchedule_ReturnsTrue` | Use `Schedule` with `Sessions = []` or `Sessions = null`; ensure we never pass null Schedule |
| `PlayerWithOneHourPerDay_ProgressesLessThanAlwaysOnline` | Ensure snapshot has explicit Schedule for both teams |
| `Determinism_SameSeedAndSchedule_SameOutcomes` | No change |
| `GroupPlay_EightPlayersMaxFourPerGroup_FormsMultipleConcurrentGroups` | Ensure ModeSupport is explicit |
| All other integration tests | Ensure snapshots have all required fields |

---

## 6) Summary Checklist

- [ ] Create `SnapshotValidator` in Application
- [ ] Add validation at batch start (after building snapshot)
- [ ] Add validation at simulation run start (in SimulationRunner or before)
- [ ] Update `EventSnapshotBuilder`: always set EventStartTimeEt, always set Schedule on players, require StrategyConfig
- [ ] Update `MapSchedule`: return non-null DTO (empty Sessions for always online)
- [ ] Remove `rule?.Modifiers ?? []` in ModifierApplicator; require non-null
- [ ] Remove `activity.ModeSupport ?? new ActivityModeSupportSnapshotDto()` in SimulationRunner
- [ ] Remove `eventStartEt` null branch (always require)
- [ ] Remove `o.Grants?.Any(...) ?? false`; require Grants non-null
- [ ] Remove `activity` null in SampleAttemptDuration; fail
- [ ] Remove `StrategyKey ?? "RowRush"` in EventSnapshotBuilder; require StrategyConfig
- [ ] Update ScheduleEvaluator: document that Schedule must be non-null (caller responsibility)
- [ ] Remove/update backward-compat tests
- [ ] Add SnapshotValidator tests
- [ ] Add simulation fail-fast tests

---

## 7) Execution Order

1. Create `SnapshotValidator` and tests
2. Update `EventSnapshotBuilder` to always populate required fields
3. Remove `StrategyKey ?? "RowRush"` in builder; add validation at batch start for StrategyConfig
4. Integrate validation into batch start and simulation run
5. Remove legacy null coalescing in SimulationRunner, ModifierApplicator, ScheduleEvaluator
6. Update snapshot DTOs (remove init defaults where we want fail-fast)
7. Update/remove tests
8. Manual verification: local + distributed execution, UI
