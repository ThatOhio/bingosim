# Distributed Simulation Performance Plan

**Date:** February 3, 2025  
**Status:** Phase 1 Implemented; Phase 2 Implemented; Phase 3 Implemented  
**Context:** Running 10,000 simulations in distributed mode with 3 workers takes roughly the same time as with 1 worker. Phase 2 addresses snapshot cache and claim observability. CPU and RAM are underutilized. Goal: achieve measurable speedup when scaling workers (e.g., 3 workers ≈ 2–3× faster than 1 worker).

---

## 1. Executive Summary

Phase 1 (per-worker concurrency, prefetch, metrics) is implemented. **Phase 1 test results** ([Phase 1 Test Results.md](Phase%201%20Test%20Results.md)) show that 3 workers still achieve the same aggregate throughput as 1 worker (~980–990 runs/10s). CPU and RAM remain under 20–25% utilization. The bottleneck has shifted from concurrency to **database I/O**.

Phase 1 metrics reveal:
- **Claim phase dominates:** ~13–14 ms per run; claim time totals ~14,000 ms per 10s for ~1,000 runs.
- **Snapshot cache is not shared:** `snapshot_load` count ≈ 991 per 10s (one worker) — each scoped executor loads the snapshot instead of reusing a shared cache.
- **Workers are I/O bound:** Sim and snapshot_load are negligible; persist is secondary. The database is the limiting factor.

This plan identifies improvement areas. Phases 2+ are updated to address the database bottleneck: shared snapshot cache, claim optimization, and batch claims.

---

## 2. Current Architecture Summary

### 2.1 Flow

1. **Web** (`SimulationBatchService.StartBatchAsync`): Creates batch, snapshot, and N `SimulationRun` rows. Background task calls `PublishRunWorkBatchAsync` to publish all run IDs to RabbitMQ via MassTransit.
2. **RabbitMQ**: Holds `ExecuteSimulationRun` messages (one per run). MassTransit creates a queue bound to the `ExecuteSimulationRun` exchange.
3. **Workers**: Each worker runs `ExecuteSimulationRunConsumer`, which consumes messages with `ConcurrentMessageLimit` and `PrefetchCount` from `ExecuteSimulationRunConsumerDefinition`, calls `SimulationRunExecutor.ExecuteAsync`, and acks.
4. **Executor**: Loads run + snapshot (cache is per-executor, so effectively per message in distributed mode — not shared), runs simulation, persists via `BufferedRunResultPersister`, updates run status, triggers batch finalization.
5. **Batch finalization**: `BatchFinalizerHostedService` scans every 15 seconds; `BufferedRunResultPersister` also calls `TryFinalizeAsync` on each flush.

### 2.2 Key Configuration

| Component | Setting | Current Value | Notes |
|-----------|---------|---------------|-------|
| Local mode | `LocalSimulationOptions.MaxConcurrentRuns` | 4 | 4 runs in parallel per Web host |
| Worker | `WorkerSimulationOptions.MaxConcurrentRuns` | 4 (CPU-aware default) | Per-worker concurrency |
| Worker | `PrefetchCount` | MaxConcurrentRuns × 2 | RabbitMQ prefetch |
| Worker | `SimulationPersistence.BatchSize` | 50 | Buffered flush threshold |
| Worker | `SimulationPersistence.FlushIntervalMs` | 500 | Time-based flush |
| compose | `WORKER_REPLICAS` | 3 | Number of worker containers |

### 2.3 Concurrency Comparison (Post–Phase 1)

| Mode | Parallelism | Observed throughput |
|------|-------------|---------------------|
| Distributed (1 worker) | 4 concurrent | ~980 runs/10s |
| Distributed (3 workers) | 12 concurrent (4 × 3) | ~990 runs/10s aggregate |

**Conclusion:** Phase 1 increased per-worker concurrency, but 3 workers do not improve aggregate throughput vs. 1 worker. The bottleneck is **database I/O**, not CPU.

