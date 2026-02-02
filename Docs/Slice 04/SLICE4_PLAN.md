# Slice 4: Team Drafting & Strategy Assignment — Plan Only

**Scope:** From an existing Event (Slice 3), allow drafting Team configurations: create/edit/delete Teams, assign PlayerProfiles to each Team, assign StrategyConfig (StrategyKey + ParamsJson) per Team. Configuration only; no Run Simulations, Workers, Messaging, Simulation Engine, Results, or Aggregations.

**Source of truth:** `Docs/06_Acceptance_Tests.md` — Section "4) Team Drafting & Strategy Assignment" and Section "9) UI Expectations" (drafting/assigning teams and strategies).

**Constraints:** Clean Architecture; Web server-rendered; EF Core + Postgres via Infrastructure. Teams are per-event only (no reusable team templates). StrategyKey and ParamsJson must be persisted and support future visibility in Results (data model only; no results UI in this slice).

---

## 1) Data Model Design

### 1.1 Entities (Core)

All entities are persisted and owned within an Event context. Teams do not persist across events.

#### Team (Entity)

- **Location:** `BingoSim.Core/Entities/Team.cs`
- **Properties:**
  - `Id` (Guid)
  - `EventId` (Guid) — FK to Event
  - `Name` (string)
  - `CreatedAt` (DateTimeOffset)
- **Relationships:**
  - Team belongs to one Event (EventId).
  - Team has many TeamPlayers (membership).
  - Team has exactly one StrategyConfig (1:1; strategy key + params).
- **Invariants:**
  - Name not null/empty (trimmed).
  - EventId != default.
- **Behavior:** Constructor, `UpdateName`, methods to manage players and strategy via aggregates or repository (no embedded collections if StrategyConfig is separate; TeamPlayer is separate entity).

#### TeamPlayer (Entity — membership)

- **Location:** `BingoSim.Core/Entities/TeamPlayer.cs`
- **Properties:**
  - `TeamId` (Guid)
  - `PlayerProfileId` (Guid)
- **Purpose:** Links a reusable PlayerProfile (from Players Library, Slice 1) to a specific Team. Enables many-to-many between Team and PlayerProfile without embedding.
- **Invariants:** TeamId != default; PlayerProfileId != default. Uniqueness: (TeamId, PlayerProfileId) unique so the same player is not added twice to the same team.
- **Note:** No separate Id required if we model as a join entity with composite PK (TeamId, PlayerProfileId); alternatively use a surrogate Id for simplicity and unique constraint. Plan assumes **surrogate Id** (Guid) + unique constraint on (TeamId, PlayerProfileId) for consistency with other entities.

#### StrategyConfig (Entity or value owned by Team)

- **Location:** `BingoSim.Core/Entities/StrategyConfig.cs` (or value object; plan uses **separate entity** for clear persistence and 1:1 with Team).
- **Properties:**
  - `Id` (Guid) — if entity
  - `TeamId` (Guid) — FK to Team (1:1)
  - `StrategyKey` (string) — e.g. "RowRush", "GreedyPoints"
  - `ParamsJson` (string) — optional JSON blob; stored as-is, no parsing/validation in this slice
- **Relationships:** Team has one StrategyConfig (1:1). StrategyConfig belongs to Team.
- **Invariants:** StrategyKey not null/empty; allowed keys for UI: "RowRush", "GreedyPoints" (at least two placeholder keys). ParamsJson can be null or empty.
- **Note:** Domain doc uses "ParametersJson"; this plan uses **ParamsJson** per slice requirements; implementation can align to one name.

### 1.2 Supported Strategy Keys (placeholder)

- **RowRush** — baseline
- **GreedyPoints** — alternative

Provided as a fixed list (e.g. constant or small enum-like list) for dropdown in UI; no engine behavior in this slice.

### 1.3 Core Exceptions and Repository Interface

