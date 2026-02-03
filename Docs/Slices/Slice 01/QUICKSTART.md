# Quick Start Guide - BingoSim Slice 1

## Prerequisites
- .NET 10 SDK installed
- Docker installed and running
- Docker permissions configured

## Setup Docker Permissions (One-time)

```bash
# Add your user to docker group
sudo usermod -aG docker $USER

# Apply changes (choose one)
newgrp docker           # Apply in current shell
# OR
# Logout and login again
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
# Should show postgres as "healthy"
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

## Test the Players CRUD

1. Click **"Players"** in sidebar
2. Click **"Create Player"**
3. Fill form:
   - Name: "Test Player"
   - Skill Multiplier: 0.9
   - Add Capability: `quest.ds2` / `Desert Treasure 2`
   - Add Session: Monday, 18:00, 120 min
4. Submit → See player in list
5. Click **"Edit"** → Modify → Save
6. Click **"Delete"** → Confirm → Removed
7. Refresh page → Data persists

## Run Tests

```bash
# Unit tests only (no Docker needed)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# All tests (requires Docker)
dotnet test
```

Expected: 89 unit tests pass (50 Core + 39 Application)

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

**Permission denied:**
```bash
sudo usermod -aG docker $USER
newgrp docker
```

**Migration errors:**
```bash
# Reset database
docker compose down -v
docker compose up -d postgres
dotnet run --project BingoSim.Web  # Auto-migrates
```

## What's Implemented

✅ Full PlayerProfile CRUD
✅ Multiple sessions per day
✅ Delete confirmation
✅ Form validation
✅ Persistent storage (PostgreSQL)
✅ 89 passing unit tests
✅ Clean Architecture compliant

See `SLICE1_COMPLETE.md` for full details.