---

## 3. Root Cause Analysis

### 3.1 Primary: Database I/O Bottleneck (Phase 1 Test Results)

- **Evidence:** Phase 1 throughput logs show claim phase dominates: ~13–14 ms per run, ~14,000 ms total per 10s for ~1,000 runs. Sim and snapshot_load are negligible; persist is secondary.
- **Impact:** Workers are I/O bound. CPU and RAM stay under 20–25% utilization. Adding workers does not increase aggregate throughput because all workers contend on the same PostgreSQL instance.
- **Reference:** [Phase 1 Test Results](Phase%201%20Test%20Results.md)

### 3.2 Snapshot Cache Not Shared Across Messages

- **Evidence:** `snapshot_load` count ≈ 991 per 10s (1 worker) — nearly one load per run. The cache lives in scoped `SimulationRunExecutor`; each message gets a new executor, so the cache is never reused.
- **Impact:** Redundant DB round-trips for snapshot. Should be 1 load per batch, not per run.

### 3.3 TryClaimAsync Dominates

- **Evidence:** Claim phase totals ~13,000–14,000 ms per 10s for ~1,000 runs (~14 ms per claim). Each run does one `ExecuteUpdateAsync` to transition Pending → Running.
- **Impact:** Single round-trip per run; with 1,000 runs/10s, this is the primary DB load. Batch claims would reduce round-trips.

### 3.4 Database Connection Pool and Contention

- **Evidence:** All workers share the same PostgreSQL. `TryClaimAsync`, `BulkMarkCompletedAsync`, and `BufferedRunResultPersister` flushes all hit the DB.
- **Impact:** Connection pool exhaustion or lock contention may limit throughput. Monitor if scaling further.

### 3.5 Batch Publish and Finalization (Minor)

- **Batch publish:** Already implemented via `PublishRunWorkBatchAsync` and `PublishBatch`. No change needed for 10K runs.
- **Batch finalization:** `TryFinalizeAsync` is called on every `BufferedRunResultPersister` flush and by `BatchFinalizerHostedService` every 15 seconds. Some redundancy but not a major bottleneck.

---

## 4. Improvement Areas (Prioritized)

### 4.1 Area 1: Per-Worker Concurrency (High Impact) — IMPLEMENTED

**Goal:** Allow each worker to process multiple simulation runs concurrently, similar to local mode.

**Options:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A. MassTransit `ConcurrentMessageLimit` | Configure receive endpoint to process N messages concurrently | Simple, built-in | All consumers share same limit |
| B. MassTransit `PrefetchCount` | Increase prefetch so multiple messages are delivered per consumer | Simple | Prefetch alone does not increase processing concurrency |
| C. Both | Set `PrefetchCount` (e.g., 16) and `ConcurrentMessageLimit` (e.g., 4–8) | Best throughput | Need to tune for host resources |

**Recommendation:** Option C. Configure `PrefetchCount` and `ConcurrentMessageLimit` on the `ExecuteSimulationRun` receive endpoint.

**Implementation sketch:**
- Add `WorkerSimulationOptions.MaxConcurrentRuns` (default: 4, align with local mode).
- In Worker `Program.cs`, when configuring RabbitMQ receive endpoints, set:
  - `PrefetchCount = MaxConcurrentRuns * 2` (or similar)
  - `ConcurrentMessageLimit = MaxConcurrentRuns`
- Expose via `appsettings.json` and environment variable (e.g., `WORKER_MAX_CONCURRENT_RUNS`).

**Expected impact:** With 3 workers × 4 concurrent = 12 runs in parallel, expect ~3–4× speedup vs. current 3 runs in parallel.

---

### 4.2 Area 2: RabbitMQ Prefetch Tuning (High Impact) — IMPLEMENTED

**Goal:** Ensure workers receive messages promptly so they are not idle between runs.

