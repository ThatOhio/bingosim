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

## Known Follow-ups / Open Questions

- Batch claiming (optional Phase 3) could further reduce claim round-trips if claim remains a bottleneck.
- Monitor cache hit rate; if low, investigate batch distribution or TTL.
- If 3 workers still do not scale, consider connection pool tuning or DB locality.