- **TeamNotFoundException** — `BingoSim.Core/Exceptions/TeamNotFoundException.cs`
- **ITeamRepository** — `BingoSim.Core/Interfaces/ITeamRepository.cs`
  - `GetByIdAsync(Guid id)`, `GetByEventIdAsync(Guid eventId)` (all teams for an event), `AddAsync(Team team)`, `UpdateAsync(Team team)`, `DeleteAsync(Guid id)`, `ExistsAsync(Guid id)`.
  - Optionally: `DeleteAllByEventIdAsync(Guid eventId)` for "delete entire team set" if desired; otherwise delete teams one-by-one from UI.

---

## 2) Application: Commands, Queries, DTOs, Validators, Mapping, Service

### 2.1 Queries

- **Get teams for event:** Returns list of teams for a given Event (each team includes Id, Name, EventId, player summary or list of PlayerProfileIds, StrategyKey, ParamsJson).
- **Get team by id:** Returns single team with full detail (players, strategy) for edit/view.

### 2.2 Commands (CRUD)

- **CreateTeam:** EventId, Name, List&lt;Guid&gt; PlayerProfileIds, StrategyKey, ParamsJson (optional string).
- **UpdateTeam:** Same shape as create (full replace of name, player list, strategy).
- **DeleteTeam:** By TeamId; with confirmation in UI.
- **Delete all teams for event (optional):** Single action to remove all teams for an event; confirm in UI.

### 2.3 DTOs

- **TeamResponse** — Id, EventId, EventName (optional, for display), Name, CreatedAt, PlayerIds (List&lt;Guid&gt;) or PlayerSummaries (List&lt;PlayerProfileSummaryDto&gt; — Id, Name), StrategyKey, ParamsJson.
- **TeamDetailResponse** — Same as TeamResponse; can reuse or extend with full player details if needed.
- **CreateTeamRequest** — EventId, Name, List&lt;Guid&gt; PlayerProfileIds, StrategyKey, ParamsJson (optional).
- **UpdateTeamRequest** — Same shape (Name, PlayerProfileIds, StrategyKey, ParamsJson); TeamId/EventId from route.

Use existing **PlayerProfileResponse** or a minimal **PlayerProfileSummaryDto** (Id, Name) when listing players assigned to a team.

### 2.4 Validators

- **CreateTeamRequestValidator** — EventId required and not default; Name required/max length; PlayerProfileIds not null (can be empty list); StrategyKey required and must be one of supported keys (RowRush, GreedyPoints); ParamsJson optional.
- **UpdateTeamRequestValidator** — Same as create (Name, PlayerProfileIds, StrategyKey, ParamsJson).

### 2.5 Mapping

- **TeamMapper** (static) in `BingoSim.Application/Mapping/TeamMapper.cs`:
  - `ToResponse(Team entity, StrategyConfig? strategy, IReadOnlyList<PlayerProfile> players)` or map from aggregate — build TeamResponse with Name, EventId, player ids/summaries, StrategyKey, ParamsJson.
  - `ToEntity(CreateTeamRequest)` — build Team (and StrategyConfig, TeamPlayers) for new team; service/repository persist Team, then StrategyConfig, then TeamPlayers (or repository handles cascade).
  - `ToEntity(UpdateTeamRequest, Team existing)` — update Name; replace TeamPlayers; replace StrategyConfig (StrategyKey, ParamsJson).

### 2.6 Service

- **ITeamService** — `GetByEventIdAsync(Guid eventId)`, `GetByIdAsync(Guid id)`, `CreateAsync(CreateTeamRequest)`, `UpdateAsync(Guid id, UpdateTeamRequest)`, `DeleteAsync(Guid id)`, optional `DeleteAllByEventIdAsync(Guid eventId)`.
- **TeamService** — uses ITeamRepository, IEventRepository (ensure event exists), IPlayerProfileRepository (resolve player ids for validation/display). Create: validate event exists, validate player ids exist, create Team + StrategyConfig + TeamPlayers. Update: load team, replace name/players/strategy, persist. Delete: remove team (cascade or explicit delete StrategyConfig and TeamPlayers then Team).

