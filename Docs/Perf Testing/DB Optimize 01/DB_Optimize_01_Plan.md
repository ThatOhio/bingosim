# DB Write Optimization Plan — DB Optimize 01

**Status:** Plan only — no implementation yet  
**Date:** February 3, 2025  
**Context:** Perf measurements show persistence dominates (~66s for 2371 runs ≈ 28ms/run); sim is negligible. Must optimize DB write patterns.

---

## 1) DB Writes Per Run During Perf Execution

### 1.1 Inventory of Writes

| # | Write | Location | Per Run? | SaveChanges? |
|---|-------|----------|----------|--------------|
| 1 | **Run status Pending→Running** | `SimulationRunRepository.TryClaimAsync` | Yes | No (ExecuteUpdateAsync) |
| 2 | **Delete TeamRunResults by runId** | `TeamRunResultRepository.DeleteByRunIdAsync` | Yes | Yes |
| 3 | **Insert TeamRunResults** | `TeamRunResultRepository.AddRangeAsync` | Yes | Yes |
| 4 | **Run status Running→Completed** | `SimulationRunRepository.UpdateAsync` | Yes | Yes |
| 5 | **Batch aggregates** | `BatchFinalizationService.TryFinalizeAsync` | No (per batch, when last run done) | Yes (2×) |
| 6 | **Batch status transition** | `SimulationBatchRepository.TryTransitionToFinalAsync` | No (per batch) | No (ExecuteUpdateAsync) |

### 1.2 Per-Run Write Flow (Success Path)

```
SimulationRunExecutor.ExecuteAsync:
  1. runRepo.TryClaimAsync(runId)           → ExecuteUpdate (Pending→Running)
  2. resultRepo.DeleteByRunIdAsync(runId)   → SELECT + RemoveRange + SaveChanges
  3. resultRepo.AddRangeAsync(entities)    → AddRange + SaveChanges
  4. runRepo.UpdateAsync(run)               → Update + SaveChanges
  5. finalizationService.TryFinalizeAsync  → (only when all runs terminal)
```

**Current:** 3 SaveChanges per run + 1 ExecuteUpdate per run.

### 1.3 Polling / UI Feedback

- **GetProgressAsync** (SimulationResults.razor, every 3s): Reads `SimulationRuns` by batchId, counts Pending/Running/Completed/Failed. **Requires run status in DB** for live progress.
- **GetBatchAggregatesAsync**: Reads `BatchTeamAggregates` — only populated after batch finalization.
- **GetRunResultsAsync** (sample timelines): Reads `TeamRunResults` — only populated after run completes.

---

## 2) Per-Write Analysis

| Write | Required for Correctness? | Required for UI Feedback? | Can Defer/Batch? |
|-------|---------------------------|---------------------------|------------------|
| **TryClaimAsync** (Pending→Running) | **Yes** — prevents double execution (distributed) | Yes — "Running" count | **No** — must be atomic per run before execution |
| **DeleteByRunIdAsync** | **Only on retry** — idempotency if partial write | No | **Yes** — for first-run success, always 0 rows; can skip or batch |
| **AddRangeAsync** (TeamRunResults) | **Yes** — final results | Yes — sample timelines after completion | **Yes** — batch K runs |
| **UpdateAsync** (Running→Completed) | **Yes** — run completion | Yes — "Completed" count, finalization trigger | **Yes** — batch with AddRange |
| **Batch finalization** | **Yes** — aggregates, batch status | Yes — aggregates table, batch Completed | **No** — already per-batch, runs once |

### 2.1 DeleteByRunIdAsync Rationale

- **First-run success:** No existing rows; DELETE affects 0 rows. **Wasteful** — SELECT + DELETE roundtrip.
- **Retry after partial failure:** If AddRange succeeded but Update failed, we'd have orphaned results. Delete ensures idempotency on retry.
- **Proposal:** Skip Delete when we know it's first attempt (no prior partial write). For batched mode, we never have partial writes before first flush, so **omit Delete entirely** in batch path.

---

## 3) Batching Approach

### 3A) Local Mode Batching

