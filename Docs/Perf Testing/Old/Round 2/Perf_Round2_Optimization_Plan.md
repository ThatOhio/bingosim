# Performance Optimization Plan — Round 2

**Date:** February 3, 2025  
**Status:** Implemented — see [Perf_Round2_Implementation.md](Perf_Round2_Implementation.md)  
**Context:** 10K perf run shows ~95.8s elapsed, 104.3 runs/sec. Phase totals sum to ~3.7s; ~92 seconds unaccounted for. Target: identify and eliminate high-value bottlenecks.

---

## 1. Baseline Metrics (10K Run)

```
Runs completed: 10000 / 10000
Elapsed: 95.8s
Throughput: 104.3 runs/sec

Phase totals (ms total, count):
  persist: 3409ms total, 10000 invocations
  sim: 142ms total, 10000 invocations
  snapshot_load: 139ms total, 10000 invocations

Buffered persist: 191 flushes, 20000 rows inserted, 10000 runs updated, 191 SaveChanges, 3409ms total
```

### Key Observation: Time Gap

| Phase | Measured | % of Total |
|-------|----------|------------|
| persist | 3,409 ms | 3.6% |
| sim | 142 ms | 0.1% |
| snapshot_load | 139 ms | 0.1% |
| **Phase total** | **3,690 ms** | **3.9%** |
| **Unaccounted** | **~92,000 ms** | **~96%** |

The vast majority of elapsed time is **not** in the instrumented phases. It is spent in per-run DB round-trips and batch finalization checks that are not currently timed.

---

## 2. Root Cause Analysis

### 2.1 Per-Run DB Round-Trips (10,000 runs × 3+ calls)

Each `SimulationRunExecutor.ExecuteAsync` performs:

| Call | Location | Per Run | Round-Trips |
|------|-----------|---------|-------------|
| `GetByIdAsync(runId)` | SimulationRunRepository | Yes | 10,000 |
| `TryClaimAsync(runId)` | SimulationRunRepository | Yes | 10,000 |
| `GetByBatchIdAsync(batchId)` | EventSnapshotRepository | Yes | 10,000 |

**Total: 30,000 DB round-trips** for per-run operations.

- **Snapshot:** All 10,000 runs share the same batch → same snapshot. The executor caches the *parsed* DTO but still calls `GetByBatchIdAsync` every run. **10,000 redundant SELECTs for identical data.**
- **GetByIdAsync:** The perf loop already has all runs from `GetByBatchIdAsync(batch.Id)` at startup. Passing `runId` forces a re-fetch. With a shared scoped DbContext, some hits may come from the identity map, but the first run and any cache evictions still hit the DB.
- **TryClaimAsync:** Required for correctness in distributed mode. In **local** mode, we are the only consumer; a batch-claim or skip could apply, but semantics must stay correct.

**Estimated impact:** At ~3–4 ms per round-trip (network + DB), 30,000 × 3.5 ms ≈ **105 seconds**. This aligns with the observed gap.

### 2.2 Batch Finalization Overhead (191 flushes)

After each flush, `BufferedRunResultPersister` calls `TryFinalizeAsync(batchId)` for each distinct batch in the flush. For a single-batch perf run, that is **191 calls**.

Each `TryFinalizeAsync`:

1. `runRepo.GetByBatchIdAsync(batchId)` → loads **all 10,000 runs**
2. Checks `runs.Any(r => !r.IsTerminal)` → returns false if any run is still Pending/Running
3. Returns early (first ~190 times) before doing aggregates

**Result:** ~190 full loads of 10,000 runs = **1.9 million run records** read from the DB, almost all discarded.

**Estimated impact:** 190 × ~50–100 ms per full load ≈ **10–20 seconds**.

### 2.3 EF Core Change Tracker

- Repositories use default tracking (no `AsNoTracking`).
- Read-only queries (`GetByIdAsync`, `GetByBatchIdAsync`) still track entities.
- Over 10,000 runs, the shared scoped DbContext can accumulate thousands of entities, increasing memory and change-detection cost.

### 2.4 Simulation Engine

- **sim: 142 ms** for 10,000 runs ≈ 0.014 ms/run.
- Engine is already very fast; not a priority for this round.

---

## 3. Optimization Targets (Prioritized)

### Tier 1: High Impact, High Confidence

