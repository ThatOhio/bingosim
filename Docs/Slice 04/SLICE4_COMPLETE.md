# Slice 4: Team Drafting & Strategy Assignment - Implementation Complete ✅

## Summary

Team Drafting & Strategy Assignment has been implemented per `Docs/Slice 04/SLICE4_PLAN.md`. From an existing Event (Slice 3), users can create/edit/delete Teams, assign PlayerProfiles to each Team (many-to-many via TeamPlayer), and assign StrategyConfig (StrategyKey + ParamsJson) per Team. Supported strategy keys are in `BingoSim.Application/StrategyKeys/StrategyCatalog.cs` (RowRush, GreedyPoints). Clean Architecture and project standards are followed. Configuration only; no Run Simulations, Workers, or Results in this slice.

---

## What Was Implemented

### Domain Layer (BingoSim.Core)

- ✅ **Team** entity: Id, EventId, Name, CreatedAt; navigation StrategyConfig (1:1), TeamPlayers (many)
- ✅ **TeamPlayer** entity: Id, TeamId, PlayerProfileId (membership; unique per team)
- ✅ **StrategyConfig** entity: Id, TeamId, StrategyKey, ParamsJson (1:1 with Team)
- ✅ **TeamNotFoundException**, **ITeamRepository** (GetByIdAsync, GetByEventIdAsync, AddAsync, UpdateAsync, DeleteAsync, DeleteAllByEventIdAsync, ExistsAsync)

### Application Layer (BingoSim.Application)

- ✅ **StrategyKeys/StrategyCatalog** — Supported keys: RowRush, GreedyPoints; `GetSupportedKeys()`, `IsSupported(key)`
- ✅ DTOs: TeamResponse, CreateTeamRequest, UpdateTeamRequest
- ✅ **ITeamService**, **TeamService** (GetByEventIdAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync, DeleteAllByEventIdAsync; validates event and player ids; structured logging for create/update/delete)
- ✅ **TeamMapper** (ToResponse, ToEntity, ApplyToEntity)
- ✅ FluentValidation: CreateTeamRequestValidator (event required, name required, no duplicate players, strategy key supported), UpdateTeamRequestValidator

### Infrastructure Layer (BingoSim.Infrastructure)

- ✅ **AppDbContext:** DbSet&lt;Team&gt;, DbSet&lt;TeamPlayer&gt;, DbSet&lt;StrategyConfig&gt;
- ✅ **TeamConfiguration**, **TeamPlayerConfiguration**, **StrategyConfigConfiguration** (EF Core; cascade/unique as planned)
- ✅ **TeamRepository:** AddAsync(Team, StrategyConfig, TeamPlayers), UpdateAsync, DeleteAsync, DeleteAllByEventIdAsync, GetByIdAsync (with includes), GetByEventIdAsync
- ✅ **DependencyInjection:** ITeamRepository → TeamRepository, ITeamService → TeamService
- ✅ Migration: **AddTeamsAndStrategy** (Teams, TeamPlayers, StrategyConfigs tables)

### Presentation Layer (BingoSim.Web)

- ✅ **/events/{EventId}/teams** — EventTeams.razor: list teams for event; Create Team; Edit; Delete; optional Delete all teams (with confirmation)
- ✅ **/events/{EventId}/teams/create** — EventTeamCreate.razor: name, player multi-select, strategy dropdown, ParamsJson textarea
- ✅ **/events/{EventId}/teams/{TeamId}/edit** — EventTeamEdit.razor: same form pre-filled; ParamsJson shown (null → "" for binding)
- ✅ Events list: "Draft teams" link per event row
- ✅ Duplicate players prevented: validator + form uses Distinct() in ToCreateRequest/ToUpdateRequest
- ✅ Reuse **DeleteConfirmationModal** for delete team and delete all teams
- ✅ Styling: EventTeams.razor.css, EventTeamCreate.razor.css, EventTeamEdit.razor.css

### Tests