**Idea:** Execute K runs in memory, accumulate results, flush once per K runs.

| Step | Action |
|------|--------|
| 1 | Claim K runs (or 1 at a time to preserve TryClaim semantics — see below) |
| 2 | Execute K simulations in memory (no DB writes) |
| 3 | Accumulate: List of (runId, TeamRunResult entities) |
| 4 | **Flush:** Single SaveChanges with: AddRange(all TeamRunResults) + ExecuteUpdate(runIds, Completed) |
| 5 | Call TryFinalizeAsync once per flush (or only when batch complete) |

**TryClaim constraint:** TryClaimAsync must remain **per-run** and **before** execution to prevent double-claim in distributed mode. For **local-only** batched path, we can:
- **Option A:** Claim 1 run, execute, accumulate; repeat K times; then flush. Claims stay per-run.
- **Option B:** Add `TryClaimBatchAsync(runIds)` that atomically claims up to K pending runs. Simpler but new API.

**Recommendation:** Option A — keep TryClaim per-run. Batch only the **persist** phase: accumulate K (run, results) pairs, then one SaveChanges for all inserts + bulk ExecuteUpdate for statuses.

**Flow (Option A):**
```
for i = 0 to runs.Count step K:
  buffer = []
  for j = 0 to K-1:
    run = runs[i+j]
    if !TryClaimAsync(run.Id): skip
    results = runner.Execute(snapshot, run.Seed)
    buffer.Add((run, results))
  Flush(buffer)  // AddRange(all TeamRunResults) + ExecuteUpdate(runIds→Completed) + SaveChanges
  TryFinalizeAsync(batchId)
```

**Flush contents:**
- `context.TeamRunResults.AddRange(allEntities)` — one AddRange
- `context.SimulationRuns.Where(r => runIds.Contains(r.Id)).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, Completed).SetProperty(...))` — bulk status
- `context.SaveChangesAsync()` — **1 SaveChanges** per K runs (instead of 3K)

**DeleteByRunIdAsync:** Omit in batch path. We never have prior results for these runs.

### 3B) Distributed Mode Batching

**Idea:** Each worker batches its own completed runs and writes in chunks.

| Mechanism | Description |
|-----------|-------------|
| **Worker-local buffer** | Accumulate (runId, TeamRunResult[]) in memory |
| **Flush trigger** | Count-based (K runs) OR time-based (e.g. 500ms) — whichever first |
| **Memory cap** | K × avg teams per run × ~1KB ≈ 100 runs × 2 teams × 1KB ≈ 200KB per 100 runs. Safe. |
| **TryClaim** | Stays per-run, before execution (unchanged) |
| **Flush** | Same as local: AddRange + ExecuteUpdate(runIds) + SaveChanges |

**Concurrency:** Multiple workers flush independently. No cross-worker coordination. Each worker's flush is independent.

**Failure handling:** If worker crashes before flush, runs remain "Running". Existing retry/claim logic: another worker can re-claim after timeout (if any) or runs stay stuck. **Mitigation:** Flush on smaller K or shorter time to reduce loss. Document that very large K increases crash recovery gap.

---

## 4) Minimal EF Tuning

| Tuning | Where | Effect |
|--------|-------|--------|
| **AutoDetectChangesEnabled = false** | During bulk insert flush | Skips change detection scan; we explicitly AddRange |
| **Single DbContext per flush** | Use scoped DbContext for each flush | Avoid long-lived context with many tracked entities |
| **AddRange + single SaveChanges** | TeamRunResultRepository or new bulk method | One roundtrip for N entities |
| **ExecuteUpdate for status** | SimulationRunRepository | Already used for TryClaim; add `BulkMarkCompletedAsync(runIds, completedAt)` |

**DbContext scope:** Perf scenario and Worker use `CreateScope()` per operation. For batched flush, create a scope, get DbContext, AddRange, ExecuteUpdate, SaveChanges, dispose. No need to change default.

---

## 5) Instrumentation

**Persist phase should report:**

