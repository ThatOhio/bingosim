# Distributed Simulation Performance Plan

**Date:** February 3, 2025  
**Status:** Phase 1 Implemented (per-worker concurrency, prefetch, metrics, benchmark)  
**Context:** Running 10,000 simulations in distributed mode with 3 workers takes roughly the same time as with 1 worker. CPU and RAM are underutilized. Goal: achieve measurable speedup when scaling workers (e.g., 3 workers ≈ 2–3× faster than 1 worker).

---

## 1. Executive Summary

The distributed simulation pipeline has several bottlenecks that prevent horizontal scaling from yielding expected throughput gains. The primary issue is **per-worker concurrency**: each worker processes exactly **one message at a time**, whereas local mode runs **4 concurrent simulations** per Web host. With 3 workers, distributed mode therefore runs only 3 simulations in parallel—less than local mode—and underutilizes available CPU and RAM.

This plan identifies six improvement areas, ordered by expected impact. Implementing the first two (per-worker concurrency and RabbitMQ prefetch) should yield the most significant gains.

---

## 2. Current Architecture Summary

### 2.1 Flow

1. **Web** (`SimulationBatchService.StartBatchAsync`): Creates batch, snapshot, and N `SimulationRun` rows. Background task calls `PublishRunWorkBatchAsync` to publish all run IDs to RabbitMQ via MassTransit.
2. **RabbitMQ**: Holds `ExecuteSimulationRun` messages (one per run). MassTransit creates a queue bound to the `ExecuteSimulationRun` exchange.
3. **Workers**: Each worker runs `ExecuteSimulationRunConsumer`, which consumes one message, calls `SimulationRunExecutor.ExecuteAsync`, and acks. No concurrency configuration.
4. **Executor**: Loads run + snapshot (cached per batch), runs simulation, persists via `BufferedRunResultPersister`, updates run status, triggers batch finalization.
5. **Batch finalization**: `BatchFinalizerHostedService` scans every 15 seconds; `BufferedRunResultPersister` also calls `TryFinalizeAsync` on each flush.

### 2.2 Key Configuration

| Component | Setting | Current Value | Notes |
|-----------|---------|---------------|-------|
| Local mode | `LocalSimulationOptions.MaxConcurrentRuns` | 4 | 4 runs in parallel per Web host |
| Worker | MassTransit consumer concurrency | **1** (default) | One message at a time per worker |
| Worker | `PrefetchCount` | Default (CPU-based) | Not explicitly set |
| Worker | `SimulationPersistence.BatchSize` | 50 | Buffered flush threshold |
| Worker | `SimulationPersistence.FlushIntervalMs` | 500 | Time-based flush |
| compose | `WORKER_REPLICAS` | 3 | Number of worker containers |

### 2.3 Concurrency Comparison

| Mode | Parallelism | 10K runs (approx.) |
|------|-------------|--------------------|
| Local (1 Web) | 4 concurrent | ~2,500 runs per “wave” |
| Distributed (1 worker) | 1 concurrent | 10,000 sequential |
| Distributed (3 workers) | 3 concurrent | 3,333 runs per worker, sequential within each |

**Conclusion:** Distributed mode with 3 workers has **less** parallelism than local mode with 1 Web host. This explains why adding workers does not improve throughput.

---

## 3. Root Cause Analysis

### 3.1 Primary: Per-Worker Concurrency = 1

- **Evidence:** `ExecuteSimulationRunConsumer` is a standard MassTransit consumer. No `ConcurrentMessageLimit` or `PrefetchCount` is configured on the receive endpoint.
- **Impact:** Each worker processes one run, completes it, acks, then fetches the next. With 3 workers, only 3 runs execute at any moment.
- **Architecture doc alignment:** `04_Architecture.md` states: “Each worker may run multiple simulations concurrently” and “Concurrency should be configurable: e.g., WorkerOptions.MaxConcurrentRuns.” This is **not implemented**.

### 3.2 Secondary: RabbitMQ Prefetch

