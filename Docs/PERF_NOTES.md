# Performance Notes

Record baseline and post-optimization numbers when running the perf scenario.

---

## How to Run

### Local E2E (DB + execution + persistence)
```bash
docker compose up -d postgres
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed
dotnet run --project BingoSim.Seed -- --perf --runs 10000
```

### Distributed E2E (2 workers)
```bash
# Start full stack with 2 workers
docker compose up -d postgres rabbitmq
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed

# Terminal 1: Web
dotnet run --project BingoSim.Web

# Terminal 2 & 3: 2 workers (or: docker compose up -d --scale bingosim.worker=2)
dotnet run --project BingoSim.Worker
# (second terminal) dotnet run --project BingoSim.Worker
```

Then in the UI: Run Simulations → select "Winter Bingo 2025" → Distributed → 10000 runs → Start batch. Observe results.

### Phase 2 Benchmark Expectations

After Phase 2 (Shared Snapshot Cache + Claim Optimization):

- **snapshot_load count** should drop from ~1000 per 10s to ~1 per batch (e.g. 1 per 10s for a 10K batch).
- **snapshot_cache_hit** and **snapshot_cache_miss** confirm cache reuse: hit count >> miss count.
- **claim_avg** appears in worker throughput logs; claim total/count unchanged but DB contention may decrease.
- **Distributed throughput** with 3 workers should improve vs. Phase 1 baseline (~990 runs/10s); target: 2–3× with shared cache reducing DB load.

### Distributed Scaling Benchmark (1 vs 3 workers)

Validates that adding workers yields measurable throughput increase. Run 10K distributed with 1 worker, then 3 workers; compare elapsed time and runs/sec.

```bash
# 1. Start stack (postgres, rabbitmq, web)
docker compose up -d postgres rabbitmq bingosim.web

# 2. Seed and reset
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed

# 3. Run with 1 worker
WORKER_REPLICAS=1 docker compose up -d bingosim.worker
# In UI: Run Simulations → Distributed → 10000 runs → Start batch
# Record: elapsed time, runs/sec from Results page

# 4. Stop workers, run with 3 workers
docker compose stop bingosim.worker
WORKER_REPLICAS=3 docker compose up -d bingosim.worker
# Reset batch or use new event; run 10K again
# Record: elapsed time, runs/sec

# 5. Compare: 3 workers should complete in ~1/3 the time of 1 worker (with MaxConcurrentRuns=4)
```

**Expected:** With `WORKER_MAX_CONCURRENT_RUNS=4` (default), 3 workers × 4 concurrent = 12 in-flight runs. CPU usage should rise accordingly.

**Connection pool:** If you see repeated transient DB failures (e.g. "connection pool exhausted"), increase `Maximum Pool Size` in the connection string or reduce `MaxConcurrentRuns`. Do not blindly raise pool limits without evidence. See [Distributed Perf Plan](Distributed%20Perf/Distributed_Performance_Plan.md) Area 4.

### Engine-only (no DB)
```bash
dotnet test --filter "Category=Perf"
```

To print a summary (elapsed, throughput) for the 10K run, set `BINGOSIM_PERF_OUTPUT=1`:
```bash
BINGOSIM_PERF_OUTPUT=1 dotnet test --filter "Category=Perf"
```

### Regression guard (lightweight, no DB)
```bash
dotnet run --project BingoSim.Seed -- --perf-regression --runs 1000
# Optional: --min-runs-per-sec 50 (default)
# Exits 1 if throughput below threshold
```

---

## Command Knobs

| Command | Option | Default | Description |
|---------|--------|---------|-------------|
| `--perf` | `--runs` | 10000 | Number of runs |
| `--perf` | `--event` | "Winter Bingo 2025" | Event name |
| `--perf` | `--seed` | "perf-baseline-2025" | Batch seed |
| `--perf` | `--max-duration` | 0 (no limit) | Stop after N seconds, report partial results |
| `--perf` | `--perf-snapshot` | devseed | `devseed` = use event snapshot from DB; `synthetic` = use PerfScenarioSnapshot (unblocks E2E when dev seed hangs) |
| `--perf` | `--perf-verbose` | off | Log progress every 1000 iterations (simTime, queue count, online players) |
| `--perf` | `--perf-dump-snapshot` | - | Write loaded snapshot JSON to file. Use `--perf-dump-snapshot` for default `perf-snapshot.json`, or `--perf-dump-snapshot path.json` for custom path. Use `{0}` in path for batchId (e.g. `snapshot-{0}.json`). |
| `--perf-regression` | `--runs` | 1000 | Number of runs |
| `--perf-regression` | `--min-runs-per-sec` | 50 | Fail if below this threshold |

### Synthetic Snapshot Mode (Unblocker)

When the dev-seed "Winter Bingo 2025" event causes the simulation to hang or run extremely slowly, use `--perf-snapshot synthetic` to run E2E with the same minimal snapshot as the engine-only regression guard:

```bash
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120
```

This still creates a batch and runs through the full persist path; only the snapshot fed into execution is replaced with PerfScenarioSnapshot (always-online players, 1 activity, 4 tiles).

---

## Interpreting Metrics

### Phase totals (E2E)

```
Phase totals (ms total, count):
  persist: 4523ms total, 10000 invocations
  sim: 38200ms total, 10000 invocations
  snapshot_load: 120ms total, 1 invocations
```

