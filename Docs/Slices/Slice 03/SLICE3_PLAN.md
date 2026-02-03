# Slice 3: Event & Board Configuration (CRUD) — Implementation Plan

**Scope:** Events list, Create Event, Edit Event (nested rows/tiles/rules), Delete Event with confirmation.  
**Out of scope:** Teams, Strategy assignment, Run Simulations, Workers, Messaging, Simulation Engine, Results, Aggregations.

**Source of truth:** `Docs/06_Acceptance_Tests.md` — Section "3) Event & Board Configuration" and Section "9) UI Expectations (Events supports create/edit/delete...)".

---

## 1) Core: Entity and Value Object Shapes and Invariants

### 1.1 Event (Entity)

- **Location:** `BingoSim.Core/Entities/Event.cs`
- **Properties:**  
  - `Id` (Guid), `Name` (string), `Duration` (TimeSpan), `UnlockPointsRequiredPerRow` (int), `CreatedAt` (DateTimeOffset)  
  - `Rows` — `IReadOnlyList<Row>` backed by private `_rows` (ordered by `Index`)
- **Invariants:**
  - Name not null/empty (trimmed).
  - Duration > TimeSpan.Zero.
  - UnlockPointsRequiredPerRow >= 0 (effective value; global default 5 is UI concern).
  - Rows ordered by `Index`; no duplicate indices (enforce when adding/updating rows).
  - **Structural invariant:** Every row has exactly 4 tiles with Points {1, 2, 3, 4}; tile keys unique across the entire event.
- **Behavior:**  
  - Constructor, `UpdateName`, `UpdateDuration`, `SetUnlockPointsRequiredPerRow`, `SetRows(IEnumerable<Row>)`.  
  - `SetRows` validates: each row has exactly 4 tiles with points 1,2,3,4; all tile keys across event are distinct; throws on violation.

### 1.2 Row (Value Object)

- **Location:** `BingoSim.Core/ValueObjects/Row.cs`
- **Properties:** `Index` (int), `Tiles` — `IReadOnlyList<Tile>` (exactly 4 elements, Points 1,2,3,4).
- **Invariants:**
  - Index >= 0.
  - Tiles count == 4; exactly one tile per point value 1,2,3,4.
- **Behavior:** Constructor that accepts index and list of 4 tiles; validates tile count and point set.

### 1.3 Tile (Value Object)

- **Location:** `BingoSim.Core/ValueObjects/Tile.cs`
- **Properties:** `Key` (string), `Name` (string), `Points` (int, 1–4), `RequiredCount` (int), `AllowedActivities` — `IReadOnlyList<TileActivityRule>` (at least one). Optionally `RowIndex` (int) for convenience (set from parent Row when mapping from DTOs; need not be persisted if Row structure implies it).
- **Invariants:**
  - Key/Name not empty.
  - Points in {1, 2, 3, 4}; RequiredCount >= 1.
  - AllowedActivities.Count >= 1.
- **Behavior:** Constructor and/or factory; private parameterless ctor for EF JSON.

### 1.4 TileActivityRule (Value Object)

- **Location:** `BingoSim.Core/ValueObjects/TileActivityRule.cs`
- **Properties:**  
  - `ActivityDefinitionId` (Guid) — **source of truth** for all logic and joins; reference to Activity Library (Core does not reference ActivityDefinition entity; reference by id only).  
  - `ActivityKey` (string) — **denormalized display/debug aid**; populated at create/update from the selected ActivityDefinition and persisted as part of the Event configuration JSON; UI may use ActivityKey for display if resolution of the ActivityDefinition fails.  
  - `AcceptedDropKeys` — `IReadOnlyList<string>`.  
  - `Requirements` — `IReadOnlyList<Capability>` (eligibility gates; reuse existing `Capability`).  
  - `Modifiers` — `IReadOnlyList<ActivityModifierRule>`.  
  - `DropKeyWeights` — optional `IReadOnlyDictionary<string, int>?` only if we add it for simulation later; **do not add in Slice 3** unless already present elsewhere. Omit for now.
- **Invariants:** ActivityDefinitionId != default; ActivityKey not null (can be empty string if resolution fails); AcceptedDropKeys not null (can be empty). Requirements/Modifiers not null.
- **Rules:** Do not replace or remove ActivityDefinitionId. Do not introduce additional normalization or joins; ActivityKey is a simple duplicated value for display/debug only.

