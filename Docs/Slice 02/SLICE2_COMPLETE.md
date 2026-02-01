# Slice 2: ActivityDefinitions CRUD - Implementation Complete ✅

## Summary

Successfully implemented end-to-end ActivityDefinition CRUD with Clean Architecture following all project standards. Nested data (ModeSupport, Attempts, GroupScalingBands) is persisted as JSON; no additional abstractions or new projects were introduced.

---

## What Was Implemented

### Domain Layer (BingoSim.Core)

- ✅ `ActivityDefinition` entity (Id, Key, Name, ModeSupport, Attempts, GroupScalingBands, CreatedAt)
- ✅ Enums: `RollScope` (PerPlayer, PerGroup), `TimeDistribution` (Uniform, NormalApprox, Custom)
- ✅ Value objects: `ActivityModeSupport`, `AttemptTimeModel`, `ProgressGrant`, `ActivityOutcomeDefinition`, `ActivityAttemptDefinition`, `GroupSizeBand`
- ✅ `IActivityDefinitionRepository` interface
- ✅ Exceptions: `ActivityDefinitionNotFoundException`, `ActivityDefinitionKeyAlreadyExistsException`

### Application Layer (BingoSim.Application)

- ✅ DTOs: `ActivityDefinitionResponse`, `CreateActivityDefinitionRequest`, `UpdateActivityDefinitionRequest`; nested: `ActivityModeSupportDto`, `AttemptTimeModelDto`, `ProgressGrantDto`, `ActivityOutcomeDefinitionDto`, `ActivityAttemptDefinitionDto`, `GroupSizeBandDto`
- ✅ `IActivityDefinitionService` interface and `ActivityDefinitionService` implementation
- ✅ `ActivityDefinitionMapper` for manual mapping (entity ↔ DTOs)
- ✅ FluentValidation validators for all request and nested DTOs (including enum checks)
- ✅ Structured logging in service operations

### Infrastructure Layer (BingoSim.Infrastructure)

- ✅ `AppDbContext` with ActivityDefinitions DbSet
- ✅ `ActivityDefinitionConfiguration` (EF Core)
  - ModeSupport, Attempts, GroupScalingBands stored as JSON columns
  - Navigation/backing fields for Attempts and GroupScalingBands for deserialization
- ✅ `ActivityDefinitionRepository` implementation
- ✅ `DependencyInjection` registration for repository and service
- ✅ Migration: `AddActivityDefinitions` (table + indexes on Key, CreatedAt)

### Presentation Layer (BingoSim.Web)

- ✅ `/activities` — List all ActivityDefinitions
- ✅ `/activities/create` — Create new ActivityDefinition (dynamic attempts, outcomes, grants, bands)
- ✅ `/activities/{id}/edit` — Edit existing ActivityDefinition (pre-populated form)
- ✅ Delete confirmation via shared `DeleteConfirmationModal` (activity name in message)
- ✅ Sidebar navigation "Activities" link
- ✅ Styling consistent with Slice 1 (Activities.razor.css, ActivityCreate.razor.css, ActivityEdit.razor.css)

### Tests

- ✅ **Core unit tests** — ActivityDefinition entity; ActivityModeSupport, ActivityAttemptDefinition, ActivityOutcomeDefinition, AttemptTimeModel, ProgressGrant, GroupSizeBand value objects
- ✅ **Application unit tests** — ActivityDefinitionService (GetAll, GetById, Create, Update, Delete, key uniqueness); ActivityDefinitionMapper; Create/Update request validators
- ✅ **Integration tests** — ActivityDefinitionRepository (Add, GetById, GetByKey, GetAll, Update, Delete, Exists; nested JSON round-trip) — requires Docker

---

## Files Created (Slice 2)

### Core

```
BingoSim.Core/
├── Entities/ActivityDefinition.cs
├── Enums/RollScope.cs
├── Enums/TimeDistribution.cs
├── ValueObjects/
│   ├── ActivityModeSupport.cs
│   ├── AttemptTimeModel.cs
│   ├── ProgressGrant.cs
│   ├── ActivityOutcomeDefinition.cs
│   ├── ActivityAttemptDefinition.cs
│   └── GroupSizeBand.cs
├── Interfaces/IActivityDefinitionRepository.cs
└── Exceptions/
    ├── ActivityDefinitionNotFoundException.cs
    └── ActivityDefinitionKeyAlreadyExistsException.cs
```

### Application