**Implementation:** Part of Area 1; `PrefetchCount = MaxConcurrentRuns * 2`.

---

### 4.3 Area 3: Shared Snapshot Cache (High Impact — Phase 1 Test Results)

**Goal:** Eliminate redundant snapshot loads. Current: ~991 loads per 10s (1 worker) instead of 1 per batch.

**Current:** Cache lives in scoped `SimulationRunExecutor`; each message gets a new executor, so the cache is never reused across messages.

**Recommendation:** Use a shared cache (e.g. `IMemoryCache` or singleton) keyed by batch ID. Snapshot is loaded once per batch and reused by all runs from that batch.

**Implementation sketch:** Add `IEventSnapshotCache` (or use `IMemoryCache` with batch ID key). Executor checks shared cache before loading from DB. Evict on batch completion or TTL.

**Expected impact:** Reduce snapshot_load from ~1,000 DB round-trips per 10s to 1 per batch.

---

### 4.4 Area 4: TryClaimAsync Optimization (High Impact — Phase 1 Test Results)

**Goal:** Reduce claim phase time (~14 ms per run, dominates throughput).

**Current:** One `ExecuteUpdateAsync` per run to transition Pending → Running. Each run = one DB round-trip.

**Recommendations:**
1. **Indexes:** Ensure index supports `WHERE Id = @runId AND Status = @pending`. Verify query plan.
2. **Batch claims:** Add `ClaimBatchAsync` — worker claims N runs in one round-trip, processes them, then claims next batch. Reduces round-trips by factor of N.
3. **Database locality:** If PostgreSQL runs in Docker, measure latency. Consider same-host or optimized networking.

**Expected impact:** Batch claims (e.g. N=10) could reduce claim round-trips from 1,000 to 100 per 10s.

---

### 4.5 Area 5: Buffered Persistence Tuning (Medium Impact)

**Goal:** Reduce DB round-trips and contention when multiple workers flush concurrently.

**Current:** `BatchSize = 50`, `FlushIntervalMs = 500`. Each worker has its own buffer. Flushes do `AddRangeAsync` + `BulkMarkCompletedAsync` + `SaveChangesAsync`.

**Considerations:**
- Larger `BatchSize` → fewer flushes, larger transactions. May reduce DB load.
- Smaller `BatchSize` → more frequent flushes, smaller transactions. May increase lock contention.
- With higher concurrency, more runs complete per second; buffer fills faster. Current 50 may be reasonable; consider 100–200 if DB becomes bottleneck.

**Recommendation:** Add configuration knobs and document recommended values. Consider increasing `BatchSize` to 100 for distributed mode if DB latency is observed. Defer until Areas 1–2 are implemented and measured.

---

### 4.6 Area 6: Database Connection Pool (Medium Impact)

**Goal:** Avoid connection pool exhaustion when workers run many concurrent simulations.

**Current:** Default Npgsql/EF Core connection pool (typically 100 connections per connection string).

**Consideration:** Each `SimulationRunExecutor` uses scoped DbContext. With 3 workers × 8 concurrent = 24 concurrent executions, each may hold a connection during DB operations. Plus `BatchFinalizerHostedService`, `BufferedRunResultPersister` flushes. Total connections can grow.

**Recommendation:** Monitor connection usage. If needed, increase `Maximum Pool Size` in the connection string or ensure connection lifetime is short. Add to plan as a follow-up if connection errors appear after increasing concurrency.

---

### 4.7 Area 7: Batch Publish Chunking (Low Impact for 10K)

**Goal:** Avoid memory/network pressure when publishing very large batches (e.g., 50K+ runs).

**Current:** `PublishRunWorkBatchAsync` sends all run IDs in one `PublishBatch` call. For 10K runs, this is acceptable.

**Recommendation:** Add optional chunking (e.g., 2,000 runs per chunk) for 50K+ batches. Document in `Batch_Publish_Refactor_Implementation.md`. Low priority for current 10K scenario.

