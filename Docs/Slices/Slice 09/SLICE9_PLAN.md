# Slice 9: Group Play (PerGroup Roll Scope + GroupScalingBands + Group Formation) — Plan Only

**Scope:** Implement group formation for group-capable activities, PerGroup vs PerPlayer roll scope behavior, and GroupScalingBands application during simulation.

**Source of truth:**
- `Docs/06_Acceptance_Tests.md` — Group activities, per-group roll scope, scaling
- `Docs/02_Domain.md` — ActivityDefinition, roll scope, scaling bands
- `Docs/08_Feature_Audit_2025.md` — Group play features missing or incomplete

**Constraints:** Clean Architecture; simulation engine stays in Application; snapshot is source of truth; same seed + inputs ⇒ same outputs (determinism). Modifiers (Slice 8) already implemented; group scaling must stack with modifiers.

---

## 1) Current State: Roll Scope and Scaling Bands in Snapshot and Simulation

### 1.1 Snapshot Model

| Location | Field | Status |
|----------|-------|--------|
| `AttemptSnapshotDto` | `RollScope` (int: 0=PerPlayer, 1=PerGroup) | ✅ Present |
| `ActivitySnapshotDto` | `GroupScalingBands` (List&lt;GroupSizeBandSnapshotDto&gt;) | ✅ Present |
| `ActivitySnapshotDto` | `ModeSupport` (SupportsSolo, SupportsGroup, MinGroupSize, MaxGroupSize) | ❌ **Missing** |

**Current usage:**
- `EventSnapshotBuilder` populates `Attempts` (with RollScope) and `GroupScalingBands` when building `ActivitySnapshotDto`.
- `ModeSupport` is **not** included in the snapshot; group formation cannot determine if an activity supports group play or valid group size bounds.

### 1.2 Simulation Usage

| Component | Current Behavior |
|-----------|------------------|
| `GetFirstEligibleActivity` | Returns `activity.Attempts[0].Key` only — **first attempt only**; ignores other loot lines |
| `SimulationRunner` main loop | All work is **per-player**; `SimEvent` has `(teamIndex, playerIndex)`; no group concept |
| `SampleAttemptDuration` | Uses `skillMultiplier` (single player) and `ModifierApplicator` for time; **no group scaling** |
| `RollOutcome` | Uses `ModifierApplicator` for probability; **no group scaling**; roll scope **ignored** |

**Gap:** Simulation treats every attempt as a single-player activity. PerGroup loot lines are never simulated; GroupScalingBands are never applied.

---

## 2) Snapshot Model Additions Required

### 2.1 ActivityModeSupportSnapshotDto (New)

**Location:** `BingoSim.Application/Simulation/Snapshot/ActivityModeSupportSnapshotDto.cs`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| SupportsSolo | bool | yes | Activity can be done by 1 player |
| SupportsGroup | bool | yes | Activity can be done by 2+ players |
| MinGroupSize | int? | no | Minimum group size when SupportsGroup; null = 1 |
| MaxGroupSize | int? | no | Maximum group size; null = unbounded (use band max) |

### 2.2 Extend ActivitySnapshotDto

**Location:** `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs`

Add property:
- `ModeSupport`: `ActivityModeSupportSnapshotDto` — default to `{ SupportsSolo: true, SupportsGroup: false }` for backward compatibility with existing snapshot JSON.

### 2.3 EventSnapshotBuilder Changes

**Location:** `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs`

When building `ActivitySnapshotDto`, add:
```csharp
ModeSupport = new ActivityModeSupportSnapshotDto
{
    SupportsSolo = activity.ModeSupport.SupportsSolo,
    SupportsGroup = activity.ModeSupport.SupportsGroup,
    MinGroupSize = activity.ModeSupport.MinGroupSize,
    MaxGroupSize = activity.ModeSupport.MaxGroupSize
}
```

---

## 3) Simulation Algorithm Changes

### 3.1 Where Grouping Occurs

**Policy:** For each team and each eligible tile, determine whether the activity supports group play. If `ModeSupport.SupportsGroup` is true and there are multiple eligible players for that tile, form groups greedily.

