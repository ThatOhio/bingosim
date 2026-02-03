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

- **snapshot_load:** Total ms spent loading snapshot from DB. With caching, count = 1 for the entire batch.
- **sim:** Total ms in simulation execution. Count = runs completed.
- **persist:** Total ms for DB writes. With immediate persist (BatchSize=1): per-run delete + add + update. With buffered persist: recorded per flush by BufferedRunResultPersister; total = sum of all flush times, count = runs persisted. The "Buffered persist" line shows flushes, rows, SaveChanges for batched mode.

### Throughput

- **runs/sec:** `runs completed / elapsed seconds`. Primary metric for comparison.
- **Expected ranges (engine-only):** 50–500+ runs/sec on typical hardware. Regression guard uses 50 as minimum.
- **Expected ranges (E2E local):** Lower than engine-only due to DB; varies with hardware.
- **Distributed:** Aggregate throughput should scale with worker count (e.g. 2 workers ≈ 2× single worker).

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
