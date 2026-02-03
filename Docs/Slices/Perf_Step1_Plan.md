# Performance Refactor — Step 1: Measure + Identify Bottlenecks (PLAN ONLY)

**Status:** Plan only — no code changes yet  
**Date:** February 3, 2025  
**Goal:** Establish a repeatable baseline, identify top bottlenecks, and propose high-impact refactors to achieve single-digit millions of runs in &lt; 10 minutes on 4+ workers.

---

## Context

- **Simulation engine:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` — runs in both local (Web hosted service) and distributed (Worker via MassTransit) modes.
- **Target:** Single-digit millions of runs in &lt; 10 minutes on at least 4 workers.
- **Constraints:** No backward compatibility; DB often wiped. Performance matters most in the simulation hot path; DB persistence should be batched/optimized.
- **Invariants:** Simulation semantics unchanged; determinism (seed reproducibility) intact; local and distributed execution intact.

---

## 1) Baseline Measurement Plan

### A) Perf Scenario Definition

Use **DEV_SEEDING** data as the standard scenario:

| Component | Value |
|-----------|-------|
| Event | "Winter Bingo 2025" (first seed event) |
| Teams | 2 (Team Alpha, Team Beta) |
| Players | 8 (Alice, Bob, Carol, Dave, Eve, Frank, Grace, Henry) |
| Activities | 6 (Zulrah, Vorkath, Runecraft, Mining, CoX, ToA) |
| Rows/Tiles | 3+ rows, 4 tiles per row |
| Schedules | Per seed definitions |
| Modifiers | Per tile rules |

**Determinism:** Use fixed seed `perf-baseline-2025` so runs are reproducible.

### B) Exact Command / Test to Run

**Option 1 (Preferred):** Add a dedicated **CLI perf command** in a new project or extend `BingoSim.Seed`:

```bash
# Prerequisites
docker compose up -d postgres
dotnet run --project BingoSim.Seed -- --full-reset --confirm   # Clean DB
dotnet run --project BingoSim.Seed                              # Seed DEV data

# Perf scenario (10,000 runs, local, single worker)
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --seed "perf-baseline-2025"

