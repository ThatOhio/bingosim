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

### 1.8 Phase 4H Results (February 3, 2025) - Message Broker Bottleneck Discovered

**Test Configuration:** 500K runs, Release mode, 1 worker vs 3 workers with partitioning

| Configuration | Elapsed Time | Throughput | Scaling Factor |
|---------------|--------------|------------|----------------|
| 1 Worker | 183.8s | 2,720 runs/s | 1.0× (baseline) |
| 3 Workers | 186.5s | 2,681 runs/s | 0.99× (no improvement) |

**Steady-State Metrics:**

| Metric | 1 Worker | 3 Workers (per worker) | 3 Workers (aggregate) |
|--------|----------|------------------------|------------------------|
| Sim time per 10K | 595ms | 220ms avg | 660ms total |
| Per-run sim | 0.06ms | 0.022ms | - |
| Throughput | 3,189 runs/s | ~1,024 runs/s each | 3,072 runs/s |
| Load distribution | 100% | 32%, 33%, 35% | - |

**Critical Findings:**

✅ **Partitioning works perfectly:**
- Even load distribution (32-35% per worker)
- No claim contention (0.13-0.19ms per batch)
- No persist contention (1,600-1,800ms per 10s)
- Each worker processes correct subset of batches

✅ **Workers are highly efficient:**
- 3 workers have 3× better per-run sim time than 1 worker (0.022ms vs 0.06ms)
- Steady-state sim times are excellent (208-241ms per 10K runs)

❌ **No scaling benefit achieved:**
- Aggregate throughput is identical: ~3,000 runs/s regardless of worker count
- 3 workers are slightly slower (0.99× scaling factor)

**Root Cause: Message Broker Throughput Ceiling**

The data reveals a **message broker (RabbitMQ) bottleneck**:

1. **Throughput is constant regardless of worker count:**
   - 1 worker: 31,888 runs per 10s average
   - 3 workers: 30,723 runs per 10s aggregate
   - **Same ceiling: ~3,000-3,200 runs/s**

2. **Workers are not CPU-bound:**
   - 3 workers have 3× better sim performance per run
   - If simulation was the bottleneck, 3 workers would be slower per-run (not faster)

3. **Database is not the bottleneck:**
   - Claim times: <0.2ms per batch (no contention)
   - Persist times: consistent across all workers

4. **Message delivery rate is the constraint:**
   - RabbitMQ delivers ~30K runs per 10s window
   - Adding more workers doesn't increase message delivery rate
   - Workers compete for the same fixed message supply

**Why individual workers are faster:**
- With 3 workers, each processes 1/3 of messages → less work per worker → faster per-message processing
- But aggregate throughput is unchanged because message supply is the bottleneck

**Comparison to Phase 4A-4D:**
- Phase 4A-4D: 100K in 35.9s = 2,786 runs/s (similar throughput)
- Phase 4H: 500K in 183.8s = 2,720 runs/s (confirms consistent ceiling)

**Conclusion:**

Worker partitioning successfully eliminated database contention and enabled perfect load distribution. However, **the message broker cannot deliver messages fast enough** to saturate multiple workers. The current architecture publishes batches sequentially, creating a throughput ceiling of ~3,000 runs/s regardless of worker count.

**Next Steps:** Phase 4I implemented parallel batch publishing. See Phase_4I_Message_Broker_Optimization.md for validation results.

**Investigation:** Phase 4F appeared to have 100× regression, but was actually measured in Debug mode. After switching to Release mode, discovered the real issue.

**Root Cause:** JIT warmup overhead dominates short test runs (100K runs in ~35s)

| Phase | First 10s Sim | Steady State Sim | Per-run (steady) |
|-------|---------------|------------------|------------------|
| 1 worker | 13,925ms + 22,551ms | 1,646ms + 987ms | **0.045ms** ✅ |
| 3 workers (aggregate) | ~42,000ms | ~700ms | **0.022ms** ✅ |

**Key Findings:**

✅ **Simulation performance is excellent:** Steady-state shows 0.02-0.05ms per run (better than Phase 4A-4D target!)

✅ **Partitioning works correctly:** Each worker processes ~1/3 of batches with expected sim times in steady state

❌ **Multi-worker scaling masked by JIT warmup:** For 100K runs (35s total), JIT warmup takes 14-23s per worker in the first two windows

**Analysis:**

