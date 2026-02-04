# BingoSim

Runs simulations to aid in planning OSRS bingo pathing.

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose (for Postgres and RabbitMQ)
- Or: PostgreSQL 16+ and RabbitMQ 3+ running locally

## Development Setup

### 1. Start Infrastructure

Start Postgres and RabbitMQ via Docker Compose:

```bash
docker compose up -d postgres rabbitmq
```

Wait for healthy status: `docker compose ps`

Default connection: `Host=localhost;Port=5432;Database=bingosim;Username=postgres;Password=postgres`

### 2. Seed the Database

Populate the database with sample data (players, activities, events, teams):

**Idempotent seed** (safe to run repeatedly; creates or updates seed entities):

```bash
dotnet run --project BingoSim.Seed
```

**Reset and reseed** (deletes seed-tagged data, then re-seeds):

```bash
dotnet run --project BingoSim.Seed -- --reset
```

**Full database reset** (deletes all application data; requires confirmation):

```bash
dotnet run --project BingoSim.Seed -- --full-reset
```

To skip the confirmation prompt (e.g. in scripts): `--full-reset --confirm`

Connection string can be overridden via environment:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bingosim;Username=postgres;Password=postgres" dotnet run --project BingoSim.Seed
```

### 3. Run the Web Application

```bash
dotnet run --project BingoSim.Web
```

Open http://localhost:5212 (or https://localhost:7283 if using HTTPS).

### 4. (Optional) Run the Worker for Distributed Execution

For distributed simulation mode (RabbitMQ), start one or more workers:

```bash
dotnet run --project BingoSim.Worker
```

Run additional workers in separate terminals to scale horizontally.

### Running Workers with Partitioning (Phase 4F)

To enable worker partitioning for multi-worker scaling, run workers manually with unique indices:

**Terminal 1:**
```bash
WORKER_INDEX=0 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

**Terminal 2:**
```bash
WORKER_INDEX=1 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

**Terminal 3:**
```bash
WORKER_INDEX=2 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

Ensure `DISTRIBUTED_WORKER_COUNT=3` is set in the Web application's environment.

**Docker Compose:** Worker partitioning works with `docker compose up` and replicas. Each container gets a unique hostname (e.g. `bingosim_bingosim.worker_1`), and the worker derives its index from the trailing number. Ensure `DISTRIBUTED_WORKER_COUNT` matches `WORKER_REPLICAS` when scaling:

```bash
WORKER_REPLICAS=5 DISTRIBUTED_WORKER_COUNT=5 docker compose up -d
```

## Full Stack with Docker Compose

To run everything in containers:

```bash
docker compose up -d
```

- **Postgres:** localhost:5432
- **RabbitMQ AMQP:** localhost:5672
- **RabbitMQ Management UI:** http://localhost:15672 (guest / guest)

The Web and Worker services run in the same network as Postgres and RabbitMQ. To access the Web UI from your host, add `ports: - "8080:8080"` under `bingosim.web` in `compose.yaml`.

## Performance Tuning (Multi-Worker Scaling)

When running distributed simulations with multiple workers, PostgreSQL and connection pool tuning can improve throughput.

### PostgreSQL Tuning (Docker Compose)

The `compose.yaml` postgres service applies performance-oriented settings for write-heavy simulation workloads:

| Setting | Value | Effect |
|---------|-------|--------|
| `synchronous_commit` | off | Reduces fsync latency; trades durability for throughput |
| `shared_buffers` | 256MB | More cache, fewer disk reads |
| `work_mem` | 16MB | Better for sorts/joins in aggregates |
| `max_connections` | 200 | Supports more concurrent worker connections |

**Durability tradeoff:** `synchronous_commit=off` means up to a few seconds of data may be lost on a crash before it is fsync'd to disk. This is acceptable for simulation workloads because batches are re-runnable and results can be regenerated. For production with strict durability requirements, coordinate with your DBA and consider keeping `synchronous_commit=on`.

### Connection Pool Sizing

Connection strings include `Maximum Pool Size=50` to support 3 workers × concurrent operations (claim + persist). If you see "connection pool exhausted" errors when scaling workers, increase this value or reduce `WorkerSimulation:MaxConcurrentRuns` per worker.

## Upgrading / Database Wipe

When upgrading BingoSim, **a full database reset is expected** if snapshot schema or validation rules have changed. Snapshot fields are required; there is no backward compatibility with older snapshots.

- Run `dotnet run --project BingoSim.Seed -- --full-reset --confirm` to wipe and re-seed.
- Ensure all drafted teams have **StrategyConfig** configured before running simulations.

## Project Structure

- **BingoSim.Core**: Domain models and core logic.
- **BingoSim.Application**: Application use cases and business logic.
- **BingoSim.Infrastructure**: Data persistence, messaging, and external services.
- **BingoSim.Web**: Web interface for configuration and monitoring.
- **BingoSim.Worker**: Computation nodes for executing simulation jobs.
- **BingoSim.Shared**: Shared models and utilities.
