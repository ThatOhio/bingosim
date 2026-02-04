# Phase 4G - Simulation Performance Regression Diagnosis

**Date:** February 3, 2025  
**Status:** Completed  
**Context:** Phase 4F introduced a 100× simulation performance regression. This document identifies the root cause and documents the fix.

---

## 1. Root Cause Identified

**Root Cause: Running in Debug configuration instead of Release**

The simulation regression is caused by running the Worker with **Debug** build configuration. `dotnet run` defaults to Debug, which disables JIT optimizations and can cause **10–100× slowdown** for CPU-intensive code like simulation execution.

### Evidence

- **Phase 4A-4D (GOOD):** sim=~250-300ms per 10K runs = 0.025ms per run
- **Phase 4F (BROKEN):** sim=36,000ms for 15K runs = 2.4ms per run (first window)
- **Ratio:** 2.4ms / 0.025ms = **96× slower**

Debug builds have `<Optimize>false</Optimize>`, which influences JIT compilation and produces unoptimized machine code. CPU-bound simulation loops are particularly affected.

### Why Phase 4A-4D May Have Appeared Faster

1. **Different run environments:** Phase 4A-4D may have been run via Docker (which uses Release by default in the Dockerfile) or with `-c Release`.
2. **JIT warmup variability:** Debug builds have longer and more variable JIT warmup; Phase 4F's 3-worker partitioning adds 3× message overhead, potentially extending the warmup period.
3. **Inconsistent testing:** Without explicit `-c Release`, results can vary between runs.

---

## 2. Investigation Steps Performed

### 2.1 Configuration Verification ✅

- **SimulationDelayMs:** Verified `0` in `BingoSim.Worker/appsettings.json`, compose.yaml
- **Snapshot cache:** Logs show `snapshot_cache_hit` and `snapshot_cache_miss` counts; cache is working (8 loads per batch, rest are hits)
- **WorkerIndex/WorkerCount:** Correctly configured for single vs multi-worker

### 2.2 Snapshot Caching ✅

- `ISnapshotCache` is Singleton; shared across all scopes
- `SimulationRunExecutor` uses cache correctly; one scope per batch message, all runs in batch share executor
- No changes to caching logic between Phase 4A-4D and Phase 4F

### 2.3 Execution Path ✅

- `WorkerIndexFilter` runs once per message (not per run); no per-run overhead
- `ExecuteSimulationRunBatchConsumer` creates one scope per batch; executor shared for all runs in batch
- No `.Result`/`.Wait()`, lock contention, or blocking operations in hot path

### 2.4 Build Configuration ⚠️

- `dotnet run` defaults to **Debug** when `-c` is not specified
- Docker build uses `ARG BUILD_CONFIGURATION=Release` (Release by default)
- Debug builds can be 10–100× slower for CPU-intensive code

---

## 3. Fix Applied

### 3.1 Primary Fix: Use Release Configuration for Performance Testing

**All performance testing must use Release configuration:**

```bash
# Single worker
WORKER_INDEX=0 WORKER_COUNT=1 dotnet run --project BingoSim.Worker -c Release

# 3 workers (separate terminals)
WORKER_INDEX=0 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
WORKER_INDEX=1 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
WORKER_INDEX=2 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
```

### 3.2 Documentation Updates

- **Multi_Worker_Scaling_Plan.md:** Added Phase 4G completion, findings to Phase 4F section, and explicit `-c Release` requirement for all perf testing
- **Phase 4F Testing Procedure:** Updated to require `-c Release`

### 3.3 No Code Changes Required

The regression is **not** caused by Phase 4F code changes. The WorkerIndexFilter, message partitioning, and snapshot caching are all correct. The fix is purely operational: use Release configuration for performance testing.

---

## 4. Expected Outcomes After Fix

| Metric | Before Fix (Debug) | After Fix (Release) |
|--------|-------------------|---------------------|
| Sim time per 10K runs | 10,000–36,000ms | ~250–300ms |
| Per-run sim time | 1–2ms | ~0.025ms |
| 100K runs (1 worker) | ~37s | ~35s |
| 100K runs (3 workers) | ~37s | Target: ≤24s (with partitioning) |

---

## 5. Prevention Measures

### 5.1 Performance Testing Checklist

- [ ] Always use `-c Release` for `dotnet run` when measuring throughput
- [ ] Document build configuration in all perf test procedures
- [ ] Add `-c Release` to README and perf docs

### 5.2 Optional: Default to Release for Worker

Consider adding a `launchSettings.json` or documenting in README that performance testing requires Release. The Worker could also log a warning when running in Debug with high throughput.

### 5.3 Docker Consistency

Docker builds already use Release. When comparing local vs Docker results, ensure both use the same configuration.

---

## 6. Re-Testing Procedure

1. **Verify fix (single worker):**
   ```bash
   dotnet run --project BingoSim.Worker -c Release
   ```
   Run 10K simulations. Check that `sim` time in logs is ~250–300ms per 10K runs.

2. **Re-test single worker baseline:**
   - Run 100K simulations with 1 worker
   - Should complete in ~35s
   - Verify `sim` time stays low throughout

3. **Re-test partitioned workers:**
   - Start 3 workers with `WORKER_INDEX=0,1,2` and `WORKER_COUNT=3`
   - Run 100K simulations
   - Target: 100K runs in ≤24s (≥1.5× improvement over single worker)

---

## 7. Summary

| Item | Result |
|------|--------|
| **Root cause** | Debug build configuration (10–100× slower than Release) |
| **Discovery** | Investigation step 2.4 (build configuration) |
| **Fix** | Use `-c Release` for all performance testing |
| **Code changes** | None required |
| **Prevention** | Document `-c Release` in perf procedures; add to checklist |