**Single worker warmup overhead:**
- Windows 1-2: 36,476ms of simulation time for 44,722 runs
- Windows 3-4: 2,633ms of simulation time for 55,276 runs
- Warmup cost: ~34 seconds

**Three workers warmup overhead (aggregate):**
- Each worker independently JIT compiles simulation code
- Worker 1 window 1: 14,967ms
- Worker 2 window 1: 12,675ms
- Worker 3 window 1: 14,308ms
- Total: ~42 seconds of warmup across all workers (occurs in parallel, ~14s wall time)

**After warmup, workers are fast:**
- Worker 1 steady state: 246ms per 10K runs
- Worker 2 steady state: 304ms per 10K runs
- Worker 3 steady state: 230ms per 10K runs

**Why 3 workers = 34.2s vs 1 worker = 35.5s (only 4% faster):**
- 1 worker: 34s warmup + 1s productive work
- 3 workers: 14s warmup (parallel) + 20s productive work
- Benefit: (35.5 - 34.2) / 35.5 = 3.7% improvement

For warmup to become negligible, need test duration >> 35s. Recommend 500K-1M runs where warmup is <10% of total time.

**Next Steps:** Phase 4H will test with 500K runs to properly measure multi-worker scaling after warmup cost becomes negligible.

**Configuration Changes:** Added WorkerIndex partitioning, workers assigned indices 0-2

| Scenario | 1 Worker | 3 Workers | Scaling Factor | vs Phase 4A-4D |
|----------|----------|-----------|----------------|-----------------|
| 100K runs | 37.2s (~2,688 runs/s) | 37.4s (~2,674 runs/s) | 0.99× | **⚠️ REGRESSION** |

**CRITICAL FINDING - SIMULATION PERFORMANCE COLLAPSED:**

Phase 4A-4D simulation times (per 10s window):
- **sim: ~250-300ms** for 10K+ runs

Phase 4F simulation times (per 10s window):
- Single worker: **sim: 36,408ms** for 15K runs (first window)
- 3 Workers: **sim: 10,000-12,000ms EACH** for 10K runs (steady state)

**The simulation itself is 100× slower!** From ~0.02ms per run → ~1-2ms per run.

**Analysis:**

This is NOT a partitioning problem - this is a **simulation performance regression** introduced in Phase 4F implementation. The partitioning code is likely working correctly (each worker processes ~33K runs), but something changed that made the actual simulation execution dramatically slower.

**Potential causes:**
1. **Snapshot caching disabled/broken:** Notice all workers show "snapshot_load" times and "snapshot_cache_miss" - suggests snapshot is being deserialized repeatedly
2. **New overhead in execution path:** ExecuteSimulationRunBatchConsumer or filter may be calling expensive operations per-run
3. **Configuration issue:** `SimulationDelayMs` might have been inadvertently set to non-zero
4. **Worker identity resolution:** WorkerIndexHostnameResolver or WorkerIndexFilter might be doing expensive operations per-message

**Immediate next steps:**
1. Verify `SimulationDelayMs = 0` in all worker configurations
2. Check snapshot caching - cache should hit on all but first run per batch
3. Profile the simulation execution path to find the regression
4. Consider reverting Phase 4F changes to re-establish baseline

**Configuration Changes:** BatchSize 10→20, PostgreSQL tuning, Persistence BatchSize 50→100, FlushInterval 500ms→1000ms, Connection pool sized to 50.

| Scenario | 1 Worker | 3 Workers | Scaling Factor | Improvement vs Phase 3 |
|----------|----------|-----------|----------------|------------------------|
| 50K runs | 19.3s (~2,591 runs/s) | 18.5s (~2,703 runs/s) | **1.04×** | **2.7× faster (1 worker)** |
| 100K runs | 35.9s (~2,786 runs/s) | 35.7s (~2,801 runs/s) | **1.01×** | **2.9× faster (1 worker)** |

**Key Findings:**

✅ **Massive absolute improvement:** 50K runs dropped from 52.5s to 19.3s (1 worker) — a 2.7× speedup!

✅ **Database contention reduced:** Claim batches reduced from ~1000 to ~500 per 10K runs (50% reduction as expected).

✅ **Throughput ceiling raised:** Peak steady-state throughput increased from ~952 runs/s to ~2,786 runs/s.

❌ **Multi-worker scaling still absent:** 3 workers provide only 1.01-1.04× improvement over 1 worker — essentially identical performance.

**Analysis:**