**Group formation algorithm:**
1. **Eligible players:** Players who (a) are not currently busy (in queue with end time > simTime), (b) meet the tile’s `RequirementKeys`, (c) have at least one eligible tile to work on.
2. **Ordering (determinism):** Sort eligible players by `PlayerId` (ascending) for stable group formation.
3. **Group size:** For each group-capable activity:
   - `minSize = ModeSupport.MinGroupSize ?? 1`
   - `maxSize = ModeSupport.MaxGroupSize ?? (largest band MaxSize, or team size)`
   - Form groups of size `maxSize` when possible; remainder forms a smaller group or solo (if `SupportsSolo` and size 1).
4. **Greedy assignment:** Iterate tiles (row order, then points). For each tile, form as many groups as possible from currently idle eligible players. Each group gets one `SimEvent`. Players in a group are “busy” until the group’s attempt completes.

**Solo fallback:** If `SupportsSolo` and only 1 player is eligible, or no group can be formed, schedule a solo attempt (group size = 1).

### 3.2 Event Model Change: SimEvent

**Current:** `SimEvent(simTime, teamIndex, playerIndex, activityId, attemptKey, rule)`

**New:** Support both solo and group:
- **Solo:** `PlayerIndices = [pi]` (single player)
- **Group:** `PlayerIndices = [pi1, pi2, ...]` (sorted by PlayerId for determinism)

Rename or extend:
- `SimEvent.TeamIndex` (unchanged)
- `SimEvent.PlayerIndices`: `IReadOnlyList<int>` — one or more player indices in the group
- `SimEvent.ActivityId`, `AttemptKey`, `Rule` (unchanged)

**Priority queue:** Priority remains `endTime`. When dequeuing, the event represents one completed attempt (solo or group).

### 3.3 How Group Attempts Advance Simulated Time

- **Duration:** One attempt consumes one unit of simulated time.
- **Group duration formula:** `effectiveTime = baseTime * groupTimeMultiplier * modifierTimeMultiplier * skillMultiplier`
  - `baseTime` = baseline + variance from the attempt’s `AttemptTimeModel`
  - `groupTimeMultiplier` = from `GroupSizeBand` for `groupSize`
  - `modifierTimeMultiplier` = from `ModifierApplicator` (Slice 8)
  - `skillMultiplier` = for groups: use **maximum** (slowest) `SkillTimeMultiplier` among group members (group waits for slowest)
- **End time:** `endTime = simTime + effectiveTime`; event is enqueued with priority `endTime`.

### 3.4 How Roll Scopes Are Executed Per Loot Line

**Critical change:** Process **all** attempt definitions for the activity, not just the first.

When an attempt event fires (solo or group):

1. **For each** `AttemptSnapshotDto` in `activity.Attempts` (in definition order):
   - **PerPlayer (RollScope == 0):** Roll once per player in the group. Each roll uses that player’s capability set for modifiers. Apply group scaling to probability. Collect all grants.
   - **PerGroup (RollScope == 1):** Roll once for the entire group. Use **union** of group members’ capability keys for modifier applicability (if any member has the modifier, it applies). Apply group scaling to probability. Collect grants (single shared result).

2. **Grants:** All grants (from PerPlayer and PerGroup rolls) are allocated via the existing allocator to eligible tiles. PerGroup grants contribute to team progress like PerPlayer grants.

**Time model for multi-attempt activities:** Use the **maximum** `BaselineTimeSeconds` and `VarianceSeconds` across all attempts as the attempt duration. (One logical “run” of the activity encompasses all loot lines; time is the longest phase.) Alternatively, use the first attempt’s time for backward compatibility; document the choice. **Recommendation:** Use max across attempts to reflect that a CoX run (scavs + olm) takes the combined effort.

**Refinement:** Domain shows CoX with scavs (600s) and olm (900s) as separate attempts. A full “raid” could be modeled as one 900s attempt (olm dominates) during which we roll both scavs (PerGroup) and olm (PerPlayer). Use **first** attempt’s time as the primary duration for scheduling (backward compatible); when the event fires, execute all attempts’ rolls. This keeps the current “one event = one time sample” model and adds multi-line execution.

**Final choice:** Use **first attempt’s time** for scheduling (unchanged). When the event completes, execute **all** attempt definitions: PerPlayer rolls per player, PerGroup rolls once. Group scaling applies to duration (from first attempt’s band) and to each roll’s probability.

---

## 4) Exact Multiplier Stacking Rules (Group + Modifiers)

### 4.1 Time Multipliers

```
effectiveTime = baseTime * skillMultiplier * groupTimeMultiplier * modifierTimeMultiplier
```