- **Evidence:** MassTransit’s default `PrefetchCount` is CPU-based. If low, workers may wait for message delivery after each ack.
- **Impact:** Idle time between runs while waiting for the next message.
- **Mitigation:** Set `PrefetchCount` to a multiple of desired concurrency (e.g., 2×) so messages are ready when workers finish.

### 3.3 Tertiary: Database Contention

- **Evidence:** All workers share the same PostgreSQL instance. `TryClaimAsync`, `BulkMarkCompletedAsync`, and `BufferedRunResultPersister` flushes all hit the DB.
- **Impact:** Under high concurrency, connection pool exhaustion or lock contention could limit throughput. Current low concurrency may mask this.
- **Note:** With increased worker concurrency, DB may become a bottleneck; tuning (connection pool, batch size) may be needed.

### 3.4 Minor: Batch Publish and Finalization

- **Batch publish:** Already implemented via `PublishRunWorkBatchAsync` and `PublishBatch`. No change needed for 10K runs.
- **Batch finalization:** `TryFinalizeAsync` is called on every `BufferedRunResultPersister` flush and by `BatchFinalizerHostedService` every 15 seconds. Some redundancy but not a major bottleneck.

---

## 4. Improvement Areas (Prioritized)

### 4.1 Area 1: Per-Worker Concurrency (High Impact)

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

### 4.2 Area 2: RabbitMQ Prefetch Tuning (High Impact)

**Goal:** Ensure workers receive messages promptly so they are not idle between runs.

**Current:** Default PrefetchCount (often low, e.g., 1–4).

**Recommendation:** Set `PrefetchCount` explicitly to `MaxConcurrentRuns * 2` or similar. This keeps a pipeline of messages ready when workers finish runs.

**Implementation:** Part of Area 1; configure in the same receive endpoint setup.

---

### 4.3 Area 3: Buffered Persistence Tuning (Medium Impact)

**Goal:** Reduce DB round-trips and contention when multiple workers flush concurrently.

**Current:** `BatchSize = 50`, `FlushIntervalMs = 500`. Each worker has its own buffer. Flushes do `AddRangeAsync` + `BulkMarkCompletedAsync` + `SaveChangesAsync`.

**Considerations:**
- Larger `BatchSize` → fewer flushes, larger transactions. May reduce DB load.
- Smaller `BatchSize` → more frequent flushes, smaller transactions. May increase lock contention.
- With higher concurrency, more runs complete per second; buffer fills faster. Current 50 may be reasonable; consider 100–200 if DB becomes bottleneck.

**Recommendation:** Add configuration knobs and document recommended values. Consider increasing `BatchSize` to 100 for distributed mode if DB latency is observed. Defer until Areas 1–2 are implemented and measured.

---

### 4.4 Area 4: Database Connection Pool (Medium Impact)

**Goal:** Avoid connection pool exhaustion when workers run many concurrent simulations.

**Current:** Default Npgsql/EF Core connection pool (typically 100 connections per connection string).

**Consideration:** Each `SimulationRunExecutor` uses scoped DbContext. With 3 workers × 8 concurrent = 24 concurrent executions, each may hold a connection during DB operations. Plus `BatchFinalizerHostedService`, `BufferedRunResultPersister` flushes. Total connections can grow.

**Recommendation:** Monitor connection usage. If needed, increase `Maximum Pool Size` in the connection string or ensure connection lifetime is short. Add to plan as a follow-up if connection errors appear after increasing concurrency.

---

### 4.5 Area 5: Batch Publish Chunking (Low Impact for 10K)

**Goal:** Avoid memory/network pressure when publishing very large batches (e.g., 50K+ runs).

**Current:** `PublishRunWorkBatchAsync` sends all run IDs in one `PublishBatch` call. For 10K runs, this is acceptable.

**Recommendation:** Add optional chunking (e.g., 2,000 runs per chunk) for 50K+ batches. Document in `Batch_Publish_Refactor_Implementation.md`. Low priority for current 10K scenario.

---

### 4.6 Area 6: Observability and Validation (Supporting)