The Phase 4 optimizations successfully reduced database load by ~50% through larger batches and better PostgreSQL configuration. However, **the fundamental scaling problem persists**: when multiple workers contend for the same database resources (claim/persist operations), they serialize rather than parallelize.

Looking at the 100K run metrics:
- **1 Worker steady state:** 43,174 runs/10s with claim=420ms (2,154 batches), persist=8,604ms
- **3 Workers aggregate (steady state):** ~33,000 runs/10s total, with each worker showing claim times of 18-49ms per 10s

**The database is no longer a time bottleneck, but it's still a concurrency bottleneck.** Workers are getting through batches quickly (claim_avg=0ms means minimal contention), but the overall throughput isn't increasing because:

1. **All workers share one claim queue** - they're all drawing from the same pending runs
2. **Persistence is now fast** but still requires coordination (separate INSERTs from each worker)
3. **The simulation itself is extremely fast** (~250-300ms per 10K runs across all workers) - compute is not the bottleneck

**Next Steps:** Phase 4E (message enrichment) is unlikely to help significantly since GetById isn't visible in the metrics. The path forward is **Phase 4F: Batch Affinity/Partitioning** to give each worker exclusive ranges of work, eliminating inter-worker coordination overhead.

### 1.2 Phase 3 Metrics (50K, Steady State)

| Phase | 1 Worker (per 10s) | 3 Workers (per worker, per 10s) | Aggregate 3 Workers |
|-------|--------------------|---------------------------------|----------------------|
| claim count | ~1000 batches | ~330 batches each | ~990 batches |
| claim total | ~14,000 ms | ~4,800 ms each | ~14,400 ms |
| persist | ~5,000 ms | ~1,600 ms each | ~4,800 ms |
| sim | ~250 ms | ~60 ms each | ~180 ms |

**Key insight:** Aggregate claim count and claim time are nearly identical regardless of worker count. PostgreSQL processes ~1000 claim batches per 10 seconds whether 1 or 3 workers are requesting. The database is the bottleneck, not CPU or message throughput.

### 1.5 Phase 4A-4D Metrics (100K, Steady State)

**1 Worker (steady state - second 10s window):**
- Throughput: 43,174 runs/10s (~4,317 runs/s peak)
- Claim: 420ms for 2,154 batches (batch size 20) = ~0.19ms per batch
- Persist: 8,604ms for 43,139 results
- Sim: 16,061ms for 43,167 runs

**3 Workers (steady state - second 10s window, aggregate):**
- Throughput: ~33,000 runs/10s (~3,300 runs/s peak)
- Claim: 18-49ms per worker (aggregate ~87ms for ~1,666 batches)
- Persist: ~5,000ms aggregate
- Sim: ~844ms aggregate (254+296+294)

**Analysis:** Claim time dropped from 14,000ms to 420ms (97% reduction!), but 3 workers show **lower** aggregate throughput than 1 worker in steady state. This suggests workers are either:
1. Competing for the same messages (RabbitMQ distribution issue)
2. Experiencing coordination overhead that nullifies parallelism benefits
3. Hitting a different bottleneck (network, message broker, etc.)

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

### 3.6 Batch Affinity / Partitioning (High Effort, High Impact) — **CRITICAL FOR MULTI-WORKER SCALING**

**Problem:** Phase 4A-4D proved that database contention is no longer the bottleneck (claim time dropped 97%), yet workers still don't scale. The issue is **work distribution**: all workers consume from the same RabbitMQ queue, competing for the same batches. This creates hidden coordination costs.

**Solution:** Pre-partition work at publish time so each worker gets exclusive run ranges.

#### Option A — Worker-Indexed Partitioning (Recommended)

**Concept:** At publish time, assign each batch to a specific worker index using a consistent hash or round-robin. Worker 1 gets batches 0, 3, 6, 9...; Worker 2 gets 1, 4, 7, 10...; Worker 3 gets 2, 5, 8, 11...

**Implementation:**
1. **Message routing:** Add a `WorkerIndex` header to each batch message: `headers: { "WorkerIndex": batchNumber % workerCount }`
2. **MassTransit routing:** Configure consumer to only consume messages where `WorkerIndex` matches the worker's assigned index (set via environment variable)
3. **Fallback:** If a worker is down, messages can still be consumed by other workers after a delay (or implement a dead-letter queue strategy)

**Benefits:**
- No database changes required
- Workers process disjoint sets of runs → zero claim contention
- RabbitMQ handles message distribution
- Graceful degradation if a worker fails

