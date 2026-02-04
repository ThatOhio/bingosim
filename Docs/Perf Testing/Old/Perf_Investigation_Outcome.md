# Perf Investigation Outcome

**Date:** February 3, 2025  
**Status:** Complete — root cause fixed, unblockers implemented

---

## Summary

The perf E2E scenario was hanging (0 runs in 120s) due to an **infinite loop in the schedule fast-forward path** when `EtToSimTime` truncation caused `nextSimTime` to equal `prevSimTime`. The implementation added diagnostic harnesses, a no-progress guard, and synthetic snapshot mode. During implementation, the **root cause was identified and fixed** (advance by 1 when truncated), so both devseed and synthetic perf now complete successfully.

---

## Root Cause

**Location:** `SimulationRunner.Execute` — empty-queue fast-forward block (lines ~99–170)

**Cause:** When the next session start (`nextEt`) is less than 1 second after the current `simTimeEt`, `EtToSimTime(eventStartEt, nextEt)` truncates to the same integer as `simTime`. The loop then sets `simTime = nextSimTime` (unchanged), schedules (adds nothing because no players are online at that exact moment), and repeats. This produced an infinite loop.

**Example from diagnostics:**
```
simTimeEt=2026-02-03T16:59:59.9777834-05:00
nextSimTimeEt=2026-02-03T17:00:00.0000000-05:00
nextSimTime (16267) repeated 10 times
onlinePlayers=0
```

**Fix:** When `nextSimTime == prevSimTime`, advance by 1 second: `nextSimTime = prevSimTime + 1`. This breaks the truncation-induced loop while preserving simulation semantics (we skip at most 1 second of simulated time).

---

## Implementation Summary

### Phase 0: 1-Run Reproducible Debug Path
- Added `LogWarning` before and after `SimulationRunner.Execute` in `SimulationRunExecutor`
- Confirms hang location; "after" log now appears (hang resolved)

### Phase 1: No-Progress Guard + Cancellation
- **SimulationNoProgressException** with diagnostics (simTime, nextSimTime, simTimeEt, nextSimTimeEt, onlinePlayersCount)
- Guard 1: Throw when `nextSimTime < prevSimTime` (schedule bug)
- Guard 2: Throw when same `nextSimTime` repeats ≥10 times (infinite loop)
- Truncation fix: When `nextSimTime == prevSimTime`, set `nextSimTime = prevSimTime + 1`
- Cancellation checks in `ScheduleEventsForPlayers`, `CollectGrantsFromAttempts`, and grant allocation loop

### Phase 2: --perf-verbose
- Logs every 1000 iterations: `iter`, `simTime`, `nextSimTime`, `queue`, `online`
- Off by default

### Phase 3: --perf-snapshot synthetic
- Uses `PerfScenarioSnapshot.BuildJson()` for execution instead of DB snapshot
- Full E2E persist path unchanged
- Documented in PERF_NOTES.md

### Phase 4: --perf-dump-snapshot
- Writes loaded snapshot JSON to file (default `perf-snapshot.json`)
- Supports `{0}` placeholder for batchId

### Tests
- `SimulationNoProgressGuardTests`: Exception properties, Execute completes, cancellation honored
- `ScheduleEvaluatorTests`: `GetNextSessionStart` at session start, `GetEarliestNextSessionStart` with empty schedules

---

## Files Changed

| File | Change |
|------|--------|
| `BingoSim.Application/Services/SimulationRunExecutor.cs` | Before/after logs, IPerfScenarioOptions, synthetic/dump/verbose support |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | No-progress guard, truncation fix, cancellation checks, progress reporter |
| `BingoSim.Application/Simulation/SimulationNoProgressException.cs` | New |
| `BingoSim.Application/Simulation/ISimulationProgressReporter.cs` | New |
| `BingoSim.Application/Simulation/VerboseProgressReporter.cs` | New |
| `BingoSim.Application/Interfaces/IPerfScenarioOptions.cs` | New |
| `BingoSim.Infrastructure/Simulation/PerfScenarioOptions.cs` | New |
| `BingoSim.Infrastructure/DependencyInjection.cs` | Default IPerfScenarioOptions registration |
| `BingoSim.Seed/Program.cs` | Parse --perf-snapshot, --perf-verbose, --perf-dump-snapshot; register options; catch SimulationNoProgressException |
| `Tests/.../SimulationNoProgressGuardTests.cs` | New |
| `Tests/.../ScheduleEvaluatorTests.cs` | GetNextSessionStart at session start, GetEarliestNextSessionStart empty |
| `Docs/PERF_NOTES.md` | New options and synthetic mode docs |

---

## Commands to Run

### 1-Run Reproducible Debug
```bash
dotnet run --project BingoSim.Seed -- --perf --runs 1 --max-duration 10 --event "Winter Bingo 2025"
```
**Expected:** Completes in <1s. Logs "about to call SimulationRunner.Execute" and "SimulationRunner.Execute returned".

### Devseed Perf (Fixed)
```bash
dotnet run --project BingoSim.Seed -- --perf --runs 100 --event "Winter Bingo 2025" --max-duration 120
```
**Expected:** Completes 100 runs in ~3s at ~33 runs/sec.

### Synthetic Perf (Unblocker)
```bash
dotnet run --project BingoSim.Seed -- --perf --runs 1000 --perf-snapshot synthetic --max-duration 60
```
**Expected:** Completes 1000 runs in ~25s at ~40 runs/sec.

### With Verbose Progress
```bash
dotnet run --project BingoSim.Seed -- --perf --runs 10 --perf-verbose
```

### Dump Snapshot
```bash
dotnet run --project BingoSim.Seed -- --perf --runs 1 --perf-dump-snapshot
# Writes to perf-snapshot.json
```

---

## Where the Stall Was Happening

The stall occurred in the **schedule fast-forward block** of `SimulationRunner.Execute` when:
1. `eventQueue` was empty
2. `GetEarliestNextSessionStart` returned a session start <1 second after current `simTimeEt`
3. `EtToSimTime` truncated to the same value as `simTime`
4. `simTime` did not advance, so the loop repeated indefinitely

The diagnostics showed `onlinePlayers=0` at the stall point (near the end of the 24-hour event), consistent with no players online at that moment and no events being scheduled.