- ✅ **Core unit tests:** TeamTests, TeamPlayerTests, StrategyConfigTests (invariants)
- ✅ **Application unit tests:** CreateTeamRequestValidatorTests (name required, event required, no duplicate players, strategy key); UpdateTeamRequestValidatorTests; TeamServiceTests (Create, GetByEventId, GetById, Update, Delete, validation)
- ✅ **Integration tests:** TeamRepositoryTests (create round-trip with memberships and strategy; update roster and strategy; delete and verify memberships removed; GetByEventIdAsync; **two teams for same event persist and rehydrate in fresh context**). Postgres Testcontainers (Docker required).

---

## Files Created / Modified (Slice 4)

### Created

**Core**  
- `BingoSim.Core/Entities/Team.cs`  
- `BingoSim.Core/Entities/TeamPlayer.cs`  
- `BingoSim.Core/Entities/StrategyConfig.cs`  
- `BingoSim.Core/Exceptions/TeamNotFoundException.cs`  
- `BingoSim.Core/Interfaces/ITeamRepository.cs`  

**Application**  
- `BingoSim.Application/StrategyKeys/StrategyCatalog.cs`  
- `BingoSim.Application/DTOs/TeamResponse.cs`  
- `BingoSim.Application/DTOs/CreateTeamRequest.cs`  
- `BingoSim.Application/DTOs/UpdateTeamRequest.cs`  
- `BingoSim.Application/Interfaces/ITeamService.cs`  
- `BingoSim.Application/Mapping/TeamMapper.cs`  
- `BingoSim.Application/Services/TeamService.cs`  
- `BingoSim.Application/Validators/CreateTeamRequestValidator.cs`  
- `BingoSim.Application/Validators/UpdateTeamRequestValidator.cs`  

**Infrastructure**  
- `BingoSim.Infrastructure/Persistence/Configurations/TeamConfiguration.cs`  
- `BingoSim.Infrastructure/Persistence/Configurations/TeamPlayerConfiguration.cs`  
- `BingoSim.Infrastructure/Persistence/Configurations/StrategyConfigConfiguration.cs`  
- `BingoSim.Infrastructure/Persistence/Repositories/TeamRepository.cs`  
- `BingoSim.Infrastructure/Persistence/Migrations/20260202004700_AddTeamsAndStrategy.cs`  
- `BingoSim.Infrastructure/Persistence/Migrations/20260202004700_AddTeamsAndStrategy.Designer.cs`  

**Web**  
- `BingoSim.Web/Components/Pages/Events/EventTeams.razor`  
- `BingoSim.Web/Components/Pages/Events/EventTeams.razor.css`  
- `BingoSim.Web/Components/Pages/Events/EventTeamCreate.razor`  
- `BingoSim.Web/Components/Pages/Events/EventTeamCreate.razor.css`  
- `BingoSim.Web/Components/Pages/Events/EventTeamEdit.razor`  
- `BingoSim.Web/Components/Pages/Events/EventTeamEdit.razor.css`  

**Tests**  
- `Tests/BingoSim.Core.UnitTests/Entities/TeamTests.cs`  
- `Tests/BingoSim.Core.UnitTests/Entities/TeamPlayerTests.cs`  
- `Tests/BingoSim.Core.UnitTests/Entities/StrategyConfigTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Validators/CreateTeamRequestValidatorTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Validators/UpdateTeamRequestValidatorTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Services/TeamServiceTests.cs`  
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/TeamRepositoryTests.cs`  

**Docs**  
- `Docs/Slice 04/SLICE4_PLAN.md` (pre-existing)  
- `Docs/Slice 04/ACCEPTANCE_REVIEW.md`  
- `Docs/Slice 04/REVIEW_SUMMARY.md`  
- `Docs/Slice 04/SLICE4_COMPLETE.md`  

### Modified

- `BingoSim.Infrastructure/Persistence/AppDbContext.cs` — added DbSet&lt;Team&gt;, DbSet&lt;TeamPlayer&gt;, DbSet&lt;StrategyConfig&gt;  
- `BingoSim.Infrastructure/DependencyInjection.cs` — registered ITeamRepository, ITeamService  
- `BingoSim.Web/Components/Pages/Events/Events.razor` — added "Draft teams" link per event  
- `BingoSim.Web/Components/Pages/Events/Events.razor.css` — added .btn-outline  
- `BingoSim.Web/Components/_Imports.razor` — added @using BingoSim.Application.StrategyKeys  
- `BingoSim.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — updated for Teams, TeamPlayers, StrategyConfigs  