**Trade-offs:**
- Requires workers to have stable identities (WORKER_INDEX=1, 2, 3)
- Uneven work distribution if batches have varying complexity (unlikely for simulations)
- More complex consumer configuration

#### Option B — Range-Based Partitioning

**Concept:** Divide the entire run range into N equal chunks (where N = number of workers). Worker 1 gets runs 0-33,333, Worker 2 gets 33,334-66,666, Worker 3 gets 66,667-100,000.

**Implementation:**
1. **Publishing:** When creating batches, calculate which worker should handle each run based on RunIndex
2. **Separate queues:** Create N queues (simulation-runs-worker-1, simulation-runs-worker-2, etc.)
3. **Each worker subscribes to its dedicated queue**

**Benefits:**
- Perfect load balance (each worker gets exactly 1/N of the work)
- Completely eliminates cross-worker contention
- Simpler message model (no headers needed)

**Trade-offs:**
- Requires multiple queues
- If a worker goes down, its work is stranded (need dead-letter or timeout rebalancing)
- Less flexible than Option A

#### Option C — Hybrid Approach (Most Robust)

**Concept:** Combine Options A and B. Publish to worker-specific queues using range partitioning, but configure fallback routing so if a worker's queue builds up, messages can overflow to other workers.

**Implementation Phases:**
1. **Phase 4F.1:** Implement Option A (worker-indexed partitioning with headers)
2. **Phase 4F.2:** If successful, optionally migrate to Option B or C for production

**Recommendation:** Start with **Option A** — it's easier to implement and test, requires no queue topology changes, and provides the core benefit (work isolation) while maintaining flexibility.

---

#### Detailed Implementation Plan for Option A

**Files to modify:**

1. **BingoSim.Shared/Messages/ExecuteSimulationRunBatch.cs**
   - Add `public int? WorkerIndex { get; init; }` property

2. **BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs**
   - In `PublishRunWorkBatchAsync`, calculate `workerIndex = batchNumber % expectedWorkerCount`
   - Add to message headers: `context.Headers.Set("WorkerIndex", workerIndex)`
   - `expectedWorkerCount` should come from configuration (default 3)

3. **BingoSim.Worker/Program.cs or Worker configuration**
   - Add `WORKER_INDEX` environment variable (1, 2, or 3)
   - Add `WORKER_COUNT` environment variable (total worker count)

4. **BingoSim.Worker/Consumers/ExecuteSimulationRunBatchConsumerDefinition.cs**
   - In `ConfigureConsumer`, add message filter:
     ```csharp
     configurator.Message<ExecuteSimulationRunBatch>(m => 
         m.UseFilter(new WorkerIndexFilter(workerIndex, workerCount)));
     ```

5. **New file: BingoSim.Worker/Filters/WorkerIndexFilter.cs**
   - Implement `IFilter<ConsumeContext<ExecuteSimulationRunBatch>>`
   - In `Send` method, check if message.WorkerIndex matches this worker's index
   - If yes, pass to next filter; if no, skip (move to next message)

**Environment variables for compose.yaml:**
```yaml
bingosim.worker:
  deploy:
    replicas: 3
  environment:
    - WORKER_INDEX=${WORKER_INDEX} # Set to 0, 1, 2 for each replica
    - WORKER_COUNT=${WORKER_COUNT:-3}
    - DistributedExecution__WorkerCount=${WORKER_COUNT:-3}
```

**Note on Docker Compose replicas:** With `replicas: 3`, all workers get the same env vars. Need to either:
- Run workers separately (not using replicas)
- Use a different orchestrator (Kubernetes with pod indices)
- Or implement a registration system where workers self-assign indices on startup

**Simpler alternative for testing:** Run workers manually with different indices:
```bash
WORKER_INDEX=0 dotnet run --project BingoSim.Worker &
WORKER_INDEX=1 dotnet run --project BingoSim.Worker &
WORKER_INDEX=2 dotnet run --project BingoSim.Worker &
```

---

### 3.7 Stagger Worker Processing (Low Effort, Low Impact)

**Idea:** Reduce contention spikes by staggering when workers start consuming. If all 3 workers claim simultaneously at the start of each 10s window, locks collide. Staggering could smooth the load.

**Implementation:** Add random delay (0–500ms) before first claim in each batch consumer. Or use a simple backoff when claim fails (retry with jitter). Low confidence; measure first.

