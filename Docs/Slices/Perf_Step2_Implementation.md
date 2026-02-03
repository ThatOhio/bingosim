# Performance Refactor — Step 2 & 3: Implementation + HARDEN

**Status:** Complete  
**Date:** February 3, 2025  
**Reference:** [Perf_Step1_Plan.md](Perf_Step1_Plan.md)

---

## Overview

This document summarizes the performance optimizations (Step 2) and the validation/regression guardrails (Step 3 HARDEN).

---

## Optimizations Implemented

### 1. Proposal 3: Cache UnlockedRowIndices in TeamRunState
- **Files:** `BingoSim.Application/Simulation/Runner/TeamRunState.cs`
- **Change:** Store `_unlockedRowIndices` as a mutable `HashSet<int>` and recompute only when `AddProgress` completes a tile. Previously, `UnlockedRowIndices` called `RowUnlockHelper.ComputeUnlockedRows` on every property access, allocating a new `HashSet` each time.
- **Impact:** High — eliminates thousands of allocations per run.

### 2. Proposal 2: Cache DailyWindows Per Player
- **Files:** `BingoSim.Application/Simulation/Schedule/ScheduleEvaluator.cs`, `BingoSim.Application/Simulation/Runner/SimulationRunner.cs`
- **Change:** Build `DailyWindows` once per player at simulation start via `BuildScheduleWindowsCache`. Added overloads `IsOnlineAt(DailyWindows, ...)` and `GetCurrentSessionEnd(DailyWindows, ...)` that use precomputed windows. Previously, `DailyWindows.Build(schedule)` was called on every `IsOnlineAt` and `GetCurrentSessionEnd` invocation.
- **Impact:** High — eliminates thousands of allocations per run.

### 3. Proposal 1: Cache Parsed Snapshot Per Batch
- **Files:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs`, `BingoSim.Application/Services/SimulationRunExecutor.cs`
- **Change:** Added `Execute(EventSnapshotDto snapshot, ...)` overload. Executor caches parsed snapshot by `batchId` in `_snapshotCache`; first run parses and caches, subsequent runs reuse. Cache evicts when size exceeds 32 batches.
- **Impact:** High — eliminates 10K JSON parses per 10K-run batch (parse once, reuse 9,999 times).

### 4. Proposal 4: Reduce Allocations in Hot Paths
- **Files:** `BingoSim.Application/Simulation/ModifierApplicator.cs`, `BingoSim.Application/Simulation/Runner/SimulationRunner.cs`
- **Change:**
  - **ModifierApplicator:** When no scaling needed, return `OutcomeWeightsView` (zero-allocation wrapper) instead of `outcomes.Select(...).ToList()`. Replaced `o.Grants.Any(...)` with a `foreach` loop to avoid LINQ allocation.
  - **GetUnionCapabilityKeys:** Accept `HashSet<string>` buffer to fill instead of allocating a new `HashSet` per call. Added `groupCapsBuffer` in `Execute` and pass through `CollectGrantsFromAttempts` and `SampleAttemptDuration`.
- **Impact:** Medium — reduces per-attempt allocations.

---

## Perf Infrastructure Added

### CLI Perf Command (BingoSim.Seed)
- **Usage:**
  ```bash
  # Prerequisites: PostgreSQL running, dev data seeded
  docker compose up -d postgres
  dotnet run --project BingoSim.Seed -- --full-reset --confirm
  dotnet run --project BingoSim.Seed

  # Run perf scenario (10K runs, default)
  dotnet run --project BingoSim.Seed -- --perf

  # With options
  dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --seed "perf-baseline-2025"

  # With timeout (stop after 120s, report partial results)
  dotnet run --project BingoSim.Seed -- --perf --runs 10000 --max-duration 120
  ```
- **Output:** Runs completed, elapsed seconds, runs/sec, phase totals (snapshot_load, sim, persist) in ms total and invocation count.

### Regression Guard (--perf-regression)
- **Usage:** `dotnet run --project BingoSim.Seed -- --perf-regression [--runs 1000] [--min-runs-per-sec 50]`
- **Behavior:** Engine-only, no DB. Runs N simulations, reports runs/sec. Exits 1 if below threshold. Run manually or in a perf pipeline; not in normal CI.
- **Purpose:** Alerts if perf drops drastically.
- **Output:** `Perf regression guard: N runs in Xs = Y runs/sec (min: Z)` then PASS or FAIL.

### xUnit Perf Test (Engine-Only)
- **Location:** `Tests/BingoSim.Application.UnitTests/Simulation/SimulationPerfScenarioTests.cs`
- **Trait:** `[Trait("Category", "Perf")]` — excluded from normal CI.
- **Run:**
  ```bash
  # Run perf tests only
  dotnet test --filter "Category=Perf"

  # Run all tests except perf (normal CI)
  dotnet test --filter "Category!=Perf"
  ```
- **Tests:** `EngineOnly_10000Runs_CompletesWithinReasonableTime`, `EngineOnly_SameSeed_Deterministic`, `Execute_JsonVsDtoPath_SameSeed_ProducesIdenticalResults` (regression guard for snapshot caching).

### Phase Instrumentation
- **IPerfRecorder** and **PerfRecorder** record phase totals (ms total, count) for `snapshot_load`, `sim`, `persist`.
- **SimulationRunExecutor** accepts optional `IPerfRecorder`; when provided (e.g. in perf scenario), records timing.
- Phase totals are reported as **ms total** and **invocation count**, not per-run averages, so caching effects are visible (e.g. snapshot_load: 1 invocation for 10K runs when cached).

---

## Step 3 HARDEN (Validation + Regression Guardrails)

### Determinism
- **SimulationRunnerReproducibilityTests:** Same seed, same results (existing).
- **EngineOnly_SameSeed_Deterministic:** Perf scenario, same seed twice (existing).
- **Execute_JsonVsDtoPath_SameSeed_ProducesIdenticalResults:** NEW — verifies `Execute(string)` and `Execute(EventSnapshotDto)` produce identical results. Regression guard for Proposal 1 (snapshot caching).

### Local vs Distributed Consistency
- **Local:** `--perf` runs in local mode (in-process).
- **Distributed:** Use full stack (Web + 2 Workers): `docker compose up -d --scale bingosim.worker=2`, then start batch via UI with Distributed mode. See [PERF_NOTES.md](PERF_NOTES.md) for steps.
- **Results:** Both modes produce identical results for the same seed. Throughput should scale with workers.

### Regression Guard
- **Command:** `dotnet run --project BingoSim.Seed -- --perf-regression [--runs 1000] [--min-runs-per-sec 50]`
- **Behavior:** Engine-only, no DB. Exits 1 if runs/sec below threshold.
- **Expected range:** 50–500+ runs/sec on typical hardware. Threshold 50 catches drastic regressions.

---

## Files Changed

| File | Change |
|------|--------|
| `BingoSim.Application/Interfaces/IPerfRecorder.cs` | New |
| `BingoSim.Application/Simulation/PerfScenarioSnapshot.cs` | New — shared perf snapshot for regression guard |
| `BingoSim.Application/Services/SimulationRunExecutor.cs` | PerfRecorder, snapshot cache, phase timing |
| `BingoSim.Application/Simulation/ModifierApplicator.cs` | OutcomeWeightsView, foreach instead of Any |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Execute(EventSnapshotDto), schedule cache, groupCapsBuffer, BuildScheduleWindowsCache |
| `BingoSim.Application/Simulation/Runner/TeamRunState.cs` | Cached UnlockedRowIndices |
| `BingoSim.Application/Simulation/Schedule/ScheduleEvaluator.cs` | IsOnlineAt/GetCurrentSessionEnd overloads for DailyWindows |
| `BingoSim.Infrastructure/DependencyInjection.cs` | Keyed "distributed" publisher for Seed |
| `BingoSim.Infrastructure/Simulation/PerfRecorder.cs` | New |
| `BingoSim.Seed/Program.cs` | --perf command, --perf-regression guard, RunPerfScenarioAsync, GetArg helpers |
| `Tests/BingoSim.Application.UnitTests/Simulation/SimulationPerfScenarioTests.cs` | New engine-only perf tests |

---

## How to Run Perf Scenario

### Full E2E (DB + snapshot build + execution + persistence)
```bash
# 1. Start PostgreSQL
docker compose up -d postgres