```
BingoSim.Application/
├── DTOs/
│   ├── ActivityDefinitionResponse.cs
│   ├── CreateActivityDefinitionRequest.cs
│   ├── UpdateActivityDefinitionRequest.cs
│   ├── ActivityModeSupportDto.cs
│   ├── AttemptTimeModelDto.cs
│   ├── ProgressGrantDto.cs
│   ├── ActivityOutcomeDefinitionDto.cs
│   ├── ActivityAttemptDefinitionDto.cs
│   └── GroupSizeBandDto.cs
├── Interfaces/IActivityDefinitionService.cs
├── Services/ActivityDefinitionService.cs
├── Mapping/ActivityDefinitionMapper.cs
└── Validators/
    ├── CreateActivityDefinitionRequestValidator.cs
    ├── UpdateActivityDefinitionRequestValidator.cs
    ├── ActivityModeSupportDtoValidator.cs
    ├── ActivityAttemptDefinitionDtoValidator.cs
    ├── ActivityOutcomeDefinitionDtoValidator.cs
    ├── AttemptTimeModelDtoValidator.cs
    ├── ProgressGrantDtoValidator.cs
    └── GroupSizeBandDtoValidator.cs
```

### Infrastructure

```
BingoSim.Infrastructure/
├── Persistence/
│   ├── Configurations/ActivityDefinitionConfiguration.cs
│   ├── Repositories/ActivityDefinitionRepository.cs
│   └── Migrations/
│       ├── 20260201053555_AddActivityDefinitions.cs
│       ├── 20260201053555_AddActivityDefinitions.Designer.cs
│       └── (AppDbContextModelSnapshot updated)
└── DependencyInjection.cs (updated)
```

### Web

```
BingoSim.Web/Components/
├── Pages/Activities/
│   ├── Activities.razor
│   ├── Activities.razor.css
│   ├── ActivityCreate.razor
│   ├── ActivityCreate.razor.css
│   ├── ActivityEdit.razor
│   └── ActivityEdit.razor.css
└── Layout/MainLayout.razor (updated: Activities link)
```

### Tests

```
Tests/
├── BingoSim.Core.UnitTests/
│   ├── Entities/ActivityDefinitionTests.cs
│   └── ValueObjects/
│       ├── ActivityModeSupportTests.cs
│       ├── ActivityAttemptDefinitionTests.cs
│       ├── ActivityOutcomeDefinitionTests.cs
│       ├── AttemptTimeModelTests.cs
│       ├── ProgressGrantTests.cs
│       └── GroupSizeBandTests.cs
├── BingoSim.Application.UnitTests/
│   ├── Services/ActivityDefinitionServiceTests.cs
│   ├── Mapping/ActivityDefinitionMapperTests.cs
│   └── Validators/
│       ├── CreateActivityDefinitionRequestValidatorTests.cs
│       └── UpdateActivityDefinitionRequestValidatorTests.cs
└── BingoSim.Infrastructure.IntegrationTests/
    └── Repositories/ActivityDefinitionRepositoryTests.cs
```

### Modified Files (Slice 2)

- `BingoSim.Infrastructure/Persistence/AppDbContext.cs` — ActivityDefinitions DbSet
- `BingoSim.Infrastructure/DependencyInjection.cs` — Register IActivityDefinitionRepository, IActivityDefinitionService
- `BingoSim.Web/Components/Layout/MainLayout.razor` — "Activities" nav link
- `BingoSim.Web/Components/_Imports.razor` — Application DTOs/Interfaces if needed
- `BingoSim.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` — ActivityDefinitions model

---

## How to Run

### 1. Start PostgreSQL

```bash
cd /home/ohio/Projects/bingosim
docker compose up -d postgres
```

Wait for healthy:

```bash
docker compose ps
```

### 2. Run Tests

```bash
# Unit tests only (no Docker required)
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# All tests (integration tests require Docker)
dotnet test
```

### 3. Run the Web Application

```bash
dotnet run --project BingoSim.Web
```

App will auto-apply migrations on startup (Development) and listen on `https://localhost:5001` (or shown port).

### 4. Verify in Browser

1. Open `https://localhost:5001`
2. Click **"Activities"**
3. Click **"Create Activity"**
4. Fill Key, Name; Mode Support (Solo/Group); add Attempt(s) with outcomes and grants; optionally add Group Scaling Bands
5. Submit → redirects to list
6. Click **"Edit"** → change fields → **"Save"**
7. Click **"Delete"** → confirm in modal → activity removed
8. Refresh → data persists

---

## Architecture Compliance

### ✅ Clean Architecture