| Metric | How |
|--------|-----|
| Rows inserted | Count of TeamRunResult entities in AddRange |
| Rows updated | Count of SimulationRuns in ExecuteUpdate (or 0 if using ExecuteUpdate which returns rowsAffected) |
| SaveChanges count | Increment counter per SaveChangesAsync call |
| Elapsed ms | Stopwatch around entire flush |

**Output format (per flush or per batch):**
```
persist: 1 flush, 200 TeamRunResults inserted, 100 runs updated, 1 SaveChanges, 45ms
```

**Aggregate at end:**
```
Phase totals: persist: 23 flushes, 2371 runs, 4742 rows inserted, 2371 runs updated, 23 SaveChanges, 1200ms total
```

---

## 6) Implementation Order and Test Plan

### 6.1 Step-by-Step Implementation Order

| Step | Task | Files | Risk |
|------|------|-------|------|
| 1 | Add `BulkMarkCompletedAsync(runIds, completedAt)` to ISimulationRunRepository + impl | Core, Infra | Low |
| 2 | Add `AddRangeWithoutSaveAsync` + caller does SaveChanges (or new `BulkPersistRunResultsAsync(runIds, entities)` that does AddRange + ExecuteUpdate + SaveChanges in one scope) | Core, Infra | Low |
| 3 | Add `IBulkRunResultPersistence` or extend `ITeamRunResultRepository` with `AddRangeBulkAsync(IEnumerable<TeamRunResult>, IEnumerable<Guid> runIdsToMarkCompleted)` — single method that does both in one SaveChanges | Core, Infra | Medium |
| 4 | Add batch size K config (e.g. `LocalSimulation:BatchSize` default 100) | Web, Infra | Low |
| 5 | **Local mode:** Add batched execution path in BingoSim.Seed perf scenario — loop that accumulates K runs, then calls bulk persist | Seed | Medium |
| 6 | **Local mode:** Add batched path to SimulationRunQueueHostedService — requires coordinator to batch dequeued run IDs before passing to executor, OR executor accepts "batch mode" and processes N runs before flush | Web, Application | Medium |
| 7 | **Distributed mode:** Add batched flush in SimulationRunExecutor — buffer in executor, flush when K reached or on dispose | Application, Infra | Medium |
| 8 | Add instrumentation (rows inserted, SaveChanges count, elapsed) to persist phase | Application, Seed | Low |
| 9 | Remove or conditionalize DeleteByRunIdAsync in batch path | Application | Low |

**Simpler first cut:** Implement **only** for the **perf scenario** (BingoSim.Seed) which runs sequentially and controls the loop. That gives immediate benefit without touching SimulationRunQueueHostedService or Worker. Then extend to hosted service and worker.

### 6.2 Test Plan

| Test | Purpose |
|------|---------|
| **Determinism** | Same seed, batched vs non-batched — identical TeamRunResults (compare row counts, aggregates) |
| **Existing tests pass** | `dotnet test --filter "Category!=Perf"` |
| **Perf baseline** | Before: 2371 runs, ~66s persist. After: expect persist <20s (e.g. 23 flushes × ~1s = 23s or better) |
| **Runs/sec** | Must improve. Target: 2–3× current (e.g. from ~36 to ~100+ runs/sec for 2371 runs) |
| **Integration:** | `DistributedBatchIntegrationTests` — ensure distributed path still works |
| **Finalization** | Batch finalization still computes correct aggregates after batched persist |

### 6.3 Rollback

- Batch size K=1 reverts to per-run persist (current behavior).
- Feature flag or config: `BatchSize: 1` = per-run; `BatchSize: 100` = batched.

---

## 7) Concrete File List and Proposed Methods

### 7.1 New/Modified Files

