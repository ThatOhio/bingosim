# Phase 2 Implementation Summary

**Date:** February 3, 2025  
**Status:** Implemented  
**Phase:** Shared Snapshot Cache + Claim Optimization

---

## What Was Implemented

### A) Shared Snapshot Cache

1. **ISnapshotCache interface** (`BingoSim.Application/Interfaces/ISnapshotCache.cs`)
   - `Get(Guid batchId)` — returns cached `EventSnapshotDto` or null
   - `Set(Guid batchId, EventSnapshotDto snapshot)` — stores materialized snapshot

2. **SharedSnapshotCache implementation** (`BingoSim.Infrastructure/Simulation/SharedSnapshotCache.cs`)
   - Process-local, singleton, concurrency-safe
   - Keyed by `BatchId`
   - Bounded: max 32 entries
   - TTL eviction: 15 minutes
   - Stores only `EventSnapshotDto` (no EF entities or DbContext references)

3. **SimulationRunExecutor integration**
   - Injects `ISnapshotCache` (singleton)
   - Before DB load: checks cache
   - Cache hit: uses cached snapshot, records `snapshot_cache_hit`
   - Cache miss: loads from DB, materializes, inserts into cache, records `snapshot_cache_miss` and `snapshot_load` (DB timing only)

4. **Metrics**
   - `snapshot_cache_hit` — count of runs that reused cached snapshot
   - `snapshot_cache_miss` — count of runs that loaded from DB
   - `snapshot_load` — ms total and count for DB loads only (cache hits excluded)

### B) Claim Optimization

1. **Index verification**
   - `TryClaimAsync` uses `WHERE Id = @runId AND Status = @pending`
   - Primary key on `Id` provides O(1) lookup; no additional index needed
   - No migration added

2. **Observability**
   - `claim_avg` — average claim latency per 10s window (computed from claim total/count)
   - `[ClaimDbError]` — tag on log when `TryClaimAsync` throws (DB connection/timeout)

3. **Batch claiming**
   - Skipped per constraints (would require architectural churn; message-per-run model unchanged)

---

## Why These Choices

- **Singleton cache:** Workers are long-lived; a shared cache across concurrent message handlers eliminates redundant snapshot loads per batch.
- **BatchId key:** Snapshot is identical for all runs in a batch; one load per batch is sufficient.
- **15 min TTL:** Batches typically complete in minutes; 15 min avoids stale data while allowing cache reuse across overlapping batches.
- **32 entry cap:** Limits memory; typical deployment has few concurrent batches.
- **No EF entities in cache:** Avoids DbContext tracking issues and keeps cache process-local and serialization-free.

---

## Metrics to Validate Success

| Metric | Phase 1 Baseline | Phase 2 Expected |
|--------|------------------|------------------|
| `snapshot_load` count | ~991 per 10s (1 worker) | ~1 per batch (≈1 per 10s for 10K batch) |
| `snapshot_cache_hit` | N/A | High (e.g. 990+ per 10s) |
| `snapshot_cache_miss` | N/A | Low (e.g. 1 per batch) |
| `claim` total/count | ~14 ms avg | Same or lower |
| Distributed throughput (3 workers) | ~990 runs/10s aggregate | Higher (e.g. 2–3×) |

---

## Post-Implementation Results (Phase 2 Test Results)

**Snapshot cache:** Working as designed. `snapshot_cache_hit` ≈ run count; `snapshot_load` effectively 1 per batch.

**Throughput:** Unchanged vs Phase 1. 1 worker ~1000 runs/10s; 3 workers ~990 aggregate. Workers still splitting the same load.

**Root cause:** Snapshot load was &lt;1% of total time (~44ms/10s). **Claim dominates** (~14,000 ms/10s). Eliminating snapshot_load had negligible effect. See [Phase 2 Test Results](Phase%202%20Test%20Results.md) and [Distributed_Performance_Plan.md](Distributed_Performance_Plan.md) Phase 3 for next steps (batch claiming, message enrichment).

---

## Known Follow-ups / Open Questions

- **Batch claiming** (Phase 3) — Critical. Must reduce claim round-trips to enable scaling.
- **Message enrichment** (BatchId + Seed) — Saves GetByIdAsync round-trip per run.
- Monitor cache hit rate; if low, investigate batch distribution or TTL.
