# Strategy-Specific Behavior with Group Activities: Investigation

## Executive Summary

**Critical finding:** The seed data configures **both** Team Alpha and Team Beta to use **RowUnlocking**. There is no ComboUnlocking in the default seeded state. When running 50k simulations with the seeded event (Winter Bingo 2025 / Spring League Bingo), the 0% vs 100% result is **Team Alpha vs Team Beta** (different players and schedules), **not** RowUnlocking vs ComboUnlocking.

**Root cause:** Test configuration error. The "strategy comparison" is actually a team composition comparison.

---

## 1. E2E Test Configuration

### 1.1 How Batches Are Started

| Path | Snapshot Source | Team Strategies |
|------|-----------------|-----------------|
| Web UI → Run Simulations | `EventSnapshotBuilder.BuildSnapshotJsonAsync(eventId, batch.CreatedAt)` | From database (teams for event) |
| `BingoSim.Seed --perf` | Same (event from DB by name) | From database |
| Integration tests (DistributedBatch, etc.) | In-memory `BuildMinimalSnapshotJson()` | Hardcoded (single team, RowUnlocking) |

### 1.2 Snapshot Build Flow

```
SimulationBatchService.StartBatchAsync(request)
  → teamRepo.GetByEventIdAsync(request.EventId)     // Teams from DB
  → snapshotBuilder.BuildSnapshotJsonAsync(eventId, batch.CreatedAt)
       → teamRepo.GetByEventIdAsync(eventId)        // Same teams
       → For each team: strategy.StrategyKey        // From team.StrategyConfig
```

**Source of truth:** `team.StrategyConfig.StrategyKey` from the database. No override.

### 1.3 Seed Data Team Creation

**File:** `BingoSim.Application/Services/DevSeedService.cs`

**Team Alpha (lines 374–388):**
```csharp
if (teamAlpha is not null)
{
    var strategyConfig = teamAlpha.StrategyConfig ?? new StrategyConfig(teamAlpha.Id, StrategyCatalog.RowUnlocking, "{}");
    strategyConfig.Update(StrategyCatalog.RowUnlocking, "{}");  // ← Always RowUnlocking
    await teamRepo.UpdateAsync(teamAlpha, strategyConfig, teamPlayers, ...);
}
else
{
    var config = new StrategyConfig(team.Id, StrategyCatalog.RowUnlocking, "{}");  // ← RowUnlocking
    await teamRepo.AddAsync(team, config, teamPlayers, ...);
}
```

**Team Beta (lines 391–406):**
```csharp
if (teamBeta is not null)
{
    var strategyConfig = teamBeta.StrategyConfig ?? new StrategyConfig(teamBeta.Id, StrategyCatalog.RowUnlocking, "{}");
    strategyConfig.Update(StrategyCatalog.RowUnlocking, "{}");  // ← Always RowUnlocking
    await teamRepo.UpdateAsync(teamBeta, strategyConfig, teamPlayers, ...);
}
else
{
    var config = new StrategyConfig(team.Id, StrategyCatalog.RowUnlocking, "{}");  // ← RowUnlocking
    await teamRepo.AddAsync(team, config, teamPlayers, ...);
}
```

**Both teams are explicitly set to `StrategyCatalog.RowUnlocking`.** There is no ComboUnlocking in the seed.

---

## 2. Strategy Assignment Verification

### 2.1 What the Seed Does

| Team | Strategy (Seed) | Players |
|------|-----------------|---------|
| Team Alpha | **RowUnlocking** | Alice, Bob, Carol, Dave |
| Team Beta | **RowUnlocking** | Eve, Frank, Grace, Henry |

### 2.2 What the E2E Test Assumes (from row-unlocking-bug-investigation.md)

| Team | Assumed Strategy | Actual (Seed) |
|------|------------------|---------------|
| Team Alpha | RowUnlocking | RowUnlocking ✓ |
| Team Beta | ComboUnlocking | **RowUnlocking** ✗ |

### 2.3 Conclusion

**The E2E test (or manual 50k batch run) does not override the seed.** When using the seeded event:

- Both teams use RowUnlocking
- The 0% vs 100% result compares **Team Alpha vs Team Beta**, not strategies
- The difference is due to **team composition** (players, schedules), not strategy logic

---

## 3. Why Team Alpha vs Team Beta Differs

With both using RowUnlocking, the performance gap comes from:

### 3.1 Different Players

| Team | Players | quest.sote Holders |
|------|---------|--------------------|
| Alpha | Alice, Bob, Carol, Dave | Carol, Dave |
| Beta | Eve, Frank, Grace, Henry | Grace, Henry |