- **snapshot_load:** Total ms spent loading snapshot from DB. **Phase 2:** With shared cache, count ≈ 1 per batch (not per run). Represents DB work only; cache hits are excluded.
- **snapshot_cache_hit:** (Phase 2) Count of runs that reused a cached snapshot. High = cache effective.
- **snapshot_cache_miss:** (Phase 2) Count of runs that loaded from DB. Should ≈ 1 per batch.
- **sim:** Total ms in simulation execution. Count = runs completed.
- **persist:** Total ms for DB writes. With immediate persist (BatchSize=1): per-run delete + add + update. With buffered persist: recorded per flush by BufferedRunResultPersister; total = sum of all flush times, count = runs persisted. The "Buffered persist" line shows flushes, rows, SaveChanges for batched mode.
- **claim:** Total ms and count for atomic run claiming (Pending → Running). **Phase 2:** `claim_avg` (ms) emitted per 10s window in worker throughput logs.

### Throughput

- **runs/sec:** `runs completed / elapsed seconds`. Primary metric for comparison.
- **Expected ranges (engine-only):** 50–500+ runs/sec on typical hardware. Regression guard uses 50 as minimum.
- **Expected ranges (E2E local):** Lower than engine-only due to DB; varies with hardware.
- **Distributed:** Aggregate throughput should scale with worker count × MaxConcurrentRuns (e.g. 3 workers × 4 concurrent = 12 in-flight runs). See [Distributed Scaling Benchmark](#distributed-scaling-benchmark-1-vs-3-workers).

### [TIMED OUT]

When `--max-duration` is reached, output shows partial runs and `[TIMED OUT]`. Use runs/sec from partial data for diagnosis.

---

## Baseline and Results

### Pre-Optimization

| Scenario | Runs | Elapsed (s) | Runs/sec | Phase Totals |
|----------|------|-------------|----------|--------------|
| E2E 10K | 10000 | 768.8 | 13.0 | snapshot_load: 35ms (10000), sim: 104ms (10000), persist: 460020ms (10000) |
| Engine-only 10K | 10000 | 1.3 | 7766.9 | N/A |

**2025-02-03 run** (command: `dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --seed "perf-baseline-2025"`): persist dominates (~460s of 768s); sim is negligible (104ms).

### Post-Optimization (Step 2)

| Scenario | Runs | Elapsed (s) | Runs/sec | Phase Totals |
|----------|------|-------------|----------|--------------|
| E2E 10K | 10000 | _TBD_ | _TBD_ | snapshot_load: _ms (1), sim: _ms (10000), persist: _ms (10000) |
| Engine-only 10K | 10000 | _TBD_ | _TBD_ | N/A |

### Post-Optimization (DB Optimize 01 — Batched Persistence)

| Scenario | Runs | Elapsed (s) | Runs/sec | SaveChanges | Buffered Persist |
|----------|------|-------------|----------|-------------|-------------------|
| E2E 10K (batched) | 10000 | 87.8 | 114.0 | 175 | 175 flushes, 20K rows, 3094ms |
| E2E 2K (immediate) | 2000 | 51.3 | 39.0 | ~4000 | N/A |

**Command:** `dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120`

**Improvement:** ~3× throughput (39 → 114 runs/sec); ~57× fewer SaveChanges (10000 → 175).

### Post-Optimization (Round 2 — Snapshot Cache, Finalization Check, AsNoTracking, Local Perf Path)

| Scenario | Runs | Elapsed (s) | Runs/sec | SaveChanges | Buffered Persist |
|----------|------|-------------|---------|-------------|-------------------|
| E2E 10K (batched) | 10000 | 3.9 | 2540.5 | 101 | 101 flushes, 20K rows, 2787ms |

**Command:** `dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120`

**Phase totals:** persist: 2787ms (10000), sim: 112ms (10000). No snapshot_load phase (pre-loaded in perf loop).

**Improvement:** ~22× throughput vs DB Optimize 01 (114 → 2540 runs/sec); ~195× vs pre-optimization (13 → 2540 runs/sec).

### Example Output (E2E)

```
Perf scenario: 10000 runs, event 'Winter Bingo 2025', seed 'perf-baseline-2025'

=== Perf Summary ===
Runs completed: 10000 / 10000
Elapsed: 45.2s
Throughput: 221.2 runs/sec

Phase totals (ms total, count):
  persist: 4523ms total, 10000 invocations
  sim: 38200ms total, 10000 invocations
  snapshot_load: 120ms total, 1 invocations
```

Note: With snapshot caching, `snapshot_load` shows 1 invocation for the entire batch.

---

## Stuck Runs (Buffered Persistence)

### Symptom

Distributed batch shows "Running" for the last N runs indefinitely (e.g. 81 completed, 19 running, 0 pending). Worker logs show "simulation completed" for those runs.

### Root Cause

`BufferedRunResultPersister` only flushes when `AddAsync` is called and either (a) buffer count ≥ BatchSize, or (b) FlushIntervalMs has elapsed since last flush. When the **last runs of a batch** complete in quick succession, the buffer may have &lt; BatchSize items and no further `AddAsync` calls occur, so the buffer never flushes. Runs stay in "Running" in the DB forever.

### Fix (2025-02-03)

- **BufferedPersisterFlushHostedService** runs every FlushIntervalMs and calls `FlushAsync`, ensuring the buffer is flushed even when no new runs complete.
- **Recovery:** `dotnet run --project BingoSim.Seed -- --recover-batch <batchId>` resets stuck runs to Pending and re-publishes them to RabbitMQ.
