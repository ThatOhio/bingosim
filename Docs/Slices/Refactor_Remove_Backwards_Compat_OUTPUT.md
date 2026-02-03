# Refactor: Remove Backwards-Compatibility Paths — Implementation Output

**Completed:** February 3, 2025  
**Status:** All tests passing (406 total)

---

## Files Changed

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/SnapshotValidator.cs` | Validates EventSnapshotDto; throws SnapshotValidationException on missing/invalid required fields |
| `BingoSim.Application/Simulation/Snapshot/SnapshotValidationException.cs` | Custom exception for snapshot validation failures |
| `Tests/BingoSim.Application.UnitTests/Simulation/SnapshotValidatorTests.cs` | Unit tests for SnapshotValidator |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | `eventStartTimeUtc` now required; `MapSchedule` always returns non-null (empty Sessions for always-online); throws if team has no StrategyConfig |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotDto.cs` | `EventStartTimeEt` now `required` |
| `BingoSim.Application/Simulation/Snapshot/PlayerSnapshotDto.cs` | `Schedule` now `required` |
| `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs` | `ModeSupport` now `required` (removed init default) |
| `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs` | `Modifiers` now `required` (removed init default) |
| `BingoSim.Application/Simulation/Snapshot/AttemptSnapshotDto.cs` | `VarianceSeconds` changed from `int?` to `int` (normalized at build time) |
| `BingoSim.Application/Services/SimulationBatchService.cs` | Validates snapshot after build; on failure sets batch to Error and returns; catches build failures (e.g. no StrategyConfig) |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Validates snapshot at start; `eventStartEt` always required; removed null coalescing for ModeSupport, eventStartEt; fail-fast for missing activity |
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | `rule` now required; removed `rule?.Modifiers ?? []`; `acceptedDropKeys` required; removed `o.Grants?.Any(...) ?? false` |
| `BingoSim.Application/Simulation/GroupScalingBandSelector.cs` | `rule` parameter now required |
| `BingoSim.Application/Simulation/Schedule/ScheduleEvaluator.cs` | `schedule` parameter now required (non-null); Sessions null/empty = always online |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleSimulationIntegrationTests.cs` | Removed `BackwardCompat_NoScheduleInSnapshot_AllPlayersAlwaysOnline`; removed `BuildMinimalSnapshotNoSchedule`; added Schedule to all players |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlayIntegrationTests.cs` | Removed `GroupPlay_SnapshotWithoutModeSupport_RunsAsSolo`; removed `BuildMinimalSnapshotWithoutModeSupport`; added EventStartTimeEt and Schedule to all snapshots |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierSimulationIntegrationTests.cs` | Removed `Execute_SnapshotWithoutModifiers_RunsSuccessfully`; removed `BuildMinimalSnapshotWithoutModifiers`; added EventStartTimeEt, Schedule, ModeSupport |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleEvaluatorTests.cs` | Removed `IsOnlineAt(null, ...)`; added `IsOnlineAt_NullSessions_ReturnsTrue` |
| `Tests/BingoSim.Application.UnitTests/Simulation/ModifierApplicationTests.cs` | Removed `ComputeCombinedTimeMultiplier_NullRule_ReturnsOne` |
| `Tests/BingoSim.Application.UnitTests/Simulation/GroupPlaySimulationTests.cs` | Added EventStartTimeEt and Schedule to all snapshots |
| `Tests/BingoSim.Application.UnitTests/Simulation/SimulationRunnerReproducibilityTests.cs` | Added EventStartTimeEt, Schedule, ModeSupport, Modifiers |
| `Tests/BingoSim.Application.UnitTests/Simulation/EventSnapshotBuilderModifierTests.cs` | Added `eventStartTimeUtc` argument; added StrategyConfig to test team |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Added EventStartTimeEt, Schedule, ModeSupport, Modifiers to all snapshot builders |

---

## Migration Impact

**No EF migrations created.** Snapshot is stored as JSON in `EventSnapshot.EventConfigJson`. All validation is at the application layer. Existing snapshots in the DB will fail validation if they lack required fields.

**DB wipe is acceptable** per project context. After wipe, re-seed to populate fresh data.

---

## Commands

### Run Tests

```bash
dotnet test
```

All 406 tests pass (Core: 151, Application: 191, Infrastructure: 64).

### Wipe DB and Re-seed

```bash
# Ensure PostgreSQL is running (e.g. via Docker)
docker compose up -d

# Full wipe (required when upgrading; snapshots are not backward compatible)
dotnet run --project BingoSim.Seed -- --full-reset --confirm

# Then re-seed
dotnet run --project BingoSim.Seed
```

### Local Batch Run

1. Start the web app: `dotnet run --project BingoSim.Web`
2. Navigate to an event with drafted teams (each team must have StrategyConfig)
3. Click "Run Simulations" → configure runs → Start
4. Batch should complete; runs execute locally via `SimulationRunQueueHostedService`

### Distributed Batch Run (2 Workers)

1. Start infrastructure: `docker compose up -d` (PostgreSQL + RabbitMQ)
2. Start Web: `dotnet run --project BingoSim.Web`
3. Start Worker 1: `dotnet run --project BingoSim.Worker`
4. Start Worker 2 (in another terminal): `dotnet run --project BingoSim.Worker`
5. Create event, draft teams (with strategy), run batch with ExecutionMode=Distributed
6. Both workers consume runs; batch completes when all runs are done