Where:
- `baseTime` = `BaselineTimeSeconds + rng.Next(-VarianceSeconds, VarianceSeconds+1)` (clamped to ≥ 1 before multipliers)
- `skillMultiplier` = for group: `max(players.SkillTimeMultiplier)`; for solo: that player’s value
- `groupTimeMultiplier` = from band for `groupSize` (see §4.3)
- `modifierTimeMultiplier` = `ModifierApplicator.ComputeCombinedTimeMultiplier(rule, capabilityKeys)`; for group use **union** of members’ capability keys

**Clamp:** `effectiveTime = Max(1, (int)Math.Floor(effectiveTime))`

### 4.2 Probability Multipliers

For each outcome weight scaling:
```
effectiveProbMultiplier = groupProbMultiplier * modifierProbMultiplier
```

- `modifierProbMultiplier` = `ModifierApplicator.ComputeCombinedProbabilityMultiplier(rule, capabilityKeys)`
- `groupProbMultiplier` = from band for `groupSize`
- Pass `effectiveProbMultiplier` to `ModifierApplicator.ApplyProbabilityMultiplier` (or apply both: first modifier, then group, or multiply and pass product). **Implementation:** Compute `combined = groupProbMultiplier * modifierProbMultiplier` and pass to weight adjustment logic.

**Clamping:** Same as Slice 8 — weights clamped to [0, MaxAdjustedWeight]; if total weight ≤ 0, use original weights.

### 4.3 Band Selection

**Rule:** Choose the first `GroupSizeBand` where `MinSize <= groupSize <= MaxSize`. If no band matches, use `1.0m` for both time and probability multipliers. If `GroupScalingBands` is null/empty, use `1.0m`.

**Order:** Bands should be ordered by MinSize (ascending) in the snapshot. Iterate in order; return first match.

---

## 5) Determinism Choices

### 5.1 Player Ordering

- **Group formation:** Sort eligible players by `PlayerId` (ascending, `Guid` comparison) before forming groups. Same team + same tick ⇒ same group composition.
- **SimEvent.PlayerIndices:** Store sorted by `PlayerId` so that event processing is order-independent for group identity.

### 5.2 Random Usage

- **RNG:** Same `Random` instance seeded from `SeedDerivation.DeriveRngSeed(runSeedString, runIndex)`. No additional entropy.
- **Roll order:** When executing multiple loot lines, process attempts in definition order. PerPlayer: process players in `PlayerIndices` order. Each roll consumes RNG in a fixed sequence.

### 5.3 Group Formation Stability

- At a given `simTime`, the set of “idle” players is deterministic (events complete in order of `endTime`; ties broken by insertion order or team/player index).
- Group formation is a pure function of (eligible players, activity ModeSupport, band bounds) with deterministic ordering.

---

## 6) Test Plan and Exact Test Cases

### 6.1 Unit Tests — Scaling Band Selection

**Location:** `Tests/BingoSim.Application.UnitTests/Simulation/` (e.g. `GroupScalingBandSelectorTests.cs` or equivalent)

| Test | Description |
|------|-------------|
| `SelectBand_GroupSizeInRange_ReturnsMatchingBand` | Bands [1-1], [2-4], [5-8]; groupSize=3 ⇒ returns band with MinSize=2, MaxSize=4 |
| `SelectBand_GroupSizeAtBoundary_ReturnsBand` | groupSize=4 ⇒ returns [2-4] band |
| `SelectBand_NoMatchingBand_ReturnsIdentityMultipliers` | groupSize=10, no band for 10 ⇒ TimeMultiplier=1.0, ProbabilityMultiplier=1.0 |
| `SelectBand_EmptyBands_ReturnsIdentity` | GroupScalingBands=[] ⇒ 1.0 |
| `SelectBand_GroupSize1_ReturnsSoloBand` | Band [1-1] with 1.0, 1.0 ⇒ returns that band |

### 6.2 Unit Tests — Per-Group vs Per-Player Roll Scope

| Test | Description |
|------|-------------|
| `RollScope_PerPlayer_OneRollPerPlayer` | Activity with PerPlayer attempt; group of 3; exactly 3 rolls; grants aggregated |
| `RollScope_PerGroup_OneRollForGroup` | Activity with PerGroup attempt; group of 3; exactly 1 roll; grants applied once |
| `RollScope_Mixed_SameSeed_Deterministic` | Activity with PerPlayer + PerGroup; same seed ⇒ identical grants sequence |