---

## 4. Recommended Implementation Order

| Phase | Optimization | Effort | Expected Impact | Dependencies | Status |
|-------|--------------|--------|-----------------|--------------|--------|
| 4A | Increase batch size (10 → 20) | Low | Medium | None | ✅ Completed |
| 4B | PostgreSQL tuning | Low | Variable | None | ✅ Completed |
| 4C | Persistence batch size + flush interval | Low | Medium | None | ✅ Completed |
| 4D | Connection pool tuning | Low | Low–Medium | None | ✅ Completed |
| ~~4E~~ | ~~Message enrichment (skip GetById)~~ | Medium | Low | 4A | ⏭️ **Skip** (not visible in metrics) |
| **4F** | **Batch affinity / partitioning** | High | **High** | 4A–4D | 🎯 **Next priority** |

**Updated sequence based on Phase 4A-4D results:** 

Phase 4A-4D achieved a **2.7× absolute speedup** but **no multi-worker scaling**. The database is no longer slow, but workers still don't benefit from parallelism because they're all competing for the same work queue.

**Skip Phase 4E:** Message enrichment would eliminate GetByIdAsync calls, but these don't appear in the performance metrics at all — they're negligible compared to claim/persist/sim times.

**Proceed directly to Phase 4F:** Batch affinity/partitioning is now the critical path. By assigning exclusive run ranges to each worker, we eliminate the hidden coordination costs that prevent scaling.

---

## 5. Success Metrics

### Phase 3 Baseline
| Metric | 1 Worker (50K) | 3 Workers (50K) |
|--------|----------------|-----------------|
| Elapsed time | 52.5s | 55.3s |
| Throughput | ~952 runs/s | ~905 runs/s |
| Scaling factor | 1.0× | 0.95× (regression) |

### Phase 4A-4D Achieved
| Metric | 1 Worker (50K) | 3 Workers (50K) | 1 Worker (100K) | 3 Workers (100K) |
|--------|----------------|-----------------|-----------------|------------------|
| Elapsed time | 19.3s | 18.5s | 35.9s | 35.7s |
| Throughput | ~2,591 runs/s | ~2,703 runs/s | ~2,786 runs/s | ~2,801 runs/s |
| Scaling factor | 1.0× | 1.04× | 1.0× | 1.01× |
| **vs Phase 3** | **2.7× faster** | **2.9× faster** | **2.9× faster** | **3.1× faster** |

✅ **Absolute performance goal exceeded:** 50K runs in 19.3s (target was ≤35s)
❌ **Multi-worker scaling goal not met:** 1.04× actual vs 1.5× target

### Phase 4G Discovered (100K runs, Release mode)
| Metric | 1 Worker | 3 Workers |
|--------|----------|-----------|
| Elapsed time | 35.5s | 34.2s |
| Steady-state sim time | 0.045ms/run | 0.022ms/run |
| JIT warmup overhead | ~34s total | ~14s (parallel) |
| Scaling factor | 1.0× | 1.04× |

**Discovery:** JIT warmup dominates 100K test runs. Partitioning works, but benefit is masked by warmup cost.

### Phase 4H Target (500K runs - warmup amortized)
| Metric | Current (3 workers, 100K) | Target (3 workers, 500K) |
|--------|---------------------------|--------------------------|
| Elapsed time | 34.2s | ≤70s (vs ~105s for 1 worker) |
| Warmup as % of total | ~41% (14s / 34s) | ~13% (14s / 105s) |
| Steady-state throughput | ~3,000 runs/s | ≥4,500 runs/s |
| Scaling factor (3 vs 1 worker) | 1.04× | **≥1.5×** |
| Workers fully utilized | ❌ (warmup dominates) | ✅ (warmup negligible) |

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

### Phase 4A: ✅ Completed (2025-02-03)
- [x] Update DistributedExecution:BatchSize to 20 in appsettings
- [x] Update compose.yaml DISTRIBUTED_BATCH_SIZE default
- [x] Run baseline tests (1 worker, 3 workers)
- [x] Document results (see Phase_4A-4D_Implementation.md)

**Results:** Batch size 20 reduced claim batches by 50% as expected. Did not test batch size 50 — 20 was sufficient.

### Phase 4B: ✅ Completed (2025-02-03)
- [x] Add PostgreSQL tuning parameters to compose.yaml
- [x] Test with synchronous_commit=off
- [x] Test with increased shared_buffers and work_mem
- [x] Document results (README.md Performance Tuning section)