---

## Migration Instructions

1. **Apply migration (development:** migrations apply automatically on startup when running the Web project in Development.  
2. **Apply migration manually:**
   ```bash
   dotnet ef database update --project BingoSim.Infrastructure --startup-project BingoSim.Web
   ```
3. **New migration name:** `AddTeamsAndStrategy` (adds Tables: Teams, TeamPlayers, StrategyConfigs with FKs and unique constraint on (TeamId, PlayerProfileId); unique index on StrategyConfigs.TeamId).

---

## Commands to Run Tests

```bash
# Unit tests only (no Docker)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Slice 4 unit tests only
dotnet test Tests/BingoSim.Core.UnitTests --filter "FullyQualifiedName~Team"
dotnet test Tests/BingoSim.Application.UnitTests --filter "FullyQualifiedName~Team"

# All tests (integration tests require Docker for Postgres Testcontainers)
dotnet test
```

---

## Manual UI Steps to Verify

1. **Start app:** `dotnet run --project BingoSim.Web` (ensure Postgres is running; e.g. `docker compose up -d postgres`).
2. **Events list:** Open **Events** in sidebar. Ensure at least one Event exists (create one if needed).
3. **Draft teams:** Click **"Draft teams"** on an event row → should navigate to `/events/{eventId}/teams`.
4. **Teams list:** Page title "Teams for [Event Name]". If no teams, empty state; click **Create Team**.
5. **Create team:** Enter name (e.g. "Team Alpha"). Select one or more players from the checklist. Choose **Strategy** (RowRush or GreedyPoints). Optionally enter Params (e.g. `{"key":"value"}`). Click **Create Team** → redirect to teams list; new team appears.
6. **Edit team:** From teams list, click **Edit** on a team. Change name, roster, or strategy. Click **Save** → redirect to teams list; changes persist.
7. **Delete team:** Click **Delete** on a team → confirmation modal. Click **Delete** → team removed from list.
8. **Delete all teams:** With at least one team, click **Delete all teams** → confirm → all teams for that event removed.
9. **Validation:** Try creating a team with empty name → validation error. Try selecting the same player twice (duplicate) → validation error if implemented in UI (validator rejects duplicate list). Strategy must be RowRush or GreedyPoints.

---

## Architecture Compliance

- **Core:** No EF or infrastructure; pure domain (Team, TeamPlayer, StrategyConfig)  
- **Application:** StrategyCatalog (StrategyKeys), DTOs, TeamService, TeamMapper, validators; depends only on Core  
- **Infrastructure:** EF configurations, TeamRepository; implements Core/Application interfaces  
- **Web:** Server-rendered Blazor; uses ITeamService; EventTeams list/Create/Edit and DeleteConfirmationModal  

---

## Next Steps (Future Slices)

Per `06_Acceptance_Tests.md`:

- Slice 5: Run Simulations (batch start, local execution)
- Slice 6: Results page (StrategyKey and ParamsJson visible in results; data model ready)
- Later: Workers, messaging, simulation engine, aggregations, metrics

---

**Slice:** 4 - Team Drafting & Strategy Assignment  
**Status:** ✅ Complete  
**Docs:** SLICE4_PLAN.md, ACCEPTANCE_REVIEW.md, REVIEW_SUMMARY.md, SLICE4_COMPLETE.md