| # | Target | Current Cost | Est. Savings | Complexity |
|---|--------|--------------|-------------|------------|
| 1 | **Snapshot load once per batch** | 10,000 DB round-trips | ~35–40 s | Low |
| 2 | **Lightweight finalization check** | 190 × load 10K runs | ~10–15 s | Medium |
| 3 | **AsNoTracking for read-only queries** | Change tracker bloat | ~5–10 s | Low |

### Tier 2: Medium Impact

| # | Target | Current Cost | Est. Savings | Complexity |
|---|--------|--------------|-------------|------------|
| 4 | **Pass run entity in local perf path** | 10,000 GetByIdAsync | ~10–15 s* | Low |
| 5 | **Remove debug LogWarning in executor** | 20,000 log calls | Minor | Trivial |

\* Overlaps with Tier 1 if GetByIdAsync is often cached; still reduces load and simplifies flow.

### Tier 3: Lower Priority / Future

| # | Target | Notes |
|---|--------|------|
| 6 | **Batch TryClaim for local mode** | Requires new API; local-only; lower ROI |
| 7 | **Persist batch size tuning** | Already 191 flushes; 3409 ms total; minor gains |
| 8 | **PostgreSQL bulk COPY** | Out of scope for this round |

---

## 4. Detailed Optimization Plans

### 4.1 Snapshot Load Once Per Batch

**Problem:** `snapshotRepo.GetByBatchIdAsync(batchId)` is called 10,000 times for the same batch.

**Solution:** Load snapshot once per batch before the run loop; pass it into the executor or a batch-scoped cache.

**Options:**

- **A) Perf loop pre-load:** In `RunPerfScenarioAsync`, load snapshot once after `GetByBatchIdAsync(runs)`, then pass `(run, snapshot)` or `snapshot` to an overload of `ExecuteAsync` that skips snapshot load.
- **B) Executor batch cache:** Executor maintains `Dictionary<BatchId, EventSnapshotDto>` and only calls `GetByBatchIdAsync` on cache miss. Currently it caches the *parsed* DTO but still fetches the entity every run. Change: fetch once per batch, then use cache for all runs in that batch.
- **C) Snapshot parameter:** Add `ExecuteAsync(runId, EventSnapshotDto? preloadedSnapshot, ct)`. When non-null, skip `GetByBatchIdAsync` entirely.

**Recommendation:** **B** — minimal API change, works for both perf and distributed. On first run of a batch, load from DB; on subsequent runs, use cache. Add `GetByBatchIdAsync` only when `!_snapshotCache.ContainsKey(batchId)`.

**Files:** `SimulationRunExecutor.cs`, `IEventSnapshotRepository` (if interface changes)

**Tests:** Verify snapshot cache hit/miss; verify distributed workers still load correctly when cache is cold.

---

### 4.2 Lightweight Finalization Check

**Problem:** `TryFinalizeAsync` loads all 10,000 runs 191 times; the first ~190 times it returns false immediately.

**Solution:** Use a lightweight "any non-terminal?" check before loading runs.

**Implementation:**

1. Add `ISimulationRunRepository.HasNonTerminalRunsAsync(batchId)` or `GetTerminalCountAsync(batchId)`.
2. Implementation: `SELECT COUNT(*) FROM SimulationRuns WHERE BatchId = @batchId AND Status IN ('Pending','Running')` — or `SELECT 1 ... LIMIT 1` for existence.
3. In `TryFinalizeAsync`: if `HasNonTerminalRunsAsync` returns true, return false without loading runs.
4. Only when the check says "all terminal" do we load runs and proceed with aggregates.

**SQL sketch:**
```sql
SELECT 1 FROM "SimulationRuns"
WHERE "SimulationBatchId" = @batchId
  AND "Status" IN ('Pending', 'Running')
LIMIT 1;
```

**Files:** `ISimulationRunRepository.cs`, `SimulationRunRepository.cs`, `BatchFinalizationService.cs`

**Tests:** `TryFinalizeAsync` still finalizes correctly when all runs terminal; returns false when any run is Pending/Running.

---

### 4.3 AsNoTracking for Read-Only Queries

**Problem:** All reads use default tracking; 10,000+ entities accumulate in the change tracker.

**Solution:** Use `AsNoTracking()` for queries that do not mutate entities.

**Candidates:**