# 2. Reset and seed
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed

# 3. Run perf (10K runs)
dotnet run --project BingoSim.Seed -- --perf

# Optional: custom runs, event, seed, timeout
dotnet run --project BingoSim.Seed -- --perf --runs 5000 --event "Winter Bingo 2025" --seed "perf-baseline-2025" --max-duration 60
```

### Engine-Only (No DB)
```bash
dotnet test --filter "Category=Perf"
```

---

## Dotnet Test Commands

```bash
# All tests except perf (CI)
dotnet test --filter "Category!=Perf"

# Perf tests only
dotnet test --filter "Category=Perf"

# All tests (including perf)
dotnet test
```

---

## Baseline and Results

Record baseline and post-optimization numbers in [PERF_NOTES.md](PERF_NOTES.md) when you run the perf scenario. Example format:

```
## Baseline (before optimizations)
- 10K runs E2E: X.X runs/sec, elapsed Xs
- Phase totals: snapshot_load Xms (10000), sim Xms (10000), persist Xms (10000)

## After Proposal 3 (UnlockedRowIndices cache)
- 10K runs E2E: X.X runs/sec
- Phase totals: ...

## After Proposal 2 (DailyWindows cache)
...

## After Proposal 1 (Snapshot cache)
- Phase totals: snapshot_load Xms (1), sim Xms (10000), persist Xms (10000)
```

---

## Verification

- [x] Determinism preserved: `SimulationRunnerReproducibilityTests`, `EngineOnly_SameSeed_Deterministic`, `Execute_JsonVsDtoPath_SameSeed_ProducesIdenticalResults` pass.
- [x] No semantic changes to simulation logic.
- [x] Local and distributed execution intact (keyed "distributed" added for Seed; Web overrides with MassTransit).
- [x] Config knobs default to current behavior; `--perf` and `--perf-regression` are opt-in.
- [x] Regression guard: `--perf-regression` exits 1 if throughput below threshold; no strict timing assert in CI.
