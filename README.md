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

## Full Stack with Docker Compose

To run everything in containers:

```bash
docker compose up -d
```

- **Postgres:** localhost:5432
- **RabbitMQ AMQP:** localhost:5672
- **RabbitMQ Management UI:** http://localhost:15672 (guest / guest)

The Web and Worker services run in the same network as Postgres and RabbitMQ. To access the Web UI from your host, add `ports: - "8080:8080"` under `bingosim.web` in `compose.yaml`.

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
