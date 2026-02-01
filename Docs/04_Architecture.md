# Architecture

This document describes the high-level architecture of the BingoSim system: component responsibilities, project boundaries, runtime topology, and the end-to-end execution flow from UI configuration to worker execution and persisted results.

The system follows Clean Architecture: the Core domain is dependency-free, Application orchestrates use cases, Infrastructure implements persistence/messaging, and Presentation/Worker run at the edges.

---

## Goals

- Enable configuration of events, players, activities, and strategies via a server-rendered Web UI
- Run large batches of simulations via worker processes without requiring the UI to remain running
- Support thousands to millions of simulation runs with parallel execution
- Persist reproducible run context via snapshots and store aggregated results for analysis
- Keep the domain model clean and infrastructure-agnostic

---

## Non-Goals (v1)

- Multi-user, authentication, permissions, or tenanting
- Strict UI validation or guardrails (power-user workflow)
- Export (CSV/JSON) and cross-batch comparison tooling
- Cloud-native requirements (managed services, multi-region, zero-downtime)
- Deterministic execution across workers or strict ordering of work

---

## Project Structure & Responsibilities

### BingoSim.Core (Domain)
- Entities, value objects, domain rules, invariants
- No EF Core, no MassTransit, no third-party infrastructure concerns
- Interfaces for external dependencies (repositories, job queue abstractions if needed)
- Domain exceptions for invariant violations

### BingoSim.Application (Use Cases)
- Orchestrates domain logic and coordinates workflows
- Defines commands/queries and DTOs for the Web UI
- Defines strategy contracts and strategy implementations (application-level)
- Builds simulation snapshots and schedules simulation batches

### BingoSim.Infrastructure (Adapters)
- EF Core DbContext, migrations, entity configurations
- Repository implementations for Core/Application interfaces
- MassTransit configuration, consumers, and publishers
- Implements cross-cutting technical concerns (e.g., caching if needed)

### BingoSim.Web (Presentation)
- Server-rendered UI (e.g., Razor/Blazor Server style)
- Calls Application use cases (commands/queries)
- Displays batch progress and results by querying database via Application queries
- Does not execute simulation workloads by default; may host an optional in-process executor for local/dry-run execution

### BingoSim.Worker (Execution)
- MassTransit consumers that execute simulation workloads
- Uses Application services to run simulations and compute aggregates
- Writes results directly to Postgres using Infrastructure repositories
- Runs independently; the Web UI is not required for completion

---

## Runtime Topology (Docker-first)

Components (containers):
- Web: `BingoSim.Web`
- Worker(s): `BingoSim.Worker` (scaled horizontally as needed)
- Postgres: database for configuration and results
- RabbitMQ: message broker for work orchestration (MassTransit)

Connectivity:
- Web connects to Postgres and RabbitMQ
- Workers connect to Postgres and RabbitMQ
- Workers operate independently; UI is optional once a batch is started

---

## Key Architectural Decisions

### 1) Workers Drive Batches to Completion
When a batch is started, the system must not depend on the UI remaining active. The first message triggers worker-side orchestration that:
- materializes run work items
- fans out execution work
- retries failures
- marks completion/failed states in the database

### 2) Snapshot-at-Start for Reproducibility
Even though events reference the *latest* Activity/Player definitions by default, a simulation batch should store the effective configuration used at start time.

A batch start produces an `EventSnapshot` persisted as JSON:
- effective Event config
- selected Teams and their Strategies + Params
- resolved PlayerProfiles included in Teams
- resolved ActivityDefinitions referenced by tiles
- RNG seed information (user-provided or system-generated)

Workers use the snapshot for the entire batch, preventing mid-batch edits from changing behavior.

### 3) Fire-and-Forget Messaging
The Web UI enqueues a “batch started” message and returns immediately. Results are persisted by workers and viewed by the UI through queries.

### 3a) Optional Local Execution for Testing and Debugging
The Web application may host an in-process executor that uses the same Application-level simulation engine as distributed workers.

This execution mode is intended for:
- single-run and small-batch testing
- debugging strategies and configurations
- reproducible runs using user-provided seeds

Local execution follows the same snapshot, persistence, and result aggregation rules as distributed execution, but does not require external worker containers.

### 4) Parallel Workers, No Ordering Assumptions
Workers may run in parallel, and a single worker process may execute multiple simulations concurrently. No design depends on deterministic ordering or strict sequencing across workers.

---

## Core Execution Flow

### Flow A: Configure Libraries
User creates/edits:
- PlayerProfiles (skills, capabilities, schedules)
- ActivityDefinitions (attempt definitions, outcomes, scaling)
- Events (rows, tiles, tile-activity rules)

These are stored in Postgres and are “live” definitions for future snapshots.

### Flow B: Start Simulation Batch (Web → Application)
1. User selects Event, drafts Teams, assigns PlayerProfiles, and sets Team strategy + JSON params.
2. User enters run count (N), optionally selects dry-run (N=1), and may provide a seed for reproducibility.
3. Web calls an Application command, e.g. `StartSimulationBatchCommand`.

