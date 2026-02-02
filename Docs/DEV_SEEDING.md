# Development Seed Data

The Dev Seed system populates the database with realistic test data for manual UI testing. It covers **Slices 1–4**: PlayerProfiles, ActivityDefinitions, Events (Rows/Tiles/TileActivityRules), and **Teams + StrategyConfig** for seed events.

## How to Run Seeding Locally

### Prerequisites

- .NET 10 SDK
- PostgreSQL running (e.g. via Docker)

### Start PostgreSQL

```bash
docker compose up -d postgres
```

Wait for healthy status: `docker compose ps`

### Run the Seeder

**Idempotent seed** (creates or updates seed entities; safe to run repeatedly):

```bash
dotnet run --project BingoSim.Seed
```

**Reset and reseed** (deletes only seed-tagged data, then re-seeds):

```bash
dotnet run --project BingoSim.Seed -- --reset
```

Connection string is read from `BingoSim.Seed/appsettings.json` (default: `Host=localhost;Port=5432;Database=bingosim;Username=postgres;Password=postgres`). Override via environment variables, e.g.:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;..." dotnet run --project BingoSim.Seed
```

## Running Seeding in Docker

If the Web app runs in Docker and you want to seed the same database from your host:

1. Ensure PostgreSQL is reachable from your host (e.g. port 5432 published).
2. Run the seeder from your host using the Docker network host or the published port:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bingosim;Username=postgres;Password=postgres" dotnet run --project BingoSim.Seed
```

To seed from inside the Docker network (e.g. in a custom init script), use `Host=postgres` instead of `localhost`.

## Verify in the UI

1. Start the Web app: `dotnet run --project BingoSim.Web`
2. Open `https://localhost:5001`
3. **Players**: 8 seed players (Alice, Bob, Carol, Dave, Eve, Frank, Grace, Henry) with varied skill multipliers, capabilities, and weekly schedules
4. **Activities**: 6 activities (Zulrah, Vorkath, Runecraft, Mining, CoX, ToA) with multiple loot lines, PerPlayer/PerGroup roll scopes, and group scaling bands
5. **Events**: 2 events (Winter Bingo 2025, Spring League Bingo) with 3+ rows, 4 tiles per row, and TileActivityRules referencing activities
6. **Teams (Slice 4)**: For each seed event, 2 teams — **Team Alpha** (RowRush, first 4 players) and **Team Beta** (GreedyPoints, next 4 players). Visible under **Events → Draft teams** and on **Run Simulations** when an event is selected.

## Idempotency

- **Players**: Lookup by `Name`. If found, update skill multiplier, capabilities, and weekly schedule; otherwise create.
- **Activities**: Lookup by `Key`. If found, update; otherwise create.
- **Events**: Lookup by `Name`. If found, update; otherwise create.

Seeding uses stable keys (names/keys) for find-or-create. No duplicates are created when run multiple times.

## Reset Behavior

`--reset` deletes **only** entities that match the seed stable keys. **Reset order** is enforced so that dependencies are removed first (no FK violations): **Teams (and their StrategyConfigs + TeamPlayers) for each seed event → that Event → Activities → Players**. Within each step, seed events are processed by name; then activities by key; then players by name.

- **Teams**: All teams for seed events ("Winter Bingo 2025", "Spring League Bingo") are deleted (StrategyConfigs and TeamPlayers are removed with them).
- **Events**: "Winter Bingo 2025", "Spring League Bingo"
- **Activities**: `boss.zulrah`, `boss.vorkath`, `skilling.runecraft`, `skilling.mining`, `raid.cox`, `raid.toa`
- **Players**: Alice, Bob, Carol, Dave, Eve, Frank, Grace, Henry

The database is **not** dropped; only these seed-tagged records are removed before re-seeding.

## Slice 4 (Teams/Strategy) — Included

Seed data for **Teams** and **StrategyConfig** is included so that Run Simulations can be used with seeded events without manual team drafting:

- **SeedTeamsAsync**: For each seed event ("Winter Bingo 2025", "Spring League Bingo"), creates or updates **Team Alpha** (strategy RowRush, first 4 seed players) and **Team Beta** (strategy GreedyPoints, next 4 seed players). StrategyConfig uses sample `ParamsJson` `"{}"`.
- **Reset order**: On `--reset`, teams for each seed event are deleted first (via `DeleteAllByEventIdAsync`), then that event, then all seed activities, then all seed players. This order is correct for foreign keys (teams reference events; events reference activities via tiles).

## Updating Seed Content

When new slices add new configuration entities, update the seed definitions in `BingoSim.Application/Services/DevSeedService.cs`:

- Add new entity types to the appropriate `Get*SeedDefinitions()` method
- Add stable keys to the `Seed*Names` / `Seed*Keys` arrays for reset support
- Extend `SeedAsync` and `ResetAndSeedAsync` accordingly