| File | Change |
|------|--------|
| `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` | Add `Task BulkMarkCompletedAsync(IReadOnlyList<Guid> runIds, DateTimeOffset completedAt, CancellationToken ct = default)` |
| `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` | Implement `BulkMarkCompletedAsync` via ExecuteUpdate |
| `BingoSim.Core/Interfaces/ITeamRunResultRepository.cs` | Add `Task AddRangeAsync(IEnumerable<TeamRunResult> results, CancellationToken ct)` — keep existing; add optional `Task<int> AddRangeBulkAsync(...)` that returns count? Or just use AddRange + let caller SaveChanges. Simpler: add `Task AddRangeAsync(..., bool saveChanges)` — or new interface `IBulkRunPersistService` that encapsulates the flush. |
| `BingoSim.Application/Interfaces/IBulkRunPersistService.cs` | **New.** `Task FlushAsync(IReadOnlyList<(SimulationRun run, IReadOnlyList<TeamRunResultDto> results)>, CancellationToken ct)` — creates entities, AddRange, BulkMarkCompleted, SaveChanges. |
| `BingoSim.Infrastructure/Services/BulkRunPersistService.cs` | **New.** Implements IBulkRunPersistService. Uses AppDbContext, AutoDetectChangesEnabled=false during flush. |
| `BingoSim.Application/Services/SimulationRunExecutor.cs` | Add optional batch mode: when `IBulkRunPersistService` and batch buffer provided, accumulate instead of immediate persist. Or: keep executor per-run; add **SimulationRunBatchExecutor** that orchestrates K runs and calls BulkRunPersistService. |
| `BingoSim.Seed/Program.cs` | Use batch executor when `--perf` and batch size > 1. Loop: get K runs, execute K, flush. |
| `BingoSim.Infrastructure/DependencyInjection.cs` | Register `IBulkRunPersistService` |
| `appsettings.json` / `LocalSimulationOptions` | Add `BatchSize` (default 100) |

### 7.2 Proposed New Methods (Signatures)

```csharp
// ISimulationRunRepository
Task BulkMarkCompletedAsync(IReadOnlyList<Guid> runIds, DateTimeOffset completedAt, CancellationToken cancellationToken = default);

// IBulkRunPersistService (new interface)
// completedRuns: (RunId, CompletedAt, Results from SimulationRunner)
Task<BulkPersistResult> FlushAsync(
    IReadOnlyList<(Guid RunId, DateTimeOffset CompletedAt, IReadOnlyList<TeamRunResultDto> Results)> completedRuns,
    CancellationToken cancellationToken = default);

// BulkPersistResult (new DTO)
record BulkPersistResult(int RowsInserted, int RunsUpdated, int SaveChangesCount, long ElapsedMs);
```

### 7.3 K (Batch Size) Default Suggestion

| Scenario | Suggested K | Rationale |
|----------|-------------|-----------|
| **Local perf (Seed)** | **100** | 2371 runs → 24 flushes. ~4ms/run → ~400ms/flush. Balance between roundtrips and memory. |
| **Local hosted service** | **50** | Shorter flush interval for UI progress; 50 runs × 2 teams = 100 rows. |
| **Distributed worker** | **50** | Same; workers may process interleaved batches. |
| **Conservative** | **25** | If memory or latency concerns. |

**Default: K = 100** for perf scenario. Configurable via `LocalSimulation:BatchSize` and `WorkerSimulation:BatchSize`.

---

## 8) Summary

| Item | Decision |
|------|----------|
| **Per-run writes** | TryClaim (required, per-run), Delete (skip in batch), AddRange (batch), Update (batch) |
| **Batching** | Accumulate K runs, single AddRange + ExecuteUpdate + SaveChanges per flush |
| **Local mode** | Seed perf loop uses batch executor; hosted service can be extended later |
| **Distributed mode** | Worker executor buffers and flushes when K reached or time elapsed |
| **EF tuning** | AutoDetectChangesEnabled=false during flush, single DbContext per flush |
| **Instrumentation** | Rows inserted, runs updated, SaveChanges count, elapsed ms |
| **K default** | 100 for perf, 50 for hosted/worker |
| **Determinism** | Preserved — same seed → same results; batching only changes persist timing |

---

## 9) Out of Scope (This Plan)

- Changing TryClaim to batch-claim (would require distributed coordination)
- Changing batch finalization (already per-batch)
- PostgreSQL-specific bulk copy (e.g. COPY) — future optimization if EF batching insufficient
- Read path optimization (GetProgressAsync, etc.)