### 1.5 ActivityModifierRule (Value Object)

- **Location:** `BingoSim.Core/ValueObjects/ActivityModifierRule.cs`
- **Properties:** `Capability` (reuse `Capability`), `TimeMultiplier` (decimal?), `ProbabilityMultiplier` (decimal?).
- **Invariants:** At least one of TimeMultiplier or ProbabilityMultiplier present; multipliers > 0 when set. Capability not null.

### 1.6 Core Exceptions and Repository Interface

- **EventNotFoundException** — `BingoSim.Core/Exceptions/EventNotFoundException.cs`
- **IEventRepository** — `BingoSim.Core/Interfaces/IEventRepository.cs`  
  - `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`.  
  - No GetByKey (events have no unique key like activities); optional `GetByNameAsync` only if needed for duplicate-name checks (not required for minimal slice).

---

## 2) Application: DTOs, Validators, Mapping, Services

### 2.1 DTOs

- **EventResponse** — Id, Name, Duration (e.g. total minutes or TimeSpan serialized as string/ticks for API), UnlockPointsRequiredPerRow, Rows (list of RowDto), CreatedAt.  
  - **RowDto** — Index, Tiles (list of TileDto).  
  - **TileDto** — Key, Name, Points, RequiredCount, AllowedActivities (list of TileActivityRuleDto).  
  - **TileActivityRuleDto** — ActivityDefinitionId (source of truth for logic), ActivityKey (string, **persisted** denormalized display/debug aid; populated at create/update from the selected ActivityDefinition; in response, comes from stored Event JSON; UI may use ActivityKey if resolution fails), ActivityName (optional, resolved from ActivityDefinition when available for display), AcceptedDropKeys, Requirements (list of CapabilityDto, reuse), Modifiers (list of ActivityModifierRuleDto).  
  - **ActivityModifierRuleDto** — Capability (CapabilityDto), TimeMultiplier?, ProbabilityMultiplier?.
- **CreateEventRequest** — Name, Duration, UnlockPointsRequiredPerRow, Rows (list of RowDto).
- **UpdateEventRequest** — Same shape as Create (full replace of rows/tiles/rules).

All nested DTOs mirror Core value objects; use existing `CapabilityDto` for Requirements and Modifier capability.

### 2.2 Validators (minimal guardrails; structural invariants enforced)

- **CreateEventRequestValidator** — Name required/max length; Duration > 0; UnlockPointsRequiredPerRow >= 0; Rows not null; **custom rule:** each row has exactly 4 tiles with points 1,2,3,4 and tile keys unique across event.
- **UpdateEventRequestValidator** — Same as create.
- **RowDtoValidator** — Index >= 0; Tiles count == 4; tile points set {1,2,3,4}.
- **TileDtoValidator** — Key/Name required, Points in {1..4}, RequiredCount >= 1, AllowedActivities not null and not empty.
- **TileActivityRuleDtoValidator** — ActivityDefinitionId not empty; AcceptedDropKeys/Requirements/Modifiers not null.
- **ActivityModifierRuleDtoValidator** — Capability required; at least one multiplier set and > 0.

Validators can live in `BingoSim.Application/Validators/` with names like `CreateEventRequestValidator`, `UpdateEventRequestValidator`, `RowDtoValidator`, `TileDtoValidator`, `TileActivityRuleDtoValidator`, `ActivityModifierRuleDtoValidator`.

### 2.3 Mapping

- **EventMapper** (static) in `BingoSim.Application/Mapping/EventMapper.cs`:  
  - `ToResponse(Event entity, IReadOnlyDictionary<Guid, ActivityDefinition>? activityLookup)` — map entity to EventResponse; for each TileActivityRule use **persisted ActivityKey** from entity for TileActivityRuleDto.ActivityKey; optionally set ActivityName from activityLookup when resolution succeeds (fallback: display ActivityKey if resolution fails).  
  - `ToEntity(CreateEventRequest, IReadOnlyDictionary<Guid, string>? activityKeyById)` / `ToEntity(UpdateEventRequest, ...)` — map request to new Event or to updated rows/tiles/rules (replace Rows entirely); when building TileActivityRule value objects, pass both **ActivityDefinitionId** (from request) and **ActivityKey** (from activityKeyById lookup, populated at create/update from the selected ActivityDefinition).  
  - Helpers: Row ↔ RowDto, Tile ↔ TileDto, TileActivityRule ↔ TileActivityRuleDto (entity already has ActivityKey; DTO gets it from entity or from lookup for response), ActivityModifierRule ↔ ActivityModifierRuleDto.  
