# Perf E2E Zero Runs Investigation Plan

**Status:** Investigation plan  
**Date:** February 3, 2025  
**Context:** `--perf --runs 10000 --max-duration 120` completes 0 runs in 120 seconds; only `snapshot_load` phase logged.

---

## 1. Problem Summary

### Observed Behavior

```
Perf scenario: 10000 runs, event 'Winter Bingo 2025', seed 'perf-baseline-2025'
Max duration: 120s (will stop and report partial results if exceeded)
info: Simulation batch ... started: 10000 runs, seed perf-baseline-2025, mode Local
info: Executing run a061268f-... for batch ... (attempt 1)

=== Perf Summary ===
Runs completed: 0 / 10000
Elapsed: 120.0s
Throughput: 0.0 runs/sec
[TIMED OUT - max-duration reached]

Phase totals (ms total, count):
  snapshot_load: 4ms total, 1 invocations
```

### Key Observations

1. **First run started** — "Executing run" was logged (after snapshot load).
2. **No sim or persist phases** — Only `snapshot_load` recorded; `sim` and `persist` never logged.
3. **0 runs completed** — The first `runner.Execute(...)` call never returned within 120 seconds.
4. **Engine-only perf works** — `--perf-regression` and `dotnet test --filter "Category=Perf"` use `PerfScenarioSnapshot` and complete 10K runs at 50+ runs/sec.

### Conclusion

The hang occurs inside `SimulationRunner.Execute(snapshot, run.Seed, cancellationToken)` when using the **dev-seed snapshot** for "Winter Bingo 2025". The engine itself is fast with the synthetic `PerfScenarioSnapshot`.

---

## 2. Root Cause Hypotheses

### Hypothesis A: Dev Seed Snapshot Complexity (Most Likely)

The dev seed "Winter Bingo 2025" snapshot differs from `PerfScenarioSnapshot` in ways that may cause extreme slowness or an infinite loop:

| Aspect | PerfScenarioSnapshot | Dev Seed Winter Bingo 2025 |
|--------|----------------------|----------------------------|
| Player schedules | `Sessions = []` (always online) | Limited windows (e.g. Mon 18:00, Wed 19:00) |
| Activities | 1 simple activity | 6 activities (Zulrah, Vorkath, RC, Mining, CoX, ToA) |
| Attempt durations | 60±10 sec | 45–1800 sec (ToA: 1800±300) |
| Modifiers | None | Capability-based modifiers |
| Tiles | 4 tiles, 1 row | 8 tiles, 2 rows |
| Group scaling | None | GroupSizeBands |

**Possible failure modes:**

- **Schedule fast-forward loop** — When `eventQueue` is empty, the runner calls `GetEarliestNextSessionStart` and advances `simTime`. If `ScheduleEventsForPlayers` adds no events at that `simTime` (e.g. no players online, or no eligible activities), the loop may repeatedly fast-forward without making progress. If `GetNextSessionStart` has an edge case, this could loop indefinitely.
- **Event explosion** — Very short effective attempt durations combined with many players could produce an enormous number of events per run.
- **ScheduleEvaluator bug** — `GetNextSessionStart` or `GetEarliestNextSessionStart` could return incorrect values for certain `eventStartEt`/`simTime` combinations (e.g. timezone or day-of-week handling).

### Hypothesis B: Event Start Time Edge Case

The snapshot uses `batch.CreatedAt` as the event start. If this produces an unusual `EventStartTimeEt` (e.g. DST transition, week boundary), schedule logic could misbehave.

### Hypothesis C: Cancellation Token Not Honored

`runner.Execute` checks `cancellationToken.ThrowIfCancellationRequested()` only at the start of each main-loop iteration. If a single iteration is extremely long or never completes, cancellation would never be observed.

---

## 3. Investigation Plan

### Phase 1: Confirm Location of Hang (Low Effort)

**Goal:** Verify the hang is in `runner.Execute` and not in DB or snapshot loading.

**Actions:**

1. Add a log line immediately before `runner.Execute` in `SimulationRunExecutor` (e.g. "About to run simulation").
2. Add a log line immediately after `runner.Execute` returns.
3. Re-run perf scenario; confirm "About to run" appears and "simulation completed" does not.

**Expected:** Confirms hang is inside `runner.Execute`.

---

### Phase 2: Add Simulation Loop Instrumentation (Medium Effort)

**Goal:** Understand how far the simulation progresses before hanging.

**Actions:**

1. Add an optional `ISimulationProgressReporter` (or similar) that `SimulationRunner` can call:
   - At the start of each main-loop iteration: `(simTime, eventQueueCount, iterationIndex)`.
   - When fast-forwarding: `(prevSimTime, nextSimTime, reason)`.