- Core has no infrastructure dependencies
- Application depends only on Core
- Infrastructure implements Core/Application interfaces
- Web depends on Application (and Infrastructure via DI)

### ✅ Domain-Driven Design

- Rich entity and value objects with invariants
- Domain exceptions for not found and duplicate key
- No anemic models

### ✅ Standards

- File-scoped namespaces; primary constructors; collection expressions `[]`
- Manual mapping; FluentValidation; structured logging
- Async/await throughout (no `.Result` or `.Wait()`)

---

## Database Schema (Slice 2)

**Table: ActivityDefinitions**

```sql
CREATE TABLE "ActivityDefinitions" (
    "Id" uuid PRIMARY KEY,
    "Key" varchar(100) NOT NULL UNIQUE,
    "Name" varchar(200) NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "ModeSupport" jsonb NOT NULL,
    "Attempts" jsonb,
    "GroupScalingBands" jsonb
);

CREATE INDEX "IX_ActivityDefinitions_Key" ON "ActivityDefinitions" ("Key");
CREATE INDEX "IX_ActivityDefinitions_CreatedAt" ON "ActivityDefinitions" ("CreatedAt" DESC);
```

**ModeSupport JSON:** `{ "SupportsSolo", "SupportsGroup", "MinGroupSize", "MaxGroupSize" }`  
**Attempts JSON:** Array of attempt objects (Key, RollScope, TimeModel, Outcomes with Grants).  
**GroupScalingBands JSON:** Array of bands (MinSize, MaxSize, TimeMultiplier, ProbabilityMultiplier).

---

## Key Features

### Multiple Loot Lines (Attempts)

- Each attempt: Key, RollScope (Per Player / Per Group), TimeModel (BaselineTimeSeconds, Distribution, VarianceSeconds)
- Each attempt: one or more outcomes with Weight (num/denom) and one or more grants (DropKey, Units ≥ 1)
- At least one attempt required; attempt keys unique within activity

### Group Scaling Bands

- Optional list of bands: MinSize, MaxSize, TimeMultiplier, ProbabilityMultiplier
- Persisted as JSON; ready for simulation to match group size to band (future slice)

### Delete Confirmation

- Same `DeleteConfirmationModal` as Slice 1; message includes activity name

### Form Validation

- FluentValidation for request and nested DTOs (key/name required, attempt/outcome/grant rules, enum values)
- Consolidated validation errors in alert on Create/Edit

---

## Next Steps (Future Slices)

From `06_Acceptance_Tests.md`:

1. ✅ Slice 1: PlayerProfiles CRUD (COMPLETE)
2. ✅ Slice 2: ActivityDefinitions CRUD (COMPLETE)
3. **Slice 3:** Event & Board Configuration (Events, Rows, Tiles, TileActivityRules referencing ActivityDefinition)
4. Slice 4+: Batch start, local execution, results, strategies, multi-activity tiles, per-group roll scope, distributed workers, seed input, metrics

---

## Troubleshooting

### Docker Permission Denied

```bash
sudo usermod -aG docker $USER
newgrp docker
```

### Port Already in Use

```bash
sudo lsof -i :5432
docker compose down
```

### Migration Issues

- Ensure `AddActivityDefinitions` migration is applied (auto on Web startup in Development)
- If using `dotnet ef database update` on empty DB, ensure `__EFMigrationsHistory` exists (see `EnsureMigrationsHistory.sql` or Web bootstrap in Program.cs)

### Blazor Nested Form Binding

- Activity Create/Edit use native `<input>`/`<select>` with `@bind` for nested attempt/outcome/grant fields to avoid `FieldExpression` index issues. Keep this pattern if adding more nested fields.

---

## Success Criteria ✅

All acceptance criteria from `06_Acceptance_Tests.md` Section 2 met:

- ✅ Create ActivityDefinition with multiple loot lines (attempts, outcomes, grants; PerPlayer/PerGroup)
- ✅ Team-scoped rare roll (RollScope=PerGroup; weighted outcomes)
- ✅ Group scaling bands (MinSize..MaxSize; time/probability multipliers; persisted as JSON)
- ✅ Edit and delete with confirmation; changes persist; removed from list
- ✅ Nested ActivityDefinition data persisted as JSON (no normalization required for v1)
- ✅ Tests verify domain, service, repository, and validation

---

**Slice:** 2 - ActivityDefinitions CRUD  
**Status:** ✅ COMPLETE  
**Docs:** `Docs/Slice 02/` — ACCEPTANCE_REVIEW.md, QUICKSTART.md, REVIEW_SUMMARY.md, SLICE2_COMPLETE.md