- Service at create/update: before calling mapper ToEntity, resolve all ActivityDefinitionIds in the request to their Keys (batch load ActivityDefinitions), build activityKeyById dictionary, pass to mapper so TileActivityRule entities get ActivityKey set. Service loads ActivityDefinitions when building response to optionally fill ActivityName; **ActivityKey in response always comes from persisted Event JSON** (no join).

### 2.4 Service

- **IEventService** — `GetAllAsync`, `GetByIdAsync`, `CreateAsync(CreateEventRequest)`, `UpdateAsync(Guid, UpdateEventRequest)`, `DeleteAsync(Guid)`.
- **EventService** — uses IEventRepository and IActivityDefinitionRepository.  
  - Create: resolve ActivityDefinitionIds in request to ActivityKeys (batch load activities), pass activityKeyById to mapper; map request to Event (TileActivityRules get ActivityDefinitionId + ActivityKey), validate (or rely on entity invariants), AddAsync.  
  - Update: resolve ActivityDefinitionIds in request to ActivityKeys (batch load activities), pass activityKeyById to mapper; load event, replace Name/Duration/UnlockPointsRequiredPerRow/Rows via entity methods (Rows include persisted ActivityKey per rule), UpdateAsync.  
  - GetById: load event; map to response using **persisted ActivityKey** from entity for each rule; optionally batch-load ActivityDefinitions to fill ActivityName for display (if resolution fails, UI uses ActivityKey).

---

## 3) Infrastructure: Persistence Plan (DbContext, EF Mappings, JSON)

- **DbContext:** Add `DbSet<Event> Events`.
- **Table:** `Events` — columns: `Id`, `Name`, `Duration` (e.g. stored as `long` ticks or `TimeSpan` if Npgsql supports), `UnlockPointsRequiredPerRow`, `CreatedAt`, and **one JSON column** for the whole board: e.g. `Rows` (or `Board`).
- **JSON strategy (same as Slices 1 & 2):**
  - Event owns a single JSON column for **Rows** (owned collection).  
  - Each Row is an owned type with `Index` and a nested JSON for **Tiles**.  
  - Each Tile is an owned type with Key, Name, Points, RequiredCount, and nested JSON for **AllowedActivities** (TileActivityRules).  
  - Each TileActivityRule: **ActivityDefinitionId** (Guid), **ActivityKey** (string, denormalized, persisted in Event JSON), AcceptedDropKeys (array of string), Requirements (array of Capability — reuse same shape as PlayerProfile Capabilities), Modifiers (array of ActivityModifierRule).  
  - ActivityModifierRule: Capability (object), TimeMultiplier?, ProbabilityMultiplier?.
- **EF configuration:** `EventConfiguration` in `BingoSim.Infrastructure/Persistence/Configurations/EventConfiguration.cs`:  
  - Map `Event` to table `Events`; key `Id`; scalar properties with length/precision as needed.  
  - `OwnsMany(e => e.Rows, ...)` with `ToJson("Rows")`; on each Row owned type, `OwnsMany(r => r.Tiles, ...)` nested in same JSON or as nested structure so that the whole Rows column is one JSON document (array of objects, each with Index and Tiles array).  
  - Each Tile: OwnsMany(t => t.AllowedActivities, ...) with TileActivityRule mapped (**ActivityDefinitionId**, **ActivityKey**, AcceptedDropKeys, Requirements as list of Capability, Modifiers as list of ActivityModifierRule).  
  - Use `HasField("_rows")` for Event’s private backing field if needed.
- **Repository:** `EventRepository` in `BingoSim.Infrastructure/Persistence/Repositories/EventRepository.cs` — implement `IEventRepository` (GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync, DeleteAsync, ExistsAsync). No GetByKey.
- **Migration:** New migration adding `Events` table with JSON column(s) as above.

**Note:** Core entity `Event` exposes `Rows` as `IReadOnlyList<Row>`; Row and Tile and rules are value types/owned; EF will serialize/deserialize the entire Rows graph into one or more JSON columns. Prefer single JSON column "Rows" containing array of row objects (each with Index + Tiles array) for simplicity.

