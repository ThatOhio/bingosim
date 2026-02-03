# Slice 7: Distributed Workers — Quick Start

## Docker: Start RabbitMQ + Web + N Workers

### Prerequisites
- Docker and Docker Compose
- Optional: copy `.env.example` to `.env` and adjust RABBITMQ_* if needed

### Start the stack

```bash
# From project root
docker compose up -d

# Or start specific services only:
docker compose up -d postgres rabbitmq
docker compose up -d bingosim.web
docker compose up -d --scale bingosim.worker=2   # 2 workers
```

### Environment variables (compose)

| Variable | Description | Default |
|----------|-------------|---------|
| RABBITMQ_HOST | RabbitMQ host (inside compose: rabbitmq) | rabbitmq |
| RABBITMQ_PORT | AMQP port | 5672 |
| RABBITMQ_USER | AMQP user | guest |
| RABBITMQ_PASS | AMQP password | guest |
| ConnectionStrings__DefaultConnection | DB connection | Host=postgres;... |

### Ports

- **Postgres:** 5432
- **RabbitMQ AMQP:** 5672
- **RabbitMQ Management UI:** 15672 (http://localhost:15672, user guest / guest)
- **Web:** 8080, 8081

---

## Run a Distributed Batch from the UI

1. **Start the stack** (see above), or run locally:
   ```bash
   # Terminal 1: Postgres + RabbitMQ (or use compose)
   docker compose up -d postgres rabbitmq

   # Terminal 2: Web
   dotnet run --project BingoSim.Web

   # Terminal 3: Worker (optional: run 2 workers in separate terminals)
   dotnet run --project BingoSim.Worker
   ```

2. **Open** http://localhost:5000 (or compose port).

3. **Create prerequisites** if not seeded:
   - Players Library: at least 2 PlayerProfiles
   - Activities Library: at least 1 ActivityDefinition
   - Events: at least 1 Event with rows/tiles; draft 2 Teams

4. **Go to Run Simulations.**

5. **Select** Event and teams; set Run count (e.g. 100 or 1000).

6. **Select** **Distributed** execution mode (radio button).

7. **Click** "Start batch."

8. **Navigate** to Simulation Results → Batches list → click the batch.

9. **Observe** progress (Pending → Running → Completed) and results when done.

10. **Optional:** Stop the Web container while batch runs; restart Web later; results still visible.

---

## Parallelism Validation (Manual Guide)

Use the **SimulationDelayMs** throttle knob to validate that multiple workers outperform a single worker. The delay artificially slows each run so that parallelism yields measurable improvement.

### Throttle Configuration

Set `SimulationDelayMs` in Worker config to add a fixed delay (ms) per run:

| Location | Example |
|----------|---------|
| **Worker appsettings.json** | `"WorkerSimulation": { "SimulationDelayMs": 100 }` |
| **Environment variable** | `WorkerSimulation__SimulationDelayMs=100` |
| **Docker Compose** | Add to `bingosim.worker` env: `WorkerSimulation__SimulationDelayMs=100` |

### Step-by-Step Validation

1. **Start infrastructure:** Postgres + RabbitMQ
   ```bash
   docker compose up -d postgres rabbitmq
   # Or locally: ensure Postgres and RabbitMQ are running
   ```

2. **Configure throttle:** Set `SimulationDelayMs: 100` in Worker `appsettings.json` (or via env).

3. **Run 1 — Single worker:**
   - Start Web: `dotnet run --project BingoSim.Web`
   - Start **1** Worker: `dotnet run --project BingoSim.Worker`
   - In UI: Run Simulations → Distributed → 20 runs → Start batch
   - Navigate to batch details; note elapsed time **T1** when Completed

4. **Run 2 — Two workers:**
   - Keep Web running (or restart)
   - Start **2** Workers (two terminals): `dotnet run --project BingoSim.Worker`
   - Run a new distributed batch of **20 runs**
   - Note elapsed time **T2** when Completed

5. **Expected:** T2 < T1 (measurable improvement; no brittle timing assertions).

### Docker Variant

```bash
# Add throttle to worker (optional; for parallelism validation)
# In compose.yaml, add under bingosim.worker environment:
#   - WorkerSimulation__SimulationDelayMs=100

docker compose up -d postgres rabbitmq
docker compose up -d bingosim.web

# Run 1: 1 worker
docker compose up -d bingosim.worker
# Run batch of 20 via UI, note T1

# Run 2: 2 workers
docker compose up -d --scale bingosim.worker=2
# Run new batch of 20, note T2
# Expect T2 < T1
```

---

## Performance Benchmarking

See [PERF_NOTES.md](../../PERF_NOTES.md) for full details.

- **Local E2E:** `dotnet run --project BingoSim.Seed -- --perf --runs 10000`
- **Regression guard:** `dotnet run --project BingoSim.Seed -- --perf-regression`
- **Engine-only tests:** `dotnet test --filter "Category=Perf"`