**Goal:** Validate that scaling workers yields measurable speedup and identify remaining bottlenecks.

**Recommendations:**
- Add timing metrics for: batch publish duration, time-to-first-message, runs-per-second per batch.
- Document a simple benchmark procedure: run 10K distributed with 1, 2, 3 workers; record elapsed time and runs/sec.
- Consider a `WORKER_REPLICAS` vs. throughput table in `PERF_NOTES.md`.

---

## 5. Implementation Phases

### Phase 1: Per-Worker Concurrency (Areas 1 + 2)

| Step | Task | Files |
|------|------|-------|
| 1 | Add `WorkerSimulationOptions.MaxConcurrentRuns` and document in appsettings | `BingoSim.Worker/`, `WorkerSimulationOptions` |
| 2 | Configure MassTransit receive endpoint for `ExecuteSimulationRunConsumer` with `PrefetchCount` and `ConcurrentMessageLimit` | `BingoSim.Worker/Program.cs` |
| 3 | Expose via environment variable in compose | `compose.yaml`, `.env.example` |
| 4 | Add unit/integration test that verifies multiple messages can be processed concurrently | `Tests/BingoSim.Worker.UnitTests/` or integration tests |
| 5 | Update `PERF_NOTES.md` with benchmark procedure and expected scaling | `Docs/PERF_NOTES.md` |

**Success criteria:** 3 workers with `MaxConcurrentRuns=4` should complete 10K runs in roughly 1/3 to 1/4 of the time of 1 worker with `MaxConcurrentRuns=1`, with measurable CPU utilization increase.

---

### Phase 2: Persistence and DB Tuning (Areas 3 + 4)

| Step | Task | Files |
|------|------|-------|
| 1 | Add config for `SimulationPersistence.BatchSize` override in Worker (if different from default) | `BingoSim.Worker/appsettings.json` |
| 2 | Document connection pool considerations | `Docs/Distributed Perf/` or `PERF_NOTES.md` |
| 3 | If connection errors occur, add `Maximum Pool Size` to connection string and document | `compose.yaml`, appsettings |

**Success criteria:** No connection pool exhaustion; stable throughput under Phase 1 concurrency.

---

### Phase 3: Observability and Chunking (Areas 5 + 6)

| Step | Task | Files |
|------|------|-------|
| 1 | Add optional chunking to `PublishRunWorkBatchAsync` for 50K+ runs | `MassTransitRunWorkPublisher.cs` |
| 2 | Document scaling benchmarks | `Docs/PERF_NOTES.md` |

---

## 6. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| DB connection exhaustion | Monitor connections; tune pool size; consider reducing `MaxConcurrentRuns` if needed |
| Increased DB lock contention | `TryClaimAsync` uses `ExecuteUpdateAsync` (no long-held locks); `BulkMarkCompletedAsync` is batch. Monitor for deadlocks. |
| Memory pressure per worker | `MaxConcurrentRuns` caps concurrency; snapshot cache is bounded (32 entries). Monitor RSS. |
| Retry/error handling with concurrency | Each message is independent; retries re-publish single run. No change to retry logic. |

---

## 7. References

- `BingoSim.Worker/Program.cs` — MassTransit and consumer registration
- `BingoSim.Worker/Consumers/ExecuteSimulationRunConsumer.cs` — Single-message consumer
- `BingoSim.Web/Services/SimulationRunQueueHostedService.cs` — Local mode concurrency (MaxConcurrentRuns)
- `Docs/04_Architecture.md` — Concurrency model (In-Process Parallelism)
- `Docs/08_Feature_Audit_2025.md` — Worker MaxConcurrentRuns gap
- `Docs/09_Requirements_Review.md` — Worker concurrency note
- `Docs/PERF_NOTES.md` — Baseline and benchmark procedure
- [MassTransit RabbitMQ Configuration](https://masstransit.io/documentation/configuration/transports/rabbitmq) — PrefetchCount, endpoint options
- [MassTransit Discussion #2368](https://github.com/MassTransit/MassTransit/discussions/2368) — Increasing consumers and prefetch