---

## Quick Manual Validation Steps

### 1. Local Batch Run

- Create event with tiles and activities
- Draft at least one team (ensure strategy is configured)
- Run batch (e.g. 10 runs, Local)
- Verify batch completes; aggregates visible

### 2. Snapshot Validation Failure (Error Batch)

- Create event and team **without** StrategyConfig (e.g. via direct DB or seed gap)
- Attempt to run batch
- Batch should be created in **Error** state with message: "Team 'X' has no StrategyConfig. Configure strategy before running."
- UI should show batch with Error status and error message

### 3. Distributed Batch Run

- Start Web + 2 Workers
- Run batch with Distributed mode
- Verify runs are distributed across workers; batch completes

### 4. Schedule Enforcement

- Create player with weekly schedule (e.g. 1h/day)
- Run batch; compare with always-online player
- Scheduled player should progress less than always-online

---

## Summary of Removed Behaviors

- `Modifiers` null/empty → no longer tolerated; required, use `[]` for none
- `ModeSupport` null → no longer tolerated; required
- `EventStartTimeEt` null → no longer tolerated; required
- `Schedule` null on player → no longer tolerated; required (Sessions=[] for always-online)
- `StrategyKey ?? "RowRush"` → removed; team must have StrategyConfig
- `activity.ModeSupport ?? new()` → removed
- `eventStartEt` null (skip schedule) → removed; always enforced
- `o.Grants?.Any(...) ?? false` → removed; Grants required
- `activity` null in SampleAttemptDuration → now throws
- `activity.Attempts.Count == 0` → now throws
- `VarianceSeconds ?? 0` → normalized at build time (int)

---

## Invariants Enforced

| Field | Requirement |
|-------|-------------|
| `EventSnapshotDto.EventStartTimeEt` | Required, parseable as DateTimeOffset |
| `PlayerSnapshotDto.Schedule` | Required (Sessions null/empty = always online) |
| `ActivitySnapshotDto.ModeSupport` | Required |
| `ActivitySnapshotDto.GroupScalingBands` | Required (empty list allowed) |
| `TileActivityRuleSnapshotDto.Modifiers` | Required (empty list allowed) |
| `OutcomeSnapshotDto.Grants` | Required, non-null |
| `TeamSnapshotDto.StrategyKey` | Required, non-empty |
| Team.StrategyConfig | Required at batch start (builder throws if missing) |

---

## Step 3 — HARDEN (Verify, Clean Up, Document)

**Completed:** February 3, 2025

### Behavior Checks ✓

- **Batch start (local + distributed):** Valid config works. `SimulationBatchService.StartBatchAsync` builds snapshot, validates, creates runs, enqueues or publishes.
- **Results pages:** Batch list (`/simulations/results`) and batch details (`/simulations/results/{id}`) work. Error batches show status and `ErrorMessage`.
- **Invalid snapshot/config fails fast:** No silent fallback. Validation fails → batch set to Error with clear message; Run Simulations page shows error inline (does not navigate away).

### Performance Sanity ✓

- **Validation occurs once at batch start** — `SnapshotValidator.Validate` in `SimulationBatchService` before creating runs.
- **Validation occurs once per run start** — `SnapshotValidator.Validate` at start of `SimulationRunner.Execute` (guard for distributed workers receiving snapshot from DB).
- **No validation in hot path** — No validation inside simulation loop, per-attempt, or per-tile logic.

### Developer Experience Updates

- **README.md:** Added "Upgrading / Database Wipe" section — DB wipe expected when upgrading; snapshot fields required; StrategyConfig required.
- **DEV_SEEDING.md:** Added "Snapshot Requirements (No Backward Compatibility)" — DB wipe, StrategyConfig, Schedule requirements.
- **RunSimulations.razor:** When batch returns with Status=Error, error message shown inline; user stays on form to fix and retry.

### Additional Changes (Step 3)

| File | Change |
|------|--------|
| `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor` | When `batch.Status == Error`, show `ErrorMessage` inline and do not navigate |
| `README.md` | Added "Upgrading / Database Wipe" section |
| `Docs/DEV_SEEDING.md` | Added "Snapshot Requirements (No Backward Compatibility)" section |

---

## Contributor Checklist: Adding Snapshot Fields

When adding new fields to snapshot DTOs or changing validation rules:

1. **Update `SnapshotValidator`** — Add validation for the new required field; throw `SnapshotValidationException` with a clear message.
2. **Update `EventSnapshotBuilder`** — Ensure the builder always populates the new field (no null/omit).
3. **Update `SnapshotValidatorTests`** — Add test for missing field (expects `SnapshotValidationException`); ensure `ValidSnapshot_Passes` still passes.
4. **Update integration tests** — Any test that builds snapshots manually must include the new field (e.g. `DistributedBatchIntegrationTests.BuildMinimalSnapshotJson`, `GroupPlayIntegrationTests`, etc.).
5. **Update docs** — Add to "Invariants Enforced" table in this document; update README/DEV_SEEDING if user-facing.