---

## 4) Web UI: Pages and Form Model Approach

### 4.1 Routes and Layout

- **Events list:** `@page "/events"` — `Pages/Events/Events.razor`.
- **Create Event:** `@page "/events/create"` — `Pages/Events/EventCreate.razor`.
- **Edit Event:** `@page "/events/{Id:guid}/edit"` — `Pages/Events/EventEdit.razor`.
- Add "Events" nav link in `MainLayout.razor` (alongside Players, Activities).

### 4.2 Events List Page

- Page title: "Events" (or "Event & Board Configuration").
- Table: columns e.g. Name, Duration, Unlock Points Per Row, Row Count, Created, Actions (Edit, Delete).
- Buttons: "Create Event", Edit (navigate to edit), Delete (opens DeleteConfirmationModal).
- Reuse `DeleteConfirmationModal` for delete confirmation; on confirm call `IEventService.DeleteAsync`, then reload list.

### 4.3 Create Event Page

- Form: Name, Duration (e.g. hours + minutes or total TimeSpan input), UnlockPointsRequiredPerRow (number, default 5).
- **Rows section:** Add/remove rows; each row has Index (editable or auto) and **exactly 4 tile slots** (Points 1, 2, 3, 4).  
  - Each tile: Key, Name, Points (fixed per slot: 1, 2, 3, 4), RequiredCount, and **Allowed Activities** (list of TileActivityRule form models).  
  - For each TileActivityRule: dropdown to select ActivityDefinition (from Activity Library), AcceptedDropKeys (list of strings, add/remove), Requirements (list of Capability keys or CapabilityDto, add/remove), Modifiers (list of ActivityModifierRule form model: capability + optional time/prob multipliers, add/remove).
- Submit: build CreateEventRequest from form model, validate, call `IEventService.CreateAsync`, redirect to `/events`.

### 4.4 Edit Event Page

- Load event by Id via `IEventService.GetByIdAsync`; if not found show "Event not found".
- Form model: same structure as create (EventFormModel with Rows → RowFormModel with Tiles → TileFormModel with AllowedActivities → TileActivityRuleFormModel with AcceptedDropKeys, Requirements, Modifiers).
- **Editing nested data:**  
  - Add/remove rows (ensure after save each row still has exactly 4 tiles with points 1–4).  
  - Add/remove tiles only in the sense of reordering or replacing; **invariant:** 4 tiles per row with points 1,2,3,4. So UI: each row shows 4 tile editors (for points 1–4).  
  - Add/remove TileActivityRules per tile.  
  - Add/remove AcceptedDropKeys, Requirements, Modifiers per rule.
- Activity dropdown: inject `IActivityDefinitionService`, call `GetAllAsync()` to populate options; store ActivityDefinitionId in each TileActivityRule.
- On submit: build UpdateEventRequest, validate, call `IEventService.UpdateAsync(Id, request)`, redirect to `/events`.

### 4.5 Form Model Structure (mirroring ActivityEdit)

- **EventFormModel** — Name, Duration (e.g. two ints for hours/minutes or one TimeSpan), UnlockPointsRequiredPerRow, List&lt;RowFormModel&gt; Rows.
- **RowFormModel** — Index, List&lt;TileFormModel&gt; Tiles (exactly 4; fixed Points 1,2,3,4 per position or per tile).
- **TileFormModel** — Key, Name, Points (1–4), RequiredCount, List&lt;TileActivityRuleFormModel&gt; AllowedActivities.
- **TileActivityRuleFormModel** — ActivityDefinitionId (Guid), ActivityKey (string; display/fallback; when user selects activity from dropdown, set from selected item; persisted at save—service populates ActivityKey from ActivityDefinition before persisting), List&lt;string&gt; AcceptedDropKeys, List&lt;CapabilityDto&gt; or simple key list for Requirements, List&lt;ActivityModifierRuleFormModel&gt; Modifiers.
- **ActivityModifierRuleFormModel** — Capability (Key + Name or CapabilityDto), TimeMultiplier?, ProbabilityMultiplier?.
- Methods: `EventFormModel.FromResponse(EventResponse)`, `EventFormModel.ToCreateRequest()`, `EventFormModel.ToUpdateRequest()`; same for nested types where needed.

### 4.6 CSS