### 2.7 Strategy key list (application or shared)

- Expose supported strategy keys for UI dropdown: e.g. `TeamService.GetSupportedStrategyKeys()` or a shared constant/list in Application: `["RowRush", "GreedyPoints"]`.

---

## 3) Infrastructure: Persistence Plan (Tables + Relationships)

### 3.1 Tables

- **Teams**
  - Columns: Id (PK, Guid), EventId (FK to Events), Name (string, required), CreatedAt (DateTimeOffset).
  - Index: EventId (for GetByEventIdAsync).

- **TeamPlayers**
  - Columns: Id (PK, Guid), TeamId (FK to Teams), PlayerProfileId (FK to PlayerProfiles).
  - Unique constraint: (TeamId, PlayerProfileId).
  - Index: TeamId; optionally PlayerProfileId if needed for reverse lookups.

- **StrategyConfigs**
  - Columns: Id (PK, Guid), TeamId (FK to Teams, unique — 1:1), StrategyKey (string, required), ParamsJson (string, nullable).
  - One-to-one: Team → StrategyConfig (StrategyConfig.TeamId unique).

### 3.2 Relationships (EF Core)

- **Event** has many **Teams** (Event.Id → Team.EventId). No navigation collection on Event required for minimal slice; Team has EventId.
- **Team** has many **TeamPlayers** (Team.Id → TeamPlayer.TeamId). Cascade delete: when Team is deleted, delete TeamPlayers and StrategyConfig.
- **Team** has one **StrategyConfig** (Team.Id → StrategyConfig.TeamId); StrategyConfig.TeamId unique.
- **PlayerProfile** is referenced by **TeamPlayer** (PlayerProfile.Id → TeamPlayer.PlayerProfileId). No cascade from PlayerProfile to TeamPlayer (deleting a player does not remove them from historical team configs if we keep referential integrity; optional: restrict delete if player is in any team, or leave nullable for soft handling — prefer restrict or no cascade from PlayerProfiles).

### 3.3 DbContext and configurations

- **AppDbContext:** Add `DbSet<Team> Teams`, `DbSet<TeamPlayer> TeamPlayers`, `DbSet<StrategyConfig> StrategyConfigs`.
- **TeamConfiguration** — Map Team to table "Teams"; HasMany TeamPlayers; HasOne StrategyConfig (with foreign key on StrategyConfig).
- **TeamPlayerConfiguration** — Map to "TeamPlayers"; HasOne Team, HasOne PlayerProfile (reference only; no cascade from PlayerProfile).
- **StrategyConfigConfiguration** — Map to "StrategyConfigs"; HasOne Team (required). Unique index on TeamId.

### 3.4 Repository

- **TeamRepository** — Implements ITeamRepository. AddAsync/UpdateAsync/DeleteAsync for Team; when loading, Include StrategyConfig and TeamPlayers (and optionally PlayerProfiles for display). GetByEventIdAsync: query Teams where EventId = x, include StrategyConfig and TeamPlayers. DeleteAsync: remove StrategyConfig and TeamPlayers then Team (or configure cascade delete in EF).

### 3.5 Migration

- New migration: Add tables Teams, TeamPlayers, StrategyConfigs with FKs and unique constraint on (TeamId, PlayerProfileId) and unique TeamId on StrategyConfigs.

---

## 4) Web UI: Pages and Navigation

### 4.1 User flow (drafting teams for an event)

