# Multi-Worker Scaling Plan

**Date:** February 3, 2025  
**Status:** Plan  
**Context:** Phase 3 achieved massive overall improvement (100s → 11s for 10K runs) via batch claiming, but 3 workers still do not outperform 1 worker. 50K runs: 1 worker 52.5s vs 3 workers 55.3s – adding workers can make throughput worse.

---

## 1. Phase 3 Results Analysis

### 1.1 Observed Throughput

| Scenario | 1 Worker | 3 Workers | Scaling Factor |
|----------|----------|-----------|----------------|
| 10K runs | 11.6s (~860 runs/s) | 11.0s (~910 runs/s) | ~1.05× (marginal) |
| 50K runs | 52.5s (~952 runs/s) | 55.3s (~905 runs/s) | ~0.95× (regression) |

**Conclusion:** No meaningful scaling benefit. At 50K, 3 workers are slightly slower than 1 worker.

### 1.2 Phase 3 Metrics (50K, Steady State)

| Phase | 1 Worker (per 10s) | 3 Workers (per worker, per 10s) | Aggregate 3 Workers |
|-------|--------------------|---------------------------------|----------------------|
| claim count | ~1000 batches | ~330 batches each | ~990 batches |
| claim total | ~14,000 ms | ~4,800 ms each | ~14,400 ms |
| persist | ~5,000 ms | ~1,600 ms each | ~4,800 ms |
| sim | ~250 ms | ~60 ms each | ~180 ms |

**Key insight:** Aggregate claim count and claim time are nearly identical regardless of worker count. PostgreSQL processes ~1000 claim batches per 10 seconds whether 1 or 3 workers are requesting. The database is the bottleneck, not CPU or message throughput.

### 1.3 Root Cause: Shared Database Contention

All workers share a single PostgreSQL instance. They contend on:

1. **ClaimBatchAsync** – `UPDATE SimulationRuns SET Status = 'Running' WHERE Id = ANY(...) AND Status = 'Pending'`
   - Row-level locks; concurrent workers serialize on the same table
   - ~14 ms per batch regardless of worker count

2. **BufferedRunResultPersister** – `INSERT INTO TeamRunResults`, `BulkMarkCompletedAsync` on SimulationRuns
   - Each worker flushes independently; all hit the same tables
   - 3 workers × more frequent flushes = more contention

3. **GetByIdAsync** – 1 read per run (10K reads for 10K runs)
   - Less dominant than claim/persist but adds load

4. **Connection pool** – More workers = more concurrent connections
   - Potential exhaustion or connection wait time

**Why 3 workers can be slower:** Increased lock contention. When Worker A holds a lock during `ClaimBatchAsync`, Workers B and C block. The DB serializes work; adding workers adds queueing without adding capacity.

---

## 2. Goal

Achieve **≥1.5× aggregate throughput** when scaling from 1 to 3 workers (e.g., 50K runs in ≤35s with 3 workers vs 52s with 1 worker).

---

## 3. Proposed Optimizations (Prioritized)

### 3.1 Increase Batch Size (Low Effort, Medium Impact)

**Current:** `DistributedExecution:BatchSize = 10` → ~1000 claim batches per 10K runs.

**Proposal:** Increase to 20 or 50. Fewer claim round-trips = less DB load.

| Batch Size | Claim batches per 10K | Expected reduction |
|------------|------------------------|---------------------|
| 10 (current) | 1000 | baseline |
| 20 | 500 | ~50% fewer claim round-trips |
| 50 | 200 | ~80% fewer claim round-trips |

**Implementation:** Config change only. Validate with 20, then 50. Monitor for worker imbalance (larger batches = fewer messages = potential skew).

**Risk:** Larger batches increase latency per message; if one worker gets many large batches, others may idle. Tune based on results.

---

### 3.2 Message Enrichment – Skip GetByIdAsync (Medium Effort, Medium Impact)

**Current:** Executor calls `GetByIdAsync(runId)` for every run to fetch BatchId, Seed, Status. 10K runs = 10K SELECTs.

**Proposal:** Enrich `ExecuteSimulationRunBatch` with per-run payload: `{ RunId, BatchId, Seed, RunIndex }`. Executor uses message data when available; skips GetByIdAsync for batch path.

**Impact:** Eliminates 10K reads per 10K runs. Reduces DB load by ~10–15% (GetById is fast but not free).

**Implementation:** See [Phase_3_Claim_Strategy.md](../Distributed%20Perf/Phase_3_Claim_Strategy.md) Option C / D. Add `RunWorkItem` to batch message; Web populates from runs at publish; Worker passes to executor.