2. Implement a `VerbosePerfRecorder` that logs every N iterations (e.g. every 1000) or when `simTime` advances by a large amount.
3. Add `--perf-verbose` flag to the perf scenario that enables this and runs a single run with a short timeout (e.g. 10s).
4. Run: `dotnet run --project BingoSim.Seed -- --perf --runs 1 --max-duration 10 --perf-verbose`

**Expected:** See whether the loop iterates at all, how many iterations occur, and whether `simTime` advances or stalls.

---

### Phase 3: Use Synthetic Snapshot for E2E (Quick Fix)

**Goal:** Unblock perf baseline measurement while investigating the dev-seed snapshot.

**Actions:**

1. Add `--perf-snapshot synthetic` (or `--use-synthetic-snapshot`) to the perf scenario.
2. When set, bypass the DB snapshot:
   - Use `PerfScenarioSnapshot.BuildJson()` to build the snapshot.
   - Still create the batch and runs in DB (for consistency), but inject the synthetic snapshot into the `EventSnapshot` for that batch, or
   - Skip batch creation and run the engine directly with synthetic snapshot + persist to a minimal batch (simpler but diverges from full E2E).
3. Prefer: Create batch and runs as today, but when loading the snapshot for execution, use the synthetic JSON if the flag is set. This keeps the persist path realistic.

**Expected:** `--perf --perf-snapshot synthetic --runs 10000 --max-duration 120` completes runs at engine-only–like throughput.

---

### Phase 4: Snapshot Comparison and Single-Run Debug (Medium Effort)

**Goal:** Compare dev-seed vs synthetic snapshot and run a single dev-seed run in isolation.

**Actions:**

1. Add `--perf-dump-snapshot` to write the loaded snapshot JSON to a file (e.g. `perf-snapshot.json`).
2. Add a small console app or test that:
   - Loads the dev-seed snapshot from DB (or from dumped file).
   - Runs a single `runner.Execute` with a 5-second timeout and `CancellationToken`.
   - Logs iteration count, `simTime` progression, and whether it completed or timed out.
3. Compare structure of dev-seed snapshot vs `PerfScenarioSnapshot` (schedules, activity count, etc.).

**Expected:** Identify structural differences and whether a single run completes or hangs.

---

### Phase 5: ScheduleEvaluator and Fast-Forward Logic Review (If Needed)

**Goal:** Rule out bugs in schedule handling.

**Actions:**

1. Add unit tests for `GetNextSessionStart` and `GetEarliestNextSessionStart` with:
   - Event start on various days (Mon–Sun).
   - Schedules that match or don’t match the event start day.
   - Empty schedules (always online).
2. Add a test that runs one simulation iteration with a dev-seed–like snapshot and asserts the fast-forward path behaves correctly.
3. Consider adding a max-iteration guard in the simulation loop (e.g. 1M iterations) that throws with diagnostic info to avoid infinite loops in production.

---

## 4. Recommended Implementation Order

| Step | Action | Effort | Impact |
|------|--------|--------|--------|
| 1 | Phase 3: Add `--perf-snapshot synthetic` | Low | Unblocks baseline measurement |
| 2 | Phase 1: Add pre/post logs around `runner.Execute` | Low | Confirms hang location |
| 3 | Phase 4: Add `--perf-dump-snapshot` and single-run debug | Medium | Enables snapshot comparison |
| 4 | Phase 2: Add simulation loop instrumentation | Medium | Pinpoints where it stalls |
| 5 | Phase 5: ScheduleEvaluator tests and loop guard | Medium | Prevents future hangs |

---

## 5. Immediate Workaround

Until the root cause is fixed, use one of:

1. **Engine-only perf** (no DB):
   ```bash
   dotnet run --project BingoSim.Seed -- --perf-regression --runs 1000
   dotnet test --filter "Category=Perf"
   ```

2. **E2E with synthetic snapshot** (after Phase 3):
   ```bash
   dotnet run --project BingoSim.Seed -- --perf --perf-snapshot synthetic --runs 10000 --max-duration 120
   ```

3. **Shorter event** (if supported): Add a "Perf Event" to the dev seed with `DurationSeconds = 3600` and minimal complexity to reduce run time.

---

## 6. Success Criteria

- [ ] Perf E2E completes at least 100 runs within 120 seconds with dev-seed "Winter Bingo 2025", or root cause is identified and documented.
- [ ] `--perf-snapshot synthetic` provides a reliable E2E baseline.
- [ ] Simulation loop has instrumentation (or a guard) to detect and diagnose infinite or extremely slow runs.