# With timeout: stop after 2 minutes, return partial data (avoids hanging on extremely bad performance)
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --seed "perf-baseline-2025" --max-duration 120
```

**Timeout / Max Duration:** Add `--max-duration <seconds>` (or `--timeout`) as an optional parameter. When specified:

- Start a `CancellationTokenSource` with the given timeout.
- Pass the cancellation token into the run loop (executor, queue consumer, etc.).
- When the timeout fires: stop accepting new runs, allow in-flight runs to complete or cancel them gracefully, then **return partial perf data**.
- Output must clearly indicate whether the run **completed normally** or **timed out**, and report: `runs completed`, `elapsed seconds`, `runs/sec` (based on partial count). Example: `Perf summary: 847 runs in 120.0s (7.1 runs/sec) [TIMED OUT - max-duration reached]`.

This guards against extremely bad performance (e.g., 10K runs taking hours) while still yielding useful throughput data from the partial run.

**Option 2:** Add an **xUnit perf test** excluded from normal CI:

- New test class: `SimulationPerfScenarioTests.cs` in `Tests/BingoSim.Application.UnitTests` or a dedicated `Tests/BingoSim.PerfTests` project.
- Use `[Trait("Category", "Perf")]` and exclude via `dotnet test --filter "Category!=Perf"` in CI.
- Test: Resolve event by name → build snapshot → run 10,000 simulations in a tight loop (no DB, no queue) → record elapsed time and runs/sec.
- Requires in-memory snapshot JSON (from seed or fixture) to avoid DB dependency for pure simulation throughput.
- **Timeout:** Support an optional max duration (e.g., via `[PerfTimeout(seconds: 300)]` or config). If exceeded, fail the test with partial metrics (runs completed, elapsed, runs/sec) so CI or local runs don't hang.

**Recommended:** Implement **both**:
- **CLI perf command** for end-to-end measurement (DB + snapshot build + execution + persistence).
- **Unit perf test** for simulation-only throughput (no DB, no queue) to isolate engine cost.

### C) Measurement Outputs

| Metric | How to Capture |
|-------|-----------------|
| Elapsed time | `Stopwatch` around the run block (full or partial) |
| Runs completed | Actual count when stopped (may be less than requested if timed out) |
| Runs/sec | `runsCompleted / elapsedSeconds` |
| Timed out? | Boolean: whether `--max-duration` was reached before completing requested runs |
| Allocations | Optional: `dotnet-gcdump` or `BenchmarkDotNet` with `MemoryDiagnoser` if available |
| Per-phase timing | See §1.D below |

When a timeout occurs, all metrics are based on the partial run. The output must explicitly indicate `[TIMED OUT]` so consumers know the run did not complete the full requested count.

### D) Minimal Instrumentation (Timing Wrapper)

Add a **timing wrapper** around these phases (no heavy metrics yet):

| Phase | Location | What to Measure |
|-------|----------|-----------------|
| Snapshot load/parse | `SimulationRunExecutor.ExecuteAsync` | Time from `snapshotRepo.GetByBatchIdAsync` return to `runner.Execute` start (includes JSON parse inside `runner.Execute`) |
| Per-run simulation | `SimulationRunner.Execute` | Time inside `Execute` (excluding JSON parse if measured separately) |
| Persistence | `SimulationRunExecutor.ExecuteAsync` | Time for `DeleteByRunIdAsync` + `AddRangeAsync` + `UpdateAsync` |
| Aggregate computation | `BatchFinalizationService.TryFinalizeAsync` | Time to compute and persist aggregates |

**Implementation:** Use `Stopwatch` and log a **summary once per batch** (or per N runs in a loop). If `System.Diagnostics.Metrics` is added later, keep the Stopwatch approach for the perf scenario to avoid coupling.

**Suggested logging format:**
```
Perf summary: 10000 runs in 45.2s (221 runs/sec). Snapshot parse: 2.1ms/run avg. Sim: 1.8ms/run avg. Persist: 0.5ms/run avg.
```

**With timeout (partial run):**
```
Perf summary: 847 runs in 120.0s (7.1 runs/sec) [TIMED OUT - max-duration 120s reached]. Snapshot parse: 2.1ms/run avg. Sim: 135ms/run avg. Persist: 0.5ms/run avg.
```

---

## 2) Bottleneck Hypotheses (Code Inspection)

### 2.1 JSON Parsing Inside Loop (High Impact)

- **Location:** `SimulationRunner.Execute` line 26: `EventSnapshotBuilder.Deserialize(snapshotJson)`.
- **Behavior:** Every run parses the **same** snapshot JSON. For 10K runs, that's 10K parses of ~50–150KB JSON.
- **Evidence:** `System.Text.Json.JsonSerializer.Deserialize<EventSnapshotDto>` is called per run in `SimulationRunExecutor` → `runner.Execute(snapshot.EventConfigJson, ...)`.

### 2.2 ScheduleEvaluator.DailyWindows.Build — Per-Call Allocation (High Impact)

- **Location:** `ScheduleEvaluator.IsOnlineAt`, `GetCurrentSessionEnd`, `GetNextSessionStart` — each calls `DailyWindows.Build(schedule)`.
- **Behavior:** `DailyWindows.Build` allocates a new `DailyWindows` and populates `_intervals` **every time** it's called. No caching.
- **Hot path:** `ScheduleEventsForPlayers` calls `IsOnlineAt` for each player (8 players × 2 teams), and `GetCurrentSessionEnd` for each player in each group. With hundreds of loop iterations per run, this is thousands of allocations per run.

### 2.3 RowUnlockHelper.ComputeUnlockedRows — Recomputed on Every Access (High Impact)

- **Location:** `TeamRunState.UnlockedRowIndices` property → `RowUnlockHelper.ComputeUnlockedRows(...)`.
- **Behavior:** Returns a **new** `HashSet<int>` on every property access.
- **Hot path:** `GetFirstEligibleActivity`, `GetEligibleTileKeys`, and `AddProgress` all access `state.UnlockedRowIndices`. Called in the main simulation loop many times per run.

### 2.4 Per-Attempt Allocations (Medium Impact)

| Location | Allocation | Frequency |
|---------|------------|-----------|
| `GetUnionCapabilityKeys` | `new HashSet<string>` | Per PerGroup roll (multiple per event) |
| `ModifierApplicator.ApplyProbabilityMultiplier` | `outcomes.Select(...).ToList()` when no scaling | Per outcome roll |
| `ScheduleEventsForPlayers` | `scheduleFiltered.ToList()`, `sortedPlayers.ToList()`, `assignments`, `used`, `sameWork.ToList()`, `group.ToList()` | Per schedule cycle |
| `GetEligibleTileKeys` | `new List<string>` | Per grant applied |
| `CollectGrantsFromAttempts` | `grantsBuffer` reused (good) | — |

### 2.5 DB Roundtrips Per Run (Medium Impact)

- **Per run:** `GetByIdAsync` (run), `TryClaimAsync`, `GetByBatchIdAsync` (snapshot), `DeleteByRunIdAsync`, `AddRangeAsync` (TeamRunResults), `UpdateAsync` (run).
- **SaveChanges:** `DeleteByRunIdAsync` and `AddRangeAsync` each call `SaveChangesAsync`; `UpdateAsync` also. So **3 SaveChanges per run**.
- **Batching opportunity:** For local mode, could batch N runs before persisting (higher risk; requires design).

### 2.6 Logging Inside Loop (Low Impact if Debug Disabled)

- **Location:** `SimulationRunner` line 97: `logger?.LogDebug("Schedule fast-forward: ...")`.
- **Behavior:** Only when LogLevel is Debug. Default is Information, so usually not a factor. Keep as-is unless profiling shows otherwise.

### 2.7 JSON Serialization of Results (Low–Medium Impact)

- **Location:** `SimulationRunner.Execute` lines 163–164: `JsonSerializer.Serialize(state.RowUnlockTimes)`, `JsonSerializer.Serialize(state.TileCompletionTimes)` per team.
- **Behavior:** 2 teams × 2 serializations = 4 JSON serializations per run. Smaller payloads than snapshot; lower impact than parse.

---

## 3) Concrete Refactor Proposals

### Proposal 1: Cache Parsed Snapshot Per Batch (Eliminate Redundant JSON Parse)

| Attribute | Value |
|-----------|-------|
| **Expected impact** | High |
| **Risk** | Low |
| **Files/classes** | `SimulationRunExecutor`, `ISimulationRunExecutor`, possibly `SimulationRunner` |
| **Description** | Each run in a batch uses the **same** snapshot JSON. Parse once per batch (or per worker batch window) and pass `EventSnapshotDto` to `SimulationRunner.Execute` instead of `string snapshotJson`. Add overload: `Execute(EventSnapshotDto snapshot, string runSeedString, ...)`. Executor: parse once when processing first run of a batch; reuse for subsequent runs. In distributed mode, workers receive run IDs; snapshot is loaded per run. **Optimization:** Pass snapshot DTO from a cache keyed by `batchId` so that within a worker, multiple runs for the same batch reuse the parsed DTO. |
| **Determinism** | Unchanged — same DTO, same seed → same result. |

### Proposal 2: Cache DailyWindows Per Player (Eliminate Schedule Rebuild)

| Attribute | Value |
|-----------|-------|
| **Expected impact** | High |
| **Risk** | Low |
| **Files/classes** | `ScheduleEvaluator`, `SimulationRunner` |
| **Description** | Build `DailyWindows` once per player at simulation start and pass a `Dictionary<playerIndex, DailyWindows>` (or similar) into schedule checks. Change `IsOnlineAt(WeeklyScheduleSnapshotDto schedule, ...)` to `IsOnlineAt(DailyWindows windows, ...)` and `GetCurrentSessionEnd` similarly. `SimulationRunner` builds the cache in `BuildPlayerCapabilitySets`-style setup (or a new `BuildScheduleWindows`). |
| **Determinism** | Unchanged — same schedule → same windows. |

### Proposal 3: Cache UnlockedRowIndices in TeamRunState (Eliminate Recomputation)

| Attribute | Value |
|-----------|-------|
| **Expected impact** | High |
| **Risk** | Low |
| **Files/classes** | `TeamRunState`, `RowUnlockHelper` |
| **Description** | Store `_unlockedRowIndices` as a mutable `HashSet<int>` in `TeamRunState`. Recompute only when `AddProgress` completes a tile (i.e., when `_completedPointsByRow` changes). Invalidate and recompute in `AddProgress` after updating `_completedPointsByRow`. Expose as `IReadOnlySet<int>` to avoid external mutation. |
| **Determinism** | Unchanged — same progression → same unlock set. |

### Proposal 4: Reduce Allocations in ModifierApplicator and Hot Paths

| Attribute | Value |
|-----------|-------|
| **Expected impact** | Medium |
| **Risk** | Low |
| **Files/classes** | `ModifierApplicator`, `SimulationRunner` (ScheduleEventsForPlayers, GetUnionCapabilityKeys) |
| **Description** | (a) `ApplyProbabilityMultiplier`: When no scaling needed, return `outcomes` directly (or a lightweight wrapper) instead of `outcomes.Select(o => o.WeightNumerator).ToList()`. (b) `GetUnionCapabilityKeys`: Accept a `HashSet<string>` buffer to fill instead of allocating. (c) Reuse lists in `ScheduleEventsForPlayers` where possible (e.g., pool or instance-level buffers). |
| **Determinism** | Unchanged. |

### Proposal 5: Batch DB Writes for Local Mode

| Attribute | Value |
|-----------|-------|
| **Expected impact** | Medium (if DB is bottleneck) |
| **Risk** | Medium |
| **Files/classes** | `SimulationRunExecutor`, `ITeamRunResultRepository`, `SimulationRunQueueHostedService`, batch finalization |
| **Description** | For local mode, instead of persisting after each run, accumulate results in memory and flush every N runs (e.g., 100) or on a timer. Requires: (a) deferring run status updates, (b) batching `AddRangeAsync` for TeamRunResults, (c) batching run status updates. Increases memory and complexity; may complicate failure handling. Measure first to confirm DB is a bottleneck. |
| **Determinism** | Unchanged — persistence order does not affect simulation. |

### Proposal 6: Snapshot Caching in Distributed Workers

| Attribute | Value |
|-----------|-------|
| **Expected impact** | Medium (distributed) |
| **Risk** | Low |
| **Files/classes** | `SimulationRunExecutor`, `ExecuteSimulationRunConsumer` |
| **Description** | When a worker processes multiple runs for the same batch, cache the parsed snapshot by `batchId`. First run: load snapshot, parse, cache. Subsequent runs: use cached DTO. Evict when batch is complete or after a TTL. Reduces both DB reads and JSON parse for same-batch runs. |
| **Determinism** | Unchanged. |

---

## 4) Order of Execution

| Order | Proposal | Rationale |
|-------|----------|------------|
| 1 | **Baseline measurement** | Establish current runs/sec before any changes. |
| 2 | **Proposal 3** (Cache UnlockedRowIndices) | Low risk, high impact, localized change. |
| 3 | **Proposal 2** (Cache DailyWindows) | Low risk, high impact, clear boundary. |
| 4 | **Proposal 1** (Cache parsed snapshot) | High impact; requires executor/runner interface change. |
| 5 | **Proposal 4** (Reduce allocations) | Medium impact; do after top 3 to avoid diluting measurement. |
| 6 | **Proposal 6** (Snapshot cache in workers) | For distributed; measure first to confirm benefit. |
| 7 | **Proposal 5** (Batch DB writes) | Higher risk; only if DB is confirmed bottleneck. |

---

## 5) Definition of Done

### Throughput Targets

| Scenario | Current (TBD) | Target |
|----------|---------------|--------|
| 10K runs, local, 1 worker | Measure | ≥ 200 runs/sec (50s for 10K) |
| 1M runs, distributed, 4 workers | Measure | &lt; 10 minutes (≥ 1,667 runs/sec aggregate) |

### Verification

- [ ] Perf scenario runs deterministically: same seed produces identical results before and after refactors.
- [ ] Local and distributed execution both work.
- [ ] No semantic changes to simulation logic.
- [ ] All existing tests pass.
- [ ] Perf scenario (CLI or test) documents baseline and post-refactor runs/sec.

---

## 6) Measurement Setup Checklist (To Implement in Step 2)

- [ ] Add `--perf` (or similar) to `BingoSim.Seed` or new `BingoSim.Perf` project.
- [ ] Perf command: seed DB, resolve "Winter Bingo 2025", start batch with 10K runs, local mode, fixed seed, run to completion, print elapsed + runs/sec.
- [ ] Add `--max-duration <seconds>` (optional): when specified, cancel after that many seconds and return partial perf data (runs completed, elapsed, runs/sec, `[TIMED OUT]` indicator).
- [ ] Wire `CancellationToken` from the timeout through the run loop so runs can be cancelled gracefully.
- [ ] Add xUnit perf test (excluded from CI) for simulation-only throughput, with optional timeout to avoid hanging.
- [ ] Add Stopwatch timing in `SimulationRunExecutor` for: snapshot load+parse, sim execution, persistence.
- [ ] Log summary once per batch (or per N runs in perf loop); when timed out, use partial counts for runs/sec.
- [ ] Document baseline in this file or `Docs/Perf_Baseline.md` after first run.

---

## 7) Summary

| Bottleneck | Impact | Refactor |
|------------|--------|----------|
| JSON parse per run | High | Cache parsed snapshot per batch/worker |
| DailyWindows.Build per call | High | Cache per player at sim start |
| UnlockedRowIndices recomputed | High | Cache in TeamRunState, invalidate on progress |
| Per-attempt allocations | Medium | Buffer reuse, avoid unnecessary ToList |
| DB roundtrips per run | Medium | Batch writes (if measured as bottleneck) |
| Snapshot load in workers | Medium | Cache by batchId in worker |

**Next step:** Implement baseline measurement (CLI + test + instrumentation), run once, record baseline, then proceed with Proposal 3 → 2 → 1 in that order.

**Timeout safeguard:** The `--max-duration` option ensures that even with extremely bad performance, the perf command returns useful partial data instead of hanging indefinitely.