| Repository | Method | Mutates? |
|------------|--------|----------|
| SimulationRunRepository | GetByIdAsync | No |
| SimulationRunRepository | GetByBatchIdAsync | No |
| EventSnapshotRepository | GetByBatchIdAsync | No |
| SimulationBatchRepository | GetByIdAsync | No (when used for read) |
| TeamRunResultRepository | GetByBatchIdAsync | No |

**Implementation:** Add `.AsNoTracking()` to the query chain for these methods. Ensure callers do not rely on change tracking (e.g., no subsequent `Update` on the same instance without re-attach).

**Files:** All repository implementations listed above

**Tests:** Existing tests; ensure no behavioral change from tracking.

---

### 4.4 Pass Run Entity in Local Perf Path

**Problem:** Perf loop has `runs` from `GetByBatchIdAsync(batch.Id)` but passes only `runs[i].Id` to `ExecuteAsync`. Executor then calls `GetByIdAsync(runId)`.

**Solution:** Add overload `ExecuteAsync(SimulationRun run, EventSnapshotDto? snapshot, ct)` for the local perf path. Caller passes the run and optional snapshot; executor skips both `GetByIdAsync` and snapshot load.

**Considerations:**

- `TryClaimAsync` still needs the run ID; we have it from the entity.
- Executor must handle both `ExecuteAsync(Guid runId, ct)` (distributed) and `ExecuteAsync(SimulationRun run, EventSnapshotDto? snapshot, ct)` (local perf).
- For local perf, we could skip `TryClaimAsync` if we guarantee single consumer — but that changes semantics; document as local-only optimization.

**Files:** `ISimulationRunExecutor.cs`, `SimulationRunExecutor.cs`, `Program.cs` (RunPerfScenarioAsync)

**Tests:** Perf scenario produces identical results; integration tests for distributed path unchanged.

---

### 4.5 Remove Debug Logging

**Problem:** `SimulationRunExecutor` contains `LogWarning` before and after `SimulationRunner.Execute` (added for hang investigation). These fire 10,000 times per batch.

**Solution:** Remove or gate behind `--perf-verbose` / `IPerfScenarioOptions.Verbose`.

**Files:** `SimulationRunExecutor.cs`

---

## 5. Implementation Order

| Step | Task | Est. Savings | Risk |
|------|------|--------------|------|
| 1 | Snapshot load once per batch (4.1) | ~35–40 s | Low |
| 2 | Lightweight finalization check (4.2) | ~10–15 s | Low |
| 3 | AsNoTracking for read-only queries (4.3) | ~5–10 s | Low |
| 4 | Pass run entity in local perf path (4.4) | ~10–15 s | Low |
| 5 | Remove debug LogWarning (4.5) | Minor | Trivial |

**Suggested order:** 1 → 2 → 3 → 5 → 4. Steps 1–3 and 5 are independent; step 4 may overlap with 1 but is still valuable for clarity and local perf.

---

## 6. Success Criteria

| Metric | Current | Target |
|--------|---------|--------|
| 10K elapsed | 95.8 s | < 40 s |
| Throughput | 104.3 runs/sec | > 250 runs/sec |
| Phase total vs elapsed | ~4% | > 50% (less unaccounted time) |

---

## 7. Verification

1. **Before/after perf run:**
   ```bash
   dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120
   ```

2. **Determinism:** Same seed → identical `TeamRunResult` rows and aggregates.

3. **Distributed:** `DistributedBatchIntegrationTests` and manual 2-worker run still pass.

4. **Regression guard:**
   ```bash
   dotnet run --project BingoSim.Seed -- --perf-regression --runs 1000 --min-runs-per-sec 100
   ```

---

## 8. Out of Scope (This Round)

- Batch `TryClaimAsync` for local mode
- PostgreSQL `COPY` or other bulk insert
- Simulation engine micro-optimizations
- Connection pool tuning
- Changing batch finalization semantics (only optimizing the "all terminal?" check)

---

## 9. References

- [DB_Optimize_01_Plan.md](../DB%20Optimize%2001/DB_Optimize_01_Plan.md) — Batched persistence
- [DB_Optimize_01_Implementation.md](../DB%20Optimize%2001/DB_Optimize_01_Implementation.md) — Implementation summary
- [PERF_NOTES.md](../../PERF_NOTES.md) — How to run perf, interpreting metrics
- [Perf_Investigation_Outcome.md](../Perf_Investigation_Outcome.md) — Infinite loop fix, synthetic snapshot