**Results:** PostgreSQL tuning contributed to overall 2.7× speedup. Claim time dropped from ~14,000ms to ~420ms per 10s.

### Phase 4C: ✅ Completed (2025-02-03)
- [x] Update SimulationPersistence:BatchSize to 100
- [x] Update SimulationPersistence:FlushIntervalMs to 1000
- [x] Add environment variable overrides to compose.yaml
- [x] Document results (Phase_4A-4D_Implementation.md, .env.example)

**Results:** Persistence batch size increase contributed to reduced flush frequency and lower overall persist time.

### Phase 4D: ✅ Completed (2025-02-03)
- [x] Add Maximum Pool Size to connection strings
- [x] Update compose.yaml connection strings
- [x] Update appsettings connection strings
- [x] Document results (README.md Performance Tuning section)

**Results:** No connection pool exhaustion errors observed. Pool sizing confirmed adequate for 3 workers.

### Phase 4E: ⏭️ Skipped
- Rationale: GetByIdAsync calls are not visible in performance metrics. Message enrichment would provide negligible benefit compared to Phase 4F.

### Phase 4F: ⚠️ Completed but CRITICAL REGRESSION (2025-02-03)
- [x] Design partitioning strategy (Option A: Worker-Indexed Partitioning)
- [x] Add WorkerIndex to ExecuteSimulationRunBatch message
- [x] Implement worker index assignment in MassTransitRunWorkPublisher
- [x] Add WORKER_INDEX and WORKER_COUNT environment variables
- [x] Implement WorkerIndexFilter for message filtering
- [x] Update ExecuteSimulationRunBatchConsumerDefinition
- [x] Configure workers with stable identities (hostname-derived indices)
- [x] Test with 1 worker, then 3 workers
- [ ] ~~Validate ≥1.5× scaling factor~~ - **FAILED: 0.99× scaling, but due to simulation regression not partitioning**
- [x] Document results

**CRITICAL ISSUE DISCOVERED:** Simulation performance regressed 100× (250ms → 36,000ms per 10K runs). This masks any partitioning benefits. Phase 4G must diagnose and fix this regression before re-testing partitioning.

### Phase 4G: ✅ Completed (2025-02-03)
- [x] Verify SimulationDelayMs = 0 in all configurations
- [x] Check snapshot caching (working correctly)
- [x] Profile ExecuteSimulationRunBatchConsumer execution path
- [x] Identify cause of apparent slowdown (Debug build vs Release build)
- [x] Switch to Release mode for all testing
- [x] Discover real issue: JIT warmup dominates short test runs
- [x] Document findings

**Results:** Simulation performance is excellent (0.02-0.05ms per run in steady state). The "regression" was measurement in Debug mode. In Release mode, discovered JIT warmup overhead masks multi-worker scaling benefits for short tests.

### Phase 4H: ✅ Completed (2025-02-03)
- [x] Run 500K simulations with 1 worker in Release mode
- [x] Run 500K simulations with 3 workers (WORKER_INDEX=0,1,2) in Release mode
- [x] Calculate scaling factor after warmup period excluded
- [x] Measure steady-state aggregate throughput
- [ ] ~~Target: ≥1.5× improvement~~ - **FAILED: 0.99× scaling**
- [x] Identified bottleneck: Message broker throughput ceiling
- [x] Document final results

**Results:** Partitioning works perfectly (even load, no contention), but RabbitMQ delivers messages at ~3,000 runs/s ceiling regardless of worker count. Adding workers doesn't increase throughput because message delivery rate is the constraint.

### Phase 4I: ✅ Implementation Complete - Message Broker Optimization (2025-02-03)
- [x] Profile batch publishing in Web application (instrumentation added)
- [x] Implement parallel batch publishing (Option B: chunked Task.WhenAll)
- [x] Add PublishChunkSize config (default 100) for tuning
- [x] Add publish timing log: `Published X batches (Y runs) in Zms [CHUNKED PARALLEL]`
- [ ] Validate: Run 500K test, check Web logs for publish time
- [ ] Validate: Re-test 3 workers for scaling factor
- [ ] Target: Increase message delivery to ≥10,000 runs/s to saturate 3 workers

**Implementation:** See Phase_4I_Message_Broker_Optimization.md. MassTransitRunWorkPublisher now publishes batches in parallel chunks of 100 (configurable). Validation pending.