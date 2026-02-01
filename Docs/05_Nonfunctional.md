# Non-Functional Requirements

This document defines operational expectations and constraints for v1 of the simulation system. The project is a single-user internal tool optimized for experimentation speed and iteration, not for SaaS-grade guardrails.

---

## Performance & Scale

### Target Scale
- Typical batch sizes may reach the **single-digit millions** of simulation runs.
- Production usage is expected to run with **multiple workers** (at least 4).

### Time-to-Results
- Target: **< 10 minutes** for batches in the single-digit millions *under the full production deployment configuration* (multiple workers).

### Resource Utilization
- Workers are allowed to use all available CPU resources on their hosts.
- Workers may use memory aggressively if it improves throughput, but should remain within a practical ceiling:
  - Target ceiling: **< 16 GB RAM per worker process** under expected workloads.

### UI Responsiveness
- The Web UI should avoid hanging during large runs.
- The UI should provide coarse progress indicators (e.g., progress bars) rather than detailed real-time telemetry.

---

## Reliability, Fault Tolerance, and Retry Policy

### Retry Policy
- Each simulation run should be retried up to **5 attempts** before being marked terminally failed.

### Failure Semantics
- If one or more runs fail terminally, the batch should be surfaced as an **error** (not silently ignored).

### At-Least-Once Delivery & Idempotency
- Messaging is considered **at-least-once** (duplicates are possible).
- The **database is the source of truth** for work state.
- Workers must treat work as idempotent by:
  - claiming work via DB-backed state transitions (locking/compare-and-swap semantics)
  - detecting “late” duplicate work and exiting without side effects

### Worker Independence
- Batches must continue to completion without the Web UI being online.
- Worker failures should not corrupt batch state; runs remain pending or retryable until terminal.

---

## Storage, Retention, and Data Growth

### Retention Policy
- No in-app retention management is required in v1.
- Manual cleanup via database tools/scripts is acceptable.

### Result Aggregation Strategy
- The system should persist:
  - per-run aggregated results (team metrics and timelines), and
  - **batch-level aggregates** to avoid recomputation in the UI.

Batch-level aggregates should include, at minimum:
- winner rate per team
- average total points per team
- average tiles completed per team
- distribution-friendly aggregates as needed for UI (e.g., percentiles later if desired)

---

## Observability

### Logging
- Use structured logging throughout Web and Worker.
- Include correlation identifiers in all relevant logs:
  - `BatchId`
  - `RunId` (when executing a run)
- Default logging levels may follow standard environment defaults:
  - Development: more verbose as needed
  - Production: informational with warnings/errors

### Debug Verbosity
- Dry-run / single-run mode should enable more verbose logging by default to aid debugging.

### Metrics (v1 required)
Provide basic performance and health metrics to detect bottlenecks and failures early.

Minimum metrics for v1:
- runs completed (counter)
- runs failed (counter)
- runs retried (counter)
- runs currently running (gauge)
- batch duration (timer)
- throughput estimate (runs/sec) at worker and/or batch level

Metrics output may be:
- structured logs, or
- a simple in-app endpoint/page,
as long as it is easy to inspect during performance tuning.

---

## Security & Secrets

### Network / Access
- The system is internal-only.
- No authentication is required for v1.
- Transport security (TLS) is not required inside the LAN.

### Secrets Management
- `.env`-based configuration is acceptable.
- Secrets should not be committed to source control.

---

## Compatibility & Deployment

- Linux x86_64 is the only required platform (no ARM support required).
- Workers may run across multiple hosts in the same network and coordinate via shared Postgres and RabbitMQ.
- Deployment and upgrades may use simple Docker Compose patterns; rolling updates are not required for v1.

---

## Reproducibility & Auditability

### RNG Seed Recording
- Seeds may be user-provided in v1 for reproducibility/testing; seeds must be recorded per run.

### Snapshot Reproducibility
- EventSnapshot JSON captured at batch start is the primary mechanism for reproducibility.
- Do not introduce additional versioning models in v1 solely for audit purposes.
- Where existing data is already available (e.g., strategy key + params, snapshot JSON), include it in results.

---

## Explicit Non-Requirements (v1)

- No accessibility/ADA requirements
- No export (CSV/JSON) features
- No user safety rails for destructive actions (“purely on me”)
- No strict determinism across workers or ordering guarantees
- No UI-heavy validation requirements