---

### 4.8 Area 8: Observability and Validation (Supporting) — IMPLEMENTED

**Goal:** Validate that scaling workers yields measurable speedup and identify remaining bottlenecks.

**Recommendations:**
- Add timing metrics for: batch publish duration, time-to-first-message, runs-per-second per batch.
- Document a simple benchmark procedure: run 10K distributed with 1, 2, 3 workers; record elapsed time and runs/sec.
- Consider a `WORKER_REPLICAS` vs. throughput table in `PERF_NOTES.md`.

---

## 5. Implementation Phases

### Phase 1: Per-Worker Concurrency (Areas 1 + 2) — IMPLEMENTED

| Step | Task | Status |
|------|------|--------|
| 1 | Add `WorkerSimulationOptions.MaxConcurrentRuns` and document in appsettings | Done |
| 2 | Configure MassTransit via `ConsumerDefinition` with `PrefetchCount` and `ConcurrentMessageLimit` | Done |
| 3 | Expose via environment variable in compose | Done |
| 4 | Add integration test for concurrent message processing | Done |
| 5 | Add worker throughput logging (phase totals, runs/10s) | Done |

**Phase 1 result:** Concurrency and prefetch implemented. Test results show DB is the bottleneck; 3 workers = same throughput as 1 worker. See [Phase 1 Test Results](Phase%201%20Test%20Results.md).

---

### Phase 2: Shared Snapshot Cache + Claim Optimization (Areas 3 + 4) — IMPLEMENTED

| Step | Task | Status |
|------|------|--------|
| 1 | Add shared snapshot cache (e.g. `IMemoryCache` keyed by batch ID) | Done — `ISnapshotCache`, `SharedSnapshotCache` |
| 2 | Executor checks shared cache before loading from DB; evict on batch completion or TTL | Done — TTL 15 min, bounded 32 entries |
| 3 | Verify indexes on `SimulationRuns` for `TryClaimAsync` (Id, Status) | Done — PK on Id sufficient; no migration |
| 4 | Document connection pool considerations; add `Maximum Pool Size` if needed | See `PERF_NOTES.md` |
| 5 | (Optional) Implement `ClaimBatchAsync` — worker claims N runs per round-trip | Skipped — architectural churn |

**Success criteria:** `snapshot_load` count drops to 1 per batch; claim phase time reduced or batch claims reduce round-trips. 3 workers should show measurable throughput increase vs. 1 worker.

**Bottlenecks addressed:** (1) Redundant snapshot DB loads (~1 per run → ~1 per batch); (2) Claim observability (avg latency, DB error tagging). Claim index verified (PK sufficient); batch claiming deferred.

**Results / Observations:** See [Phase 2 Test Results](Phase%202%20Test%20Results.md). Snapshot cache works (`snapshot_cache_hit` ≈ run count; no `snapshot_load` in steady state). Throughput unchanged: 1 worker ~1000 runs/10s, 3 workers ~990 aggregate. Workers still splitting the same load. **Root cause:** Snapshot load was negligible (~44ms/10s); **claim dominates** (~13–14 ms × 1000 runs ≈ 14,000 ms/10s). Eliminating snapshot_load did not move the needle. Claim remains the bottleneck.

---

### Phase 3: Claim Reduction + Persistence Tuning (Areas 4 + 5 + 6 + 7) — IMPLEMENTED

Phase 2 proved that **claim round-trips** are the primary bottleneck. Phase 3 reduces claim-related DB load via batch messaging (Option B).

| Step | Task | Status |
|------|------|--------|
| 1 | **Batch claiming** — `ClaimBatchAsync` claims N runs in one round-trip | Done — `ExecuteSimulationRunBatch` message, `ExecuteSimulationRunBatchConsumer` |
| 2 | **Enrich message with BatchId + Seed** | Deferred — Phase 3B candidate; measure first |
| 3 | **Connection pool tuning** | See `PERF_NOTES.md` |
| 4 | **Persistence BatchSize** | Existing config; consider 100 for distributed |
| 5 | **PostgreSQL tuning** | Low priority |
| 6 | **Optional chunking** for 50K+ publish | Deferred |