1. User goes to **Events** list (`/events`).
2. For an existing Event, user clicks a link such as **"Draft teams"** (or "Teams") that navigates to **Teams for this event**.
3. **Teams list for event** — List all teams for the selected event; show Event name as context; actions: Create Team, Edit, Delete (with confirmation).
4. **Create Team** — Form: Team name, multi-select (or checklist) of PlayerProfiles from Players Library, Strategy dropdown (RowRush, GreedyPoints), optional ParamsJson (textarea).
5. **Edit Team** — Same form pre-filled; save updates name, roster, strategy.
6. **Delete** — Confirmation modal; on confirm, delete team (and cascade StrategyConfig + TeamPlayers).
7. Optional: **"Delete all teams"** for this event — single button with confirmation.

### 4.2 Routes

- **Teams for event (list):** `@page "/events/{EventId:guid}/teams"` — e.g. `Pages/Events/EventTeams.razor` (or `Pages/Teams/Teams.razor` with route `/events/{EventId:guid}/teams`).
- **Create team:** `@page "/events/{EventId:guid}/teams/create"` — `EventTeamCreate.razor`.
- **Edit team:** `@page "/events/{EventId:guid}/teams/{TeamId:guid}/edit"` — `EventTeamEdit.razor`.

All under Events scope so URL reflects hierarchy: event → teams.

### 4.3 Pages

- **EventTeams.razor** — Title: "Teams for [Event Name]" (load event by EventId; if not found, show "Event not found"). Table: Team name, Player count (or names), Strategy, ParamsJson (truncated if long), Actions (Edit, Delete). Button: "Create Team". Each row: Edit link, Delete button (opens DeleteConfirmationModal).
- **EventTeamCreate.razor** — Form: Name, Player multi-select (load PlayerProfiles via IPlayerProfileService.GetAllAsync()), Strategy dropdown (RowRush, GreedyPoints), ParamsJson (optional textarea). Submit: call ITeamService.CreateAsync(CreateTeamRequest), redirect to `/events/{EventId}/teams`.
- **EventTeamEdit.razor** — Load team by TeamId; if not found or EventId mismatch, show error. Form: same as create; submit: ITeamService.UpdateAsync(TeamId, UpdateTeamRequest), redirect to `/events/{EventId}/teams`.

### 4.4 Navigation

- **Events list:** Add column or action "Draft teams" (or "Teams") per row — link to `/events/{EventId}/teams`. Optionally add next to "Edit" / "Delete".
- No new top-level nav item required; teams are reached from Events.

### 4.5 Shared components

- Reuse **DeleteConfirmationModal** for delete team (and optional delete all teams).
- Strategy keys: inject a service or constant that returns the list for the dropdown (e.g. from ITeamService or a small IStrategyKeyProvider).

### 4.6 Form models

- **TeamFormModel** — Name, List&lt;Guid&gt; SelectedPlayerIds, StrategyKey, ParamsJson. Methods: FromResponse(TeamDetailResponse), ToCreateRequest(EventId), ToUpdateRequest().

### 4.7 CSS

- Reuse existing patterns: `EventTeams.razor.css`, `EventTeamCreate.razor.css`, `EventTeamEdit.razor.css` (sections, form-group, consistent with Events/Activities/Players).

---

## 5) Test Plan

### 5.1 Core Unit Tests (`BingoSim.Core.UnitTests`)

- **TeamTests** — Constructor validates Name, EventId; UpdateName validates name; EventId required.
- **TeamPlayerTests** — Constructor with TeamId, PlayerProfileId; uniqueness invariant (same player twice on same team — entity or repo test).
- **StrategyConfigTests** — Constructor with StrategyKey, ParamsJson; StrategyKey required; ParamsJson optional.

### 5.2 Application Unit Tests (`BingoSim.Application.UnitTests`)