### 3.2 Different Schedules

Alpha and Beta have different weekly schedules. Row 8 requires CoX or ToA (group-only, 2+ players). Schedule overlap for 2+ quest.sote players differs between teams.

### 3.3 Event Queue Ordering

Events are prioritized by `(endTime, teamIndex, firstPlayerIndex)`. Team Alpha is index 0, Beta is index 1. When times tie, Alpha is processed first. This can create systematic differences in how often each team gets "turns."

---

## 4. Integration Tests vs Seed Data

| Test | Uses Seed? | Team Strategies |
|------|-----------|-----------------|
| StrategyComparisonIntegrationTests | No (in-memory snapshot) | Alpha=RowUnlocking, Beta=Greedy or ComboUnlocking (explicit in test) |
| Row8DiagnosticInvestigationTests | No (BuildTwentyRowSnapshot) | Alpha=RowUnlocking, Beta=ComboUnlocking (explicit) |
| DistributedBatchIntegrationTests | No (BuildMinimalSnapshotJson) | Single team, RowUnlocking |
| **Manual batch / BingoSim.Seed --perf** | **Yes (DB)** | **Both RowUnlocking (from seed)** |

Unit/integration tests that compare strategies build their own snapshots with explicit strategy keys. The production batch flow uses the database, where the seed sets both teams to RowUnlocking.

---

## 5. Root Cause

**Finding 1: Test Configuration Error (Confirmed)**

- **Issue:** E2E runs with the seeded event use both teams with RowUnlocking.
- **Result:** The 0% vs 100% outcome is Team Alpha vs Team Beta, not strategy vs strategy.
- **Implication:** No strategy-specific behavior with group activities has been demonstrated. The comparison is invalid for strategy evaluation.

---

## 6. Proposed Fix

### 6.1 For Strategy Comparison E2E Tests

**Option A: Update the seed**

Change `DevSeedService.SeedTeamsAsync` so Team Beta uses ComboUnlocking:

```csharp
// Team Beta
strategyConfig.Update(StrategyCatalog.ComboUnlocking, "{}");
// or
var config = new StrategyConfig(team.Id, StrategyCatalog.ComboUnlocking, "{}");
```

**Option B: Add a strategy-comparison seed variant**

Introduce a separate seed path (e.g. `--strategy-compare`) that creates:

- Team Alpha: RowUnlocking, players 1–4
- Team Beta: ComboUnlocking, players 5–8
- Same event, same players, same schedules (e.g. always-online for deterministic comparison)

**Option C: Document and require manual override**

Document that for strategy comparison, Team Beta’s strategy must be set to ComboUnlocking in the UI before running a batch. The seed will not do this by default.

### 6.2 For Fair Strategy Comparison

To isolate strategy behavior from team composition:

1. Use identical player configs (same players or same capabilities/schedules) for both teams.
2. Use always-online schedules for deterministic runs.
3. Or use a dedicated seed variant with Alpha=RowUnlocking, Beta=ComboUnlocking, and identical team configs.

---

## 7. Verification Checklist

| Check | Result |
|-------|--------|
| Are E2E teams using different strategies? | **No** – both use RowUnlocking from seed |
| If yes, what differs in row 8 handling? | N/A – strategies are the same |
| If no, what causes the 0% vs 100% gap? | **Team composition** (Alpha vs Beta players/schedules) |
| Is this a bug or expected? | **Test configuration error** – not a strategy bug |

---

## 8. Summary

| Finding | Detail |
|---------|--------|
| **Seed configuration** | Both Team Alpha and Team Beta use RowUnlocking |
| **Snapshot source** | Batches use teams from DB; no strategy override |
| **50k E2E setup** | Uses seeded event → both teams RowUnlocking |
| **0% vs 100% cause** | Team Alpha vs Team Beta (composition), not strategy |
| **Fix** | Update seed so Beta uses ComboUnlocking, or add a strategy-comparison seed variant |

---

## Appendix: Code References

### DevSeedService.SeedTeamsAsync (excerpt)

```csharp
// Lines 475-476: Team Alpha
strategyConfig.Update(BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");

// Lines 492-493: Team Beta  
strategyConfig.Update(BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
```

### EventSnapshotBuilder (excerpt)

```csharp
// Line 144: Strategy comes from database
StrategyKey = strategy.StrategyKey,
```

### SimulationBatchService (excerpt)

```csharp
// Line 60: Snapshot built from DB
snapshotJson = await snapshotBuilder.BuildSnapshotJsonAsync(request.EventId, batch.CreatedAt, cancellationToken);
```