**Risk:** Retry path must include enrichment. Staleness (AttemptCount) – handle retries by re-fetching or including AttemptCount in message.

---

### 3.3 PostgreSQL Tuning (Low Effort, Variable Impact)

**Proposal:** Tune PostgreSQL for write-heavy workload when running in Docker or local dev.

| Setting | Default | Suggested | Effect |
|---------|---------|-----------|--------|
| `synchronous_commit` | on | off | Reduces fsync latency; small crash window |
| `shared_buffers` | 128MB | 256MB+ | More cache, fewer disk reads |
| `work_mem` | 4MB | 16MB | Better for sorts/joins in aggregates |
| `max_connections` | 100 | 200 | Support more concurrent connections |

**Implementation:** Add to `compose.yaml` postgres service via environment variables or command line args. Document in PERF_NOTES for dev/test. Production: coordinate with DBA.

**Example for compose.yaml:**
```yaml
postgres:
  command: 
    - "postgres"
    - "-c"
    - "synchronous_commit=off"
    - "-c"
    - "shared_buffers=256MB"
    - "-c"
    - "work_mem=16MB"
    - "-c"
    - "max_connections=200"
```

**Risk:** `synchronous_commit=off` – up to a few seconds of data loss on crash. Acceptable for simulation batches (re-runnable).

---

### 3.4 Persistence Batch Size and Flush Interval (Low Effort, Medium Impact)

**Current:** `SimulationPersistence.BatchSize = 50`, `FlushIntervalMs = 500`. Each worker flushes when buffer hits 50 or every 500ms.

**Proposal:** For distributed mode, increase BatchSize to 100–200 and FlushIntervalMs to 1000. Fewer flushes = fewer DB round-trips, larger transactions.

**Trade-off:** Larger batches = more memory per worker, longer delay before first persist. For 10K runs, 100 vs 50 halves flush count.

**Implementation:** Add `SimulationPersistence:BatchSize` and `SimulationPersistence:FlushIntervalMs` overrides to compose.yaml environment variables for bingosim.worker. Update appsettings.json defaults or document recommended values.

**Example for compose.yaml:**
```yaml
bingosim.worker:
  environment:
    - SimulationPersistence__BatchSize=${PERSISTENCE_BATCH_SIZE:-100}
    - SimulationPersistence__FlushIntervalMs=${PERSISTENCE_FLUSH_INTERVAL_MS:-1000}
```

---

### 3.5 Connection Pool Tuning (Low Effort, Low–Medium Impact)

**Current:** Default Npgsql pool (typically 100 connections per connection string).

**Proposal:** Ensure pool is sized for 3 workers × 4 concurrent batches × 2 (claim + persist) ≈ 24+ connections. Add `Maximum Pool Size=50` to connection string if not set. Monitor for "connection pool exhausted" errors.

**Implementation:** Update connection strings in appsettings.json and compose.yaml to include pool size parameter. Document in PERF_NOTES.

**Example connection string:**
```
Host=postgres;Port=5432;Database=bingosim;Username=postgres;Password=postgres;Timeout=30;Maximum Pool Size=50
```

---

### 3.6 Batch Affinity / Partitioning (High Effort, High Impact)

**Idea:** Reduce lock contention by ensuring workers rarely update the same rows concurrently.

**Option A – Batch-level partitioning:** At publish time, partition run IDs by a hash (e.g., `runIndex % workerCount`). Each worker receives batches that tend to touch different rows. Doesn't eliminate contention (SimulationRuns is one table) but could reduce hot spots.

**Option B – Exclusive batch assignment:** Assign entire batches to workers. Worker 1 gets runs 0–3333, Worker 2 gets 3334–6666, etc. Each worker's claim batches touch a contiguous range. Less random lock contention.

**Implementation:** Would require changing how Web chunks and publishes. Instead of round-robin chunks, partition by run index. Complex; defer until simpler options exhausted.

---

### 3.7 Stagger Worker Processing (Low Effort, Low Impact)

**Idea:** Reduce contention spikes by staggering when workers start consuming. If all 3 workers claim simultaneously at the start of each 10s window, locks collide. Staggering could smooth the load.

**Implementation:** Add random delay (0–500ms) before first claim in each batch consumer. Or use a simple backoff when claim fails (retry with jitter). Low confidence; measure first.

---

## 4. Recommended Implementation Order

