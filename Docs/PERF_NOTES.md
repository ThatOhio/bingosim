# Performance Notes

Record baseline and post-optimization numbers when running the perf scenario.

## How to Run

```bash
# E2E (DB + execution + persistence)
docker compose up -d postgres
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed
dotnet run --project BingoSim.Seed -- --perf --runs 10000

# Engine-only (no DB)
dotnet test --filter "Category=Perf"
```

## Baseline (Pre-Optimization)

_Run before any optimizations and record here._

| Scenario | Runs | Elapsed (s) | Runs/sec | Phase Totals |
|----------|------|-------------|----------|--------------|
| E2E 10K | 10000 | _TBD_ | _TBD_ | snapshot_load: _ms (10000), sim: _ms (10000), persist: _ms (10000) |
| Engine-only 10K | 10000 | _TBD_ | _TBD_ | N/A |

## Post-Optimization (Step 2)

_Run after implementing Proposals 3, 2, 1, 4._

| Scenario | Runs | Elapsed (s) | Runs/sec | Phase Totals |
|----------|------|-------------|----------|--------------|
| E2E 10K | 10000 | _TBD_ | _TBD_ | snapshot_load: _ms (1), sim: _ms (10000), persist: _ms (10000) |
| Engine-only 10K | 10000 | _TBD_ | _TBD_ | N/A |

## Example Output (E2E)

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