- **TeamServiceTests** — CreateAsync success and returns id; CreateAsync validates event exists; CreateAsync validates player ids exist (or allow empty roster); CreateAsync persists StrategyKey and ParamsJson; UpdateAsync replaces roster and strategy; GetByEventIdAsync returns teams for event; GetByIdAsync returns team with players and strategy; DeleteAsync removes team; GetByIdAsync not found returns null or throws TeamNotFoundException; UpdateAsync not found throws. Use NSubstitute for ITeamRepository, IEventRepository, IPlayerProfileRepository.
- **CreateTeamRequestValidatorTests / UpdateTeamRequestValidatorTests** — Valid request passes; missing name fails; invalid StrategyKey fails; EventId default fails.

### 5.3 Infrastructure Integration Tests (`BingoSim.Infrastructure.IntegrationTests`)

- **TeamRepositoryTests** — AddAsync Team with StrategyConfig and TeamPlayers; GetByIdAsync returns team with strategy and players; GetByEventIdAsync returns only teams for that event; UpdateAsync changes name and roster; DeleteAsync removes team and related StrategyConfig and TeamPlayers; unique (TeamId, PlayerProfileId) enforced. Use real Postgres (test container or local).

### 5.4 Web Tests (`BingoSim.Web.Tests`)

- **EventTeams page** — Renders list when teams exist; shows empty state when none; "Create Team" navigates to create (bUnit).
- **EventTeamCreate / EventTeamEdit** (optional) — Form renders; submit calls service and redirects. Minimal if time-constrained.

---

## 6) Exact List of Files to Create or Modify

### Create

**Core**

- `BingoSim.Core/Entities/Team.cs`
- `BingoSim.Core/Entities/TeamPlayer.cs`
- `BingoSim.Core/Entities/StrategyConfig.cs`
- `BingoSim.Core/Exceptions/TeamNotFoundException.cs`
- `BingoSim.Core/Interfaces/ITeamRepository.cs`

**Application**

- `BingoSim.Application/DTOs/TeamResponse.cs` (and optionally TeamDetailResponse or reuse)
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
- `BingoSim.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddTeamsAndStrategy.cs` (and Designer + snapshot updates)

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
- `Tests/BingoSim.Application.UnitTests/Services/TeamServiceTests.cs`
- `Tests/BingoSim.Application.UnitTests/Validators/CreateTeamRequestValidatorTests.cs`
- `Tests/BingoSim.Application.UnitTests/Validators/UpdateTeamRequestValidatorTests.cs`
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/TeamRepositoryTests.cs`
- `Tests/BingoSim.Web.Tests/Pages/Events/EventTeamsPageTests.cs` (optional)

### Modify

- `BingoSim.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<Team> Teams`, `DbSet<TeamPlayer> TeamPlayers`, `DbSet<StrategyConfig> StrategyConfigs`.
- `BingoSim.Infrastructure/DependencyInjection.cs` — register `ITeamRepository` → `TeamRepository`, `ITeamService` → `TeamService`.
- `BingoSim.Web/Components/Pages/Events/Events.razor` — add "Draft teams" (or "Teams") link per event row, e.g. link to `/events/@evt.Id/teams`.
- `BingoSim.Web/Program.cs` (or service registration) — ensure ITeamService is registered (if not using DI from Infrastructure only).

---

## Summary

- **Core:** Team (EventId, Name), TeamPlayer (TeamId, PlayerProfileId) with unique (TeamId, PlayerProfileId), StrategyConfig (TeamId 1:1, StrategyKey, ParamsJson). Two placeholder strategy keys: RowRush, GreedyPoints.
- **Application:** GetByEventIdAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync; DTOs and validators; TeamService using ITeamRepository, IEventRepository, IPlayerProfileRepository.
- **Infrastructure:** Tables Teams, TeamPlayers, StrategyConfigs; EF configurations and cascade/delete behavior; TeamRepository.
- **Web:** Event-scoped routes `/events/{EventId}/teams`, create/edit team pages, list teams with delete confirmation; "Draft teams" link from Events list.
- **Tests:** Core entity tests, Application service and validator tests, Infrastructure repository integration tests, optional Web bUnit tests.

No code is written in this plan; implementation follows in a subsequent step.