| Phase | Optimization | Effort | Expected Impact | Dependencies |
|-------|--------------|--------|-----------------|--------------|
| 4A | Increase batch size (20 → 50) | Low | Medium | None |
| 4B | PostgreSQL tuning | Low | Variable | None |
| 4C | Persistence batch size + flush interval | Low | Medium | None |
| 4D | Connection pool tuning | Low | Low–Medium | None |
| 4E | Message enrichment (skip GetById) | Medium | Medium | 4A optional |
| 4F | Batch affinity (if 4A–4E insufficient) | High | High | 4A–4E |

**Suggested sequence:** 4A + 4B + 4C + 4D first (all low effort, config-only changes). Measure. If no scaling improvement, add 4E. If still no scaling, consider 4F or document that single-worker is optimal for DB-bound workload.

---

## 5. Success Metrics

| Metric | Current (3 workers, 50K) | Target |
|--------|---------------------------|--------|
| Elapsed time | 55.3s | ≤40s |
| Aggregate runs/10s | ~900 | ≥1250 |
| Claim count per 10s | ~990 | ≤500 (with larger batches) |
| 3 workers vs 1 worker | 0.95× | ≥1.5× |

---

## 6. Validation Procedure

1. **Baseline:** Run 50K distributed with 1 worker. Record elapsed, runs/10s, claim count.
2. **Apply 4A:** Set `DistributedExecution:BatchSize=20`. Run 50K with 1 worker, then 3 workers. Compare.
3. **Apply 4A+4B+4C+4D:** Batch size 50, PG tuning, persist BatchSize 100, pool 50. Re-run.
4. **Apply 4E if needed:** Message enrichment. Re-run.
5. **Compare:** 3 workers should complete 50K in ≤35s (vs 52s baseline).

---

## 7. Open Questions

- **Docker PostgreSQL:** Is the compose Postgres image using default config? Adding a custom `postgresql.conf` or env vars could help.
- **Network latency:** If DB runs in Docker and workers on host (or vice versa), network RTT adds up. Same-host placement?
- **Lock monitoring:** Enable `log_lock_waits` in PostgreSQL to confirm lock contention.

---

## 8. References

- [Phase 3 Test Results](../Distributed%20Perf/Phase%203%20Test%20Results.md) – Source data for this analysis
- [Phase 3 Implementation Summary](../Distributed%20Perf/Phase_3_Implementation_Summary.md) – Current batch claiming implementation
- [Phase 3 Claim Strategy](../Distributed%20Perf/Phase_3_Claim_Strategy.md) – Message enrichment (Option C)
- [Distributed Performance Plan](../Distributed%20Perf/Distributed_Performance_Plan.md) – Original phased plan
- [PERF_NOTES.md](../PERF_NOTES.md) – Benchmark procedure

---

## 9. Implementation Progress

### Phase 4A: Status - Completed (2025-02-03)
- [x] Update DistributedExecution:BatchSize to 20 in appsettings
- [x] Update compose.yaml DISTRIBUTED_BATCH_SIZE default
- [ ] Run baseline tests (1 worker, 3 workers)
- [ ] Increase to BatchSize 50 if 20 shows improvement
- [x] Document results (see Phase_4A-4D_Implementation.md)

### Phase 4B: Status - Completed (2025-02-03)
- [x] Add PostgreSQL tuning parameters to compose.yaml
- [ ] Test with synchronous_commit=off
- [ ] Test with increased shared_buffers and work_mem
- [x] Document results (README.md Performance Tuning section)

### Phase 4C: Status - Completed (2025-02-03)
- [x] Update SimulationPersistence:BatchSize to 100
- [x] Update SimulationPersistence:FlushIntervalMs to 1000
- [x] Add environment variable overrides to compose.yaml
- [x] Document results (Phase_4A-4D_Implementation.md, .env.example)

### Phase 4D: Status - Completed (2025-02-03)
- [x] Add Maximum Pool Size to connection strings
- [x] Update compose.yaml connection strings
- [x] Update appsettings connection strings
- [x] Document results (README.md Performance Tuning section)

### Phase 4E: Status - Not Started
- [ ] Design RunWorkItem payload structure
- [ ] Update ExecuteSimulationRunBatch message
- [ ] Modify MassTransitRunWorkPublisher to enrich messages
- [ ] Update ExecuteSimulationRunBatchConsumer to use enriched data
- [ ] Handle retry scenarios
- [ ] Document results

### Phase 4F: Status - Not Started
- [ ] Design partitioning strategy
- [ ] Implement batch assignment logic
- [ ] Test and validate
- [ ] Document results
