# Quick Start Guide - BingoSim Slice 2

## Prerequisites
- .NET 10 SDK installed
- Docker installed and running (for PostgreSQL and integration tests)
- Slice 1 (Players) already run at least once is optional; Activities are independent

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

## Test the Activities CRUD

1. Click **"Activities"** in sidebar
2. Click **"Create Activity"**
3. Fill form:
   - Key: `activity.zulrah`
   - Name: `Zulrah`
   - Mode: Supports Solo ✓, Supports Group ✓
   - Add Attempt: Key `personal_loot`, Roll Scope Per Player, Baseline Time 60, Distribution Uniform
   - In that attempt: Add Outcome Key `common`, Weight 1/1, Add Grant DropKey `drop.common`, Units 1
   - Add another outcome `rare`, Weight 1/100, Grant `drop.rare`, Units 3
   - Optionally add Group Scaling Band: Min 1, Max 4, Time Mult 1.0, Prob Mult 1.0
4. Submit → See activity in list
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

Expected: Unit tests pass for Core (including ActivityDefinition entities/value objects), Application (ActivityDefinitionService, mapper, validators). Integration tests for ActivityDefinitionRepository require Docker.

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
# Reset database
docker compose down -v
docker compose up -d postgres
dotnet run --project BingoSim.Web  # Auto-migrates
```

**Blazor form binding errors on nested items:**  
Activity Create/Edit use native `<input>`/`<select>` with `@bind` for nested attempt/outcome/grant fields to avoid index expression issues. If you add new nested fields, prefer the same pattern.

## What's Implemented (Slice 2)

✅ Full ActivityDefinition CRUD  
✅ Multiple attempt definitions (loot lines) with Per Player / Per Group roll scope  
✅ Attempt time model (baseline, distribution, optional variance)  
✅ Outcomes with weighted probabilities and multiple progress grants (Units ≥ 1)  
✅ Group scaling bands (MinSize..MaxSize, time/probability multipliers)  
✅ Delete confirmation modal  
✅ Form validation (FluentValidation, nested DTOs)  
✅ Persistent storage (PostgreSQL, JSON columns for nested data)  
✅ Unit and integration tests for ActivityDefinition  
✅ Clean Architecture compliant  

See `SLICE2_COMPLETE.md` for full details.
