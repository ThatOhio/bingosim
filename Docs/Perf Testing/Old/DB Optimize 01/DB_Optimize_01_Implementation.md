# DB Write Optimization — Implementation Summary

**Status:** Complete  
**Date:** February 3, 2025  
**Reference:** [DB_Optimize_01_Plan.md](DB_Optimize_01_Plan.md)

---

## Summary

Persistence batching was implemented to reduce SaveChanges calls and improve throughput. A buffered persister accumulates completed run results and flushes in batches (count-based or time-based).

---

## Changes Made

### 1. Repository Extensions

| File | Change |
|------|--------|
| `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` | Added `BulkMarkCompletedAsync(runIds, completedAt)` |
| `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` | Implemented `BulkMarkCompletedAsync` via ExecuteUpdate |
| `BingoSim.Core/Interfaces/ITeamRunResultRepository.cs` | Added `DeleteByRunIdsAsync(runIds)` for retry cleanup |
| `BingoSim.Infrastructure/Persistence/Repositories/TeamRunResultRepository.cs` | Implemented `DeleteByRunIdsAsync` via ExecuteDelete |

### 2. Buffered Persister

| File | Change |
|------|--------|
| `BingoSim.Application/Interfaces/IBufferedRunResultPersister.cs` | **New.** Interface: AddAsync, FlushAsync, GetStats |
| `BingoSim.Application/Interfaces/ISimulationPersistenceConfig.cs` | **New.** BatchSize, FlushIntervalMs abstraction |
| `BingoSim.Infrastructure/Services/BufferedRunResultPersister.cs` | **New.** Singleton buffer, flush on K or T |
| `BingoSim.Infrastructure/Services/SimulationPersistenceConfig.cs` | **New.** Bridges options to config interface |
| `BingoSim.Infrastructure/Services/SimulationPersistenceOptions.cs` | **New.** BatchSize (default 100), FlushIntervalMs (default 500) |

### 3. Executor Changes

| File | Change |
|------|--------|
| `BingoSim.Application/Services/SimulationRunExecutor.cs` | Optional `IBufferedRunResultPersister`, `ISimulationPersistenceConfig`; when BatchSize > 1, add to buffer instead of immediate persist; skip DeleteByRunId on first attempt (AttemptCount == 0) |

### 4. Configuration

| File | Change |
|------|--------|
| `BingoSim.Infrastructure/DependencyInjection.cs` | Register SimulationPersistenceOptions, ISimulationPersistenceConfig, IBufferedRunResultPersister |
| `BingoSim.Seed/appsettings.json` | SimulationPersistence: BatchSize 100, FlushIntervalMs 500 |
| `BingoSim.Web/appsettings.json` | SimulationPersistence: BatchSize 50, FlushIntervalMs 500 |
| `BingoSim.Worker/appsettings.json` | SimulationPersistence: BatchSize 50, FlushIntervalMs 500 |

### 5. Seed Perf Scenario

| File | Change |
|------|--------|
| `BingoSim.Seed/Program.cs` | Inject `IBufferedRunResultPersister`; call `FlushAsync()` at end of perf run; print buffered persist stats (flushes, rows inserted, runs updated, SaveChanges count, elapsed ms) |

---

## Guardrails Preserved

- **TryClaim:** Per-run, unchanged
- **Batch persist:** AddRange + BulkMarkCompleted in same DbContext/scope, single SaveChanges per flush
- **Delete on retry only:** Skip DeleteByRunId when AttemptCount == 0; delete only when isRetry (AttemptCount > 0)
- **Fresh DbContext per flush:** IServiceScopeFactory.CreateScope(); AutoDetectChangesEnabled = false only during flush, restored in finally
- **Flush triggers:** Count >= BatchSize OR time since last flush >= FlushIntervalMs (whichever first)
- **Instrumentation:** Flush count, rows inserted, rows updated, SaveChanges count, elapsed ms

---

## Perf Results

### Before (BatchSize=1, immediate persist)

```
Runs: 2000
Elapsed: 51.3s
Throughput: 39.0 runs/sec
persist: 29191ms total, 2000 invocations (~14.6ms/run)
SaveChanges: ~2000+ (2–3 per run: AddRange + Update, optionally Delete)
```

### After (BatchSize=100, FlushIntervalMs=500)

```
Runs: 10000
Elapsed: 87.8s
Throughput: 114.0 runs/sec
persist: 3094ms total (batched)
Buffered persist: 175 flushes, 20000 rows inserted, 10000 runs updated, 175 SaveChanges, 3094ms total
```

### Comparison (extrapolated)

| Metric | Before (immediate) | After (batched) |
|-------|--------------------|-----------------|
| 10K runs elapsed | ~256s (39 runs/sec) | ~88s (114 runs/sec) |
| SaveChanges for 10K runs | ~20,000+ | 175 |
| Throughput | 39 runs/sec | 114 runs/sec |

**~3× throughput improvement; ~100× fewer SaveChanges.**

---

## How to Run Perf

```bash
# Prerequisites
docker compose up -d postgres
dotnet run --project BingoSim.Seed -- --full-reset --confirm
dotnet run --project BingoSim.Seed

# Perf with synthetic snapshot (10K runs, 120s max)
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --perf-snapshot synthetic --max-duration 120

# Perf with devseed (requires Winter Bingo 2025)
dotnet run --project BingoSim.Seed -- --perf --runs 10000 --event "Winter Bingo 2025" --max-duration 120
```

### Disable batching (baseline)

Set `SimulationPersistence:BatchSize: 1` in `BingoSim.Seed/appsettings.json` to use immediate persist (no buffer).

---

## Determinism and Correctness

- Same seed produces identical results; batching only changes when results are written
- Core and Application unit tests pass
- Distributed workers use the same buffered persister (singleton); each worker flushes independently
- UI progress: GetProgressAsync reads run statuses from DB; flushes update status in batches, so progress updates every flush (every K runs or T ms)
- **Integration tests:** `DistributedBatchIntegrationTests` uses `SimulationPersistence:BatchSize=1` to force immediate persist (no batching) for deterministic test behavior