Application command responsibilities:
- Load the current effective definitions from repositories
- Validate minimal invariants required to execute (lightweight; not heavy UI validation)
- Materialize and persist:
  - `SimulationBatch` row (requested runs, status)
  - `EventSnapshot` JSON row (effective config)
- Publish `SimulationBatchStarted` message (fire-and-forget)

### Flow C: Batch Orchestration (Worker)
A worker consumer receives `SimulationBatchStarted` and:
1. Creates run work items in the database (recommended):
   - `SimulationRun` rows for 1..N (status = Pending)
2. Fans out execution messages:
   - publish `ExecuteSimulationRun { RunId }` messages (one per run)
   - or publish in chunks if needed later (v1 can do one message per run)

This allows workers to scale horizontally and process runs independently.

### Flow D: Run Execution (Worker)
A worker consumer receives `ExecuteSimulationRun`:
1. Loads the run and snapshot
2. Executes simulation using Application-level strategy logic and domain rules
3. Persists aggregated results:
   - per-team winner, points, tiles completed, row reached
   - per-team timelines: row unlock and tile completion timestamps (sim-time seconds)
4. Marks run as Completed

After completion:
- Batch completion can be tracked by querying runs count (Pending/Running vs Completed/Failed)
- A worker may periodically mark the `SimulationBatch` as Completed once all runs are terminal

---

## Messaging Contracts (Conceptual)

Recommended message types (contracts live outside Core; typically in Application or Infrastructure contracts folder):

- `SimulationBatchStarted`
  - BatchId
  - EventSnapshotId
  - RunsRequested

- `ExecuteSimulationRun`
  - SimulationRunId

Optional (v1 can skip if batch completion is derived from DB):
- `SimulationRunCompleted`
- `SimulationRunFailed`
- `SimulationBatchCompleted`

Notes:
- Messages are fire-and-forget.
- UI observes progress by querying database state, not by subscribing to messages.

---

## Failure Handling & Retry Strategy

### Expectations
- Transient failures should retry automatically.
- Failures must be tracked to avoid infinite retries.
- Terminal failures should surface to the UI as an error state.

### Approach
- Use MassTransit retry policies for transient failures.
- Persist failure metadata:
  - AttemptCount
  - LastError (truncated)
  - LastAttemptAt
  - Status (Pending / Running / Completed / Failed)

Terminal rule example:
- After X attempts (e.g., 5), mark `SimulationRun` as Failed and stop retrying.

Batch status:
- A batch is:
  - Completed when all runs are Completed
  - Failed when any run is Failed (or a threshold is exceeded)
  - CompletedWithFailures if you decide to allow partial completion later (not required for v1)

---

## Data & Persistence

### Postgres is Source of Truth
- Library definitions (PlayerProfiles, ActivityDefinitions, Events)
- Batches, runs, snapshots, results
- Progress and completion timelines (aggregated, not per-attempt trace)

### Snapshot Storage
- `EventSnapshot` persisted as JSON for v1 simplicity.
- Workers read from snapshot for all runs in the batch.

### Results Storage (Aggregated)
Per run:
- Per-team:
  - Winner flag
  - Total points
  - Tiles completed count
  - Row reached
  - Row unlock timestamps
  - Tile completion timestamps
- No per-attempt logs stored in DB (logs go to console/structured logging)

---

## Concurrency Model

### Horizontal Scale
- Run multiple worker containers.
- Each worker consumes from RabbitMQ queues.

### In-Process Parallelism
- Each worker may run multiple simulations concurrently.
- Concurrency should be configurable:
  - e.g., `WorkerOptions.MaxConcurrentRuns`

### No Ordering Guarantees
- No system behavior depends on message ordering.
- Timelines are internal to a simulation run (sim-time), not derived from global order.

---

## Observability & Logging

### Structured Logging
- Use structured logs throughout Web and Worker.
- Worker logs should include:
  - BatchId
  - RunId
  - Team (when relevant)
  - StrategyKey

### Verbosity
- Dry-run (single run) may enable more verbose logs to aid debugging.
- Batch runs should log coarse milestones to avoid log spam.

---

## Testing Strategy Alignment

- Core: unit tests for invariants, domain rules, value objects
- Application: unit tests for batch orchestration, snapshot creation, strategy behaviors
- Infrastructure: integration tests using real Postgres and RabbitMQ (containerized)
- Web: bUnit tests for non-trivial components
- Worker: tests for consumers, retry rules, and result persistence behaviors

---

## Summary

- Web configures and starts batches; it may optionally execute local simulations for testing, while production runs use distributed workers.
- Workers orchestrate and execute simulation runs independently.
- A snapshot of effective configuration is captured at batch start for reproducibility.
- Messaging is fire-and-forget; progress and results are observed via database queries.
- The system scales via multiple workers and in-worker parallelism without ordering constraints.