### 6.3 Unit Tests — Deterministic Group Formation

| Test | Description |
|------|-------------|
| `GroupFormation_SamePlayersSameOrder_SameGroups` | 4 players, same PlayerIds and ordering; two runs ⇒ identical group composition |
| `GroupFormation_PlayerOrderStable_ByPlayerId` | Players A,B,C,D (by Id); groups of 2 ⇒ (A,B), (C,D) consistently |
| `GroupFormation_SoloWhenSupportsSoloOnly_SinglePlayerGroups` | Activity SupportsSolo only; each player gets solo attempt |

### 6.4 Integration Tests

**Location:** `Tests/BingoSim.Application.UnitTests/Simulation/` or `Tests/BingoSim.Infrastructure.IntegrationTests/`

| Test | Description |
|------|-------------|
| `GroupPlay_ActivityWithPerPlayerAndPerGroup_SameSeed_Reproducible` | Build activity with PerPlayer (common) + PerGroup (rare) loot lines. Run with seed S. Re-run with seed S. Assert identical TotalPoints, TilesCompletedCount per team. |
| `GroupPlay_Team1PlayerVsTeam4Players_OutcomesDiffer` | Same activity, same seed. Team A: 1 player. Team B: 4 players. Assert Team B has different (and typically better due to scaling) results. |
| `GroupPlay_GroupScalingBands_AppliedToTimeAndProbability` | Activity with bands (1,1) 1.0/1.0 and (2,4) 0.85/1.1. Team of 4. Assert attempt durations shorter and/or grant rates higher than solo. |
| `DistributedBatch_GroupSnapshotFields_PresentAndUsed` | Run distributed batch with snapshot containing ModeSupport and GroupScalingBands. Assert batch completes; results reflect group behavior (smoke test). |

### 6.5 Optional: Debug Counters

If cheap to add: `TeamRunResultDto` (or internal state) could include `GroupAttemptsCount` and `SoloAttemptsCount` for diagnostics. Not required for Slice 9; can be a follow-up.

---

## 7) Exact List of Files to Modify/Create

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivityModeSupportSnapshotDto.cs` | Snapshot DTO for ModeSupport |
| `BingoSim.Application/Simulation/GroupScalingBandSelector.cs` | Pure function: (bands, groupSize) ⇒ (timeMultiplier, probMultiplier) |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupScalingBandSelectorTests.cs` | Unit tests for band selection |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlaySimulationTests.cs` | Unit tests for roll scope, group formation, determinism |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlayIntegrationTests.cs` | Integration tests (PerPlayer+PerGroup, 1 vs 4 players, scaling) |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs` | Add `ModeSupport` property |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `ModeSupport` when building ActivitySnapshotDto |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Group formation; SimEvent with PlayerIndices; process all attempts; apply group scaling in SampleAttemptDuration and RollOutcome; multi-line execution |
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | No API change; callers pass combined multiplier. Optionally add overload for group+modifier combination. |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Add `DistributedBatch_GroupSnapshotFields_PresentAndUsed` |

### Files to Review (No Changes Expected)

| File | Notes |
|------|-------|
| `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs` | Already has Modifiers |
| `BingoSim.Application/Simulation/Snapshot/GroupSizeBandSnapshotDto.cs` | Already correct |
| `BingoSim.Application/Simulation/Snapshot/AttemptSnapshotDto.cs` | Already has RollScope |

---

## 8) Implementation Order Recommendation

1. Add `ActivityModeSupportSnapshotDto` and extend `ActivitySnapshotDto` + `EventSnapshotBuilder`
2. Implement `GroupScalingBandSelector` and unit tests
3. Extend `SimEvent` to support `PlayerIndices`; implement group formation in initial queue and re-enqueue logic
4. Update `SampleAttemptDuration` to accept group size and apply group time scaling; combine with modifier
5. Update `RollOutcome` (or equivalent) to accept group size and apply group probability scaling
6. Change `GetFirstEligibleActivity` / activity selection to return all attempts (or iterate all in runner)
7. Execute all loot lines (PerPlayer per player, PerGroup once) when event fires
8. Add group capability union for modifier applicability
9. Add unit and integration tests; distributed smoke test

---

*End of plan. Do NOT write code until plan is approved.*
