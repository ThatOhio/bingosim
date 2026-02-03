# Quick Start Guide - BingoSim Slice 3

## Prerequisites
- .NET 10 SDK installed
- Docker installed and running (for PostgreSQL and integration tests)
- Slice 1 (Players) and Slice 2 (Activities) run at least once recommended; Events reference ActivityDefinitions

## Setup Docker Permissions (One-time)

```bash
# Add your user to docker group
sudo usermod -aG docker $USER

# Apply changes (one of)
newgrp docker
# OR logout and login again
```

## Start the Application

### 1. Start PostgreSQL
```bash
cd /home/ohio/Projects/bingosim
docker compose up -d postgres
```

Wait for healthy status:
```bash
docker compose ps
# postgres should show "healthy"
```

### 2. Run the Web App
```bash
dotnet run --project BingoSim.Web
```

Output will show:
```
Now listening on: https://localhost:5001
```

### 3. Open Browser
Navigate to: `https://localhost:5001`

## Test the Events CRUD

1. Click **"Events"** in the sidebar
2. Click **"Create Event"**
3. Fill form:
   - Name: `Test Bingo`
   - Duration: `24:00` (24 hours)
   - Unlock points per row: `5`
   - Add Row (Index 0): add 4 Tiles with Keys `t1`–`t4`, Points 1–4, RequiredCount 1
   - For each tile add at least one Activity Rule: select an Activity from the library, optional AcceptedDropKeys, Requirements, Modifiers
4. Submit → See event in list
5. Click **"Edit"** → Change name or add/remove rows/tiles/rules → Save
6. Click **"Delete"** → Confirm → Removed
7. Refresh page → Data persists

## Run Tests

```bash
# Unit tests only (no Docker needed)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# All tests (requires Docker for integration tests)
dotnet test
```

Expected: Unit tests pass for Core (Event, Row, Tile, TileActivityRule, ActivityModifierRule), Application (EventService, validators). Integration tests for EventRepository require Docker.

## Stop Services

```bash
docker compose down
```

## Troubleshooting

**Port 5432 in use:**
```bash
sudo lsof -i :5432
docker compose down
```

**Permission denied (Docker):**
```bash
sudo usermod -aG docker $USER
newgrp docker
```

**Migration errors:**
```bash
docker compose down -v
docker compose up -d postgres
dotnet run --project BingoSim.Web  # Auto-migrates in Development
```

## What's Implemented

✅ Full Event CRUD with nested Rows and Tiles  
✅ Rows ordered by explicit Index; each row exactly 4 Tiles (Points 1–4)  
✅ Tiles with Key, Name, Points, RequiredCount, and 1+ TileActivityRules  
✅ TileActivityRules: ActivityDefinition from library, AcceptedDropKeys, Requirements, Modifiers  
✅ Add/remove rows, tiles, and rules in Create/Edit  
✅ Delete with confirmation  
✅ Persistence to PostgreSQL (Rows as JSON)  
✅ Clean Architecture (Core, Application, Infrastructure, Web)

See `SLICE3_COMPLETE.md` for full details.