- Reuse existing pattern: `Events.razor.css`, `EventCreate.razor.css`, `EventEdit.razor.css` (sections, form-group, section-header, add/remove buttons for nested lists), consistent with Activities/Players.

---

## 5) Test Plan

### 5.1 Core Unit Tests (`BingoSim.Core.UnitTests`)

- **EventTests** — Constructor validates name, duration, UnlockPointsRequiredPerRow; SetRows accepts valid rows (4 tiles per row, points 1–4); SetRows throws when row has wrong tile count or wrong points; SetRows throws when tile keys duplicate across event; UpdateName/UpdateDuration/SetUnlockPointsRequiredPerRow.
- **RowTests** — Constructor with 4 tiles and correct points; constructor throws when tile count != 4 or points set != {1,2,3,4}.
- **TileTests** — Constructor validates Key, Name, Points (1–4), RequiredCount, AllowedActivities not empty.
- **TileActivityRuleTests** — Constructor with ActivityDefinitionId, ActivityKey, AcceptedDropKeys, Requirements, Modifiers; invalid ActivityDefinitionId (default Guid); ActivityKey persisted and used for display fallback.
- **ActivityModifierRuleTests** — Constructor; at least one multiplier; multipliers > 0 when set.

### 5.2 Application Unit Tests (`BingoSim.Application.UnitTests`)

- **EventServiceTests** — CreateAsync success and returns id; CreateAsync populates ActivityKey from ActivityDefinition before persist; CreateAsync invalid rows throws or validation fails; UpdateAsync replaces rows and persists ActivityKey per rule; GetByIdAsync returns response with ActivityKey from persisted JSON (and optional ActivityName when resolution succeeds); DeleteAsync removes event; GetByIdAsync not found returns null; UpdateAsync not found throws EventNotFoundException. Use NSubstitute for IEventRepository and IActivityDefinitionRepository.
- **EventMapperTests** (optional) — ToResponse maps entity and uses persisted ActivityKey for TileActivityRuleDto; ToEntity from CreateRequest with activityKeyById produces valid Event with ActivityKey set on each rule.
- **CreateEventRequestValidatorTests** / **UpdateEventRequestValidatorTests** — Valid request passes; missing name fails; invalid row (not 4 tiles or wrong points) fails; duplicate tile key across event fails.

### 5.3 Infrastructure Integration Tests (`BingoSim.Infrastructure.IntegrationTests`)

- **EventRepositoryTests** — AddAsync and GetByIdAsync round-trip event with rows/tiles/rules (including ActivityDefinitionId and ActivityKey on each TileActivityRule); UpdateAsync changes name and rows; DeleteAsync removes event; GetAllAsync returns events ordered (e.g. by CreatedAt). Use real Postgres (test container or local); ensure JSON serialization of Rows/Tiles/AllowedActivities with both ActivityDefinitionId and ActivityKey is correct.

### 5.4 Web Tests (`BingoSim.Web.Tests`)

- **Events list page** — Renders list when events exist; shows empty state when none (bUnit).
- **Event edit page** (optional) — Loads event and displays form with rows/tiles; add/remove rule does not crash. Can be minimal if time-constrained.

---

## 6) Exact List of Files to Create or Modify

### Create

**Core**  
- `BingoSim.Core/Entities/Event.cs`  
- `BingoSim.Core/ValueObjects/Row.cs`  
- `BingoSim.Core/ValueObjects/Tile.cs`  
- `BingoSim.Core/ValueObjects/TileActivityRule.cs`  
- `BingoSim.Core/ValueObjects/ActivityModifierRule.cs`  
- `BingoSim.Core/Exceptions/EventNotFoundException.cs`  
- `BingoSim.Core/Interfaces/IEventRepository.cs`  

**Application**  
- `BingoSim.Application/DTOs/EventResponse.cs`  
- `BingoSim.Application/DTOs/RowDto.cs`  
- `BingoSim.Application/DTOs/TileDto.cs`  
- `BingoSim.Application/DTOs/TileActivityRuleDto.cs`  
- `BingoSim.Application/DTOs/ActivityModifierRuleDto.cs`  
- `BingoSim.Application/DTOs/CreateEventRequest.cs`  
- `BingoSim.Application/DTOs/UpdateEventRequest.cs`  
- `BingoSim.Application/Interfaces/IEventService.cs`  
- `BingoSim.Application/Mapping/EventMapper.cs`  
- `BingoSim.Application/Services/EventService.cs`  
- `BingoSim.Application/Validators/CreateEventRequestValidator.cs`  
- `BingoSim.Application/Validators/UpdateEventRequestValidator.cs`  
- `BingoSim.Application/Validators/RowDtoValidator.cs`  
- `BingoSim.Application/Validators/TileDtoValidator.cs`  
- `BingoSim.Application/Validators/TileActivityRuleDtoValidator.cs`  
- `BingoSim.Application/Validators/ActivityModifierRuleDtoValidator.cs`  

