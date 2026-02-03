# Performance Optimization — Round 2 Implementation

**Status:** Complete  
**Date:** February 3, 2025  
**Reference:** [Perf_Round2_Optimization_Plan.md](Perf_Round2_Optimization_Plan.md)

---

## Summary

All five optimizations from the Round 2 plan were implemented to reduce the ~92-second gap between measured phase totals and total elapsed time. The changes target per-run DB round-trips, batch finalization overhead, and EF Core change tracker bloat.

---

## Changes Made

### 1. Snapshot Load Once Per Batch

**File:** `BingoSim.Application/Services/SimulationRunExecutor.cs`

- Reordered logic: check `_snapshotCache` **before** calling `GetByBatchIdAsync`.
- On cache hit: use cached DTO, skip DB call entirely.
- On cache miss: fetch from DB, parse, validate, cache, then use.
- **Effect:** Reduces snapshot DB round-trips from 10,000 to 1 per batch.

### 2. Lightweight Finalization Check

**Files:**
- `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` — Added `HasNonTerminalRunsAsync(batchId)`
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` — Implemented via `AnyAsync` with `Status IN ('Pending','Running')`, using `AsNoTracking`
- `BingoSim.Infrastructure/Services/BatchFinalizationService.cs` — Call `HasNonTerminalRunsAsync` first; only load full runs when all are terminal

**Effect:** Avoids ~190 full loads of 10,000 runs; uses a lightweight existence check instead.

### 3. AsNoTracking for Read-Only Queries

**Files:**
- `SimulationRunRepository.cs` — `GetByIdAsync`, `GetByBatchIdAsync`, `HasNonTerminalRunsAsync`
- `EventSnapshotRepository.cs` — `GetByBatchIdAsync`
- `SimulationBatchRepository.cs` — `GetByIdAsync`
- `TeamRunResultRepository.cs` — `GetByRunIdAsync`, `GetByBatchIdAsync` (both queries)

**Effect:** Reduces change tracker bloat and memory/CPU overhead over 10,000 runs.

### 4. Remove Debug LogWarning

**File:** `BingoSim.Application/Services/SimulationRunExecutor.cs`

- Removed `LogWarning` before and after `SimulationRunner.Execute` (added during hang investigation).

### 5. Pass Run Entity in Local Perf Path

**Files:**
- `BingoSim.Application/Interfaces/ISimulationRunExecutor.cs` — Added `ExecuteAsync(SimulationRun run, EventSnapshotDto snapshot, ct)`
- `BingoSim.Application/Services/SimulationRunExecutor.cs` — Implemented overload; skips `GetByIdAsync`, `TryClaimAsync`, and snapshot load; calls shared `ExecuteWithSnapshotAsync`
- `BingoSim.Seed/Program.cs` — Load snapshot once before loop (synthetic or devseed); call `ExecuteAsync(runs[i], snapshot, ct)` instead of `ExecuteAsync(runs[i].Id, ct)`

**Effect:** Eliminates 10,000 `GetByIdAsync` and 10,000 `TryClaimAsync` round-trips in the perf scenario; snapshot loaded once per batch.

**Fix:** `BulkMarkCompletedAsync` was updated to accept runs in either `Pending` or `Running` status. The local perf path skips `TryClaimAsync`, so runs stay `Pending`; the bulk update now correctly marks them `Completed`.

---

## Tests Added

| Test | File | Purpose |
|------|------|---------|
| `HasNonTerminalRunsAsync_WithPendingRun_ReturnsTrue` | SimulationRunRepositoryTryClaimTests.cs | Lightweight check returns true when batch has pending runs |
| `HasNonTerminalRunsAsync_AllCompleted_ReturnsFalse` | SimulationRunRepositoryTryClaimTests.cs | Lightweight check returns false when all runs terminal |

---

## Verification

- **Build:** `dotnet build` succeeds
- **Unit tests:** Application, Core, Worker unit tests pass
- **Integration tests:** SimulationRunRepositoryTryClaimTests, DistributedBatchIntegrationTests pass
- **Determinism:** Same seed produces identical results; local perf path uses same simulation logic

---

## How to Run Perf

```bash
# Prerequisites
docker compose up -d postgres
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed

# 10K runs with synthetic snapshot
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120

# 10K runs with devseed (Winter Bingo 2025)
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --max-duration 120
```

---

## Expected Results

| Metric | Before | Target |
|--------|--------|--------|
| 10K elapsed | 95.8 s | < 40 s |
| Throughput | 104.3 runs/sec | > 250 runs/sec |
| snapshot_load invocations | 10,000 | 1 (or 0 with pre-loaded snapshot) |

Post-implementation metrics should be measured and recorded in [PERF_NOTES.md](../../PERF_NOTES.md).

---

## Guardrails Preserved

- **Distributed path:** `ExecuteAsync(Guid runId)` unchanged; Worker and integration tests use it.
- **TryClaimAsync:** Still used in distributed path; skipped only in local perf overload.
- **Batch finalization:** Semantics unchanged; only the "all terminal?" check is optimized.
- **AsNoTracking:** Callers that need to update entities use `Update()` which attaches detached entities.