**Implementation:** Option B — `ExecuteSimulationRunBatch { SimulationRunIds }`. Web chunks run IDs by `DistributedExecution:BatchSize` (default 10), publishes one batch message per chunk. Worker consumes batch, calls `ClaimBatchAsync`, processes claimed runs with `skipClaim: true`, acks. Retries publish `ExecuteSimulationRunBatch` with failed run IDs (batch of 1 valid).

**Success criteria:** Claim round-trips drop (e.g. 1000 → 100 per 10s); 3 workers show 2–3× aggregate throughput vs 1 worker.

**Results / Observations:** See [Phase 3 Implementation Summary](Phase_3_Implementation_Summary.md). Run benchmark per [PERF_NOTES.md](../PERF_NOTES.md) Phase 3 procedure to validate.

---

## 6. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| DB connection exhaustion | Monitor connections; tune pool size; consider reducing `MaxConcurrentRuns` if needed |
| Increased DB lock contention | `TryClaimAsync` uses `ExecuteUpdateAsync` (no long-held locks); `BulkMarkCompletedAsync` is batch. Monitor for deadlocks. |
| Memory pressure per worker | `MaxConcurrentRuns` caps concurrency; snapshot cache is bounded (32 entries). Shared cache (Phase 2) will add batch-keyed entries. Monitor RSS. |
| Retry/error handling with concurrency | Each message is independent; retries re-publish single run. No change to retry logic. |

---

## 7. Phase 2 Investigation Summary

**Why did Phase 2 not improve throughput?**

| Phase | snapshot_load | claim | Throughput (1 worker) | Throughput (3 workers) |
|-------|---------------|-------|------------------------|------------------------|
| 1 | ~991 loads/10s, ~44ms total | ~14,000 ms/10s | ~990 runs/10s | ~990 aggregate |
| 2 | ~1 load/batch (cache hit) | ~14,000 ms/10s | ~1000 runs/10s | ~990 aggregate |

Snapshot load was **&lt;1%** of total time. Claim is **~90%**. Eliminating snapshot_load had negligible effect. All workers contend on the same PostgreSQL for claim; the DB processes ~1000 claims/10s regardless of worker count. To scale, we must reduce claim round-trips (batch claiming) or reduce other per-run round-trips (e.g. GetByIdAsync via message enrichment).

---

## 8. References

- `BingoSim.Worker/Program.cs` — MassTransit and consumer registration
- `BingoSim.Worker/Consumers/ExecuteSimulationRunBatchConsumer.cs` — Batch consumer (Phase 3)
- `BingoSim.Web/Services/SimulationRunQueueHostedService.cs` — Local mode concurrency (MaxConcurrentRuns)
- `Docs/04_Architecture.md` — Concurrency model (In-Process Parallelism)
- `Docs/08_Feature_Audit_2025.md` — Worker MaxConcurrentRuns gap
- `Docs/09_Requirements_Review.md` — Worker concurrency note
- `Docs/PERF_NOTES.md` — Baseline and benchmark procedure
- `Docs/Distributed Perf/Phase 1 Test Results.md` — Phase 1 throughput metrics and bottleneck analysis
- `Docs/Distributed Perf/Phase 2 Test Results.md` — Phase 2 results; snapshot cache working, claim still bottleneck
- [MassTransit RabbitMQ Configuration](https://masstransit.io/documentation/configuration/transports/rabbitmq) — PrefetchCount, endpoint options
- [MassTransit Discussion #2368](https://github.com/MassTransit/MassTransit/discussions/2368) — Increasing consumers and prefetch