**Infrastructure**  
- `BingoSim.Infrastructure/Persistence/Configurations/EventConfiguration.cs`  
- `BingoSim.Infrastructure/Persistence/Repositories/EventRepository.cs`  
- `BingoSim.Infrastructure/Persistence/Migrations/YYYYMMDDHHMMSS_AddEvents.cs` (and designer/snapshot updates)  

**Web**  
- `BingoSim.Web/Components/Pages/Events/Events.razor`  
- `BingoSim.Web/Components/Pages/Events/Events.razor.css`  
- `BingoSim.Web/Components/Pages/Events/EventCreate.razor`  
- `BingoSim.Web/Components/Pages/Events/EventCreate.razor.css`  
- `BingoSim.Web/Components/Pages/Events/EventEdit.razor`  
- `BingoSim.Web/Components/Pages/Events/EventEdit.razor.css`  

**Tests**  
- `Tests/BingoSim.Core.UnitTests/Entities/EventTests.cs`  
- `Tests/BingoSim.Core.UnitTests/ValueObjects/RowTests.cs`  
- `Tests/BingoSim.Core.UnitTests/ValueObjects/TileTests.cs`  
- `Tests/BingoSim.Core.UnitTests/ValueObjects/TileActivityRuleTests.cs`  
- `Tests/BingoSim.Core.UnitTests/ValueObjects/ActivityModifierRuleTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Services/EventServiceTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Validators/CreateEventRequestValidatorTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Validators/UpdateEventRequestValidatorTests.cs`  
- `Tests/BingoSim.Application.UnitTests/Mapping/EventMapperTests.cs` (optional)  
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/EventRepositoryTests.cs`  
- `Tests/BingoSim.Web.Tests/Pages/Events/EventsPageTests.cs` (optional)  

### Modify

- `BingoSim.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<Event> Events`.  
- `BingoSim.Infrastructure/DependencyInjection.cs` — register `IEventRepository` → `EventRepository`, `IEventService` → `EventService`.  
- `BingoSim.Web/Components/Layout/MainLayout.razor` — add "Events" nav link.  
- `BingoSim.Application` — ensure project reference to Core is present (already is).  
- `BingoSim.Infrastructure` — add reference to Core if not already; ensure Application services and repositories registered.  
- `Tests/BingoSim.Infrastructure.IntegrationTests` — add EventRepositoryTests and ensure test DB/seeding if used.  
- `Tests/BingoSim.Web.Tests` — add Events page test project reference if needed.  

---

## Summary

- **Core:** Event entity with Rows (value objects); Row with 4 Tiles; Tile with Key, Name, Points (1–4), RequiredCount, AllowedActivities (TileActivityRule list); TileActivityRule holds **ActivityDefinitionId** (source of truth for logic/joins) and **ActivityKey** (denormalized, persisted for display/debug; populated at create/update from ActivityDefinition); plus AcceptedDropKeys, Requirements (Capability), Modifiers (ActivityModifierRule). No extra normalization or joins. Invariants: 4 tiles per row with points 1–4; tile keys unique per event.  
- **Application:** Full CRUD DTOs and requests; validators enforcing same invariants; EventMapper with activity resolution; EventService using IEventRepository and IActivityDefinitionRepository.  
- **Infrastructure:** Events table with JSON column for Rows (nested Tiles and TileActivityRules); EventConfiguration (OwnsMany Rows → OwnsMany Tiles → OwnsMany AllowedActivities); EventRepository.  
- **Web:** Events list, EventCreate, EventEdit (nested form models for rows/tiles/rules), delete confirmation; nav link.  
- **Tests:** Core entity/VO tests, Application service and validator tests, Infrastructure repository integration tests, optional Web bUnit tests.

No code is written in this plan; only the above design and file list. Implementation follows this plan in a subsequent step.
