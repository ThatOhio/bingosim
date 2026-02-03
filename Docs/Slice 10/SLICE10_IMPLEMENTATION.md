# Slice 10: WeeklySchedule Usage — Implementation

**Completed:** February 2, 2025  
**Review:** February 2, 2025 — attempt-end boundary fix, added boundary test  
**Refactor:** February 2, 2025 — precomputed daily windows, explicit timezone, debug logging

## Summary

Player weekly schedules are now enforced during simulation. Players act only during their online sessions (ET). Offline players cannot start attempts or join groups. Event start time is derived from batch creation time (converted to ET). Schedule data is sourced from the snapshot for reproducibility.

---

## Scope Trim (vs Original Plan)

- **No Event.StartTime** — No migrations or Event entity changes
- **No Event create/edit UI changes**
- **EventStartTimeEt** — Derived from `Batch.CreatedAt` converted to ET at batch start
- **Schedule enforcement** — Purely within simulation/snapshot; null/empty schedule = always online
- **Fast-forward** — Earliest next session start across all players when queue empty
- **Attempt-end policy** — Do not start attempt if it would end at or past session end (`attemptEndEt >= sessionEnd`)

---

## Review Fixes (Feb 2, 2025)

- **Attempt-end boundary:** Use `>=` instead of `>` so attempts ending exactly at session end are skipped (session is [start, end); at end the player is offline).
- **AttemptEndPolicy_AttemptWouldEndAtOrPastSessionEnd_Skipped:** New test verifies 1-min session + 60s attempts → no attempts scheduled (boundary skip) and determinism.

### Refactor (Feb 2, 2025)

- **DailyWindows:** Precomputed (day, startMin, endMin) intervals for fast lookups. Midnight-spanning sessions split into two intervals. `IsOnlineAt` and `GetCurrentSessionEnd` use precomputed structure.
- **Explicit timezone:** `ScheduleEvaluator.EasternTimeZone` (America/New_York) as public constant.
- **GetNextSessionStart:** Uses raw sessions (not precomputed) to avoid duplicate starts from midnight-spanning splits.
- **Debug logging:** `SimulationRunner` accepts optional `ILogger<SimulationRunner>`. Logs at Debug: fast-forward when queue empty, attempt skipped at session boundary.

---

## Files Changed / Added

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Schedule/ScheduledSessionSnapshotDto.cs` | DTO: DayOfWeek, StartLocalTimeMinutes, DurationMinutes |
| `BingoSim.Application/Simulation/Schedule/WeeklyScheduleSnapshotDto.cs` | DTO: Sessions list (null/empty = always online) |
| `BingoSim.Application/Simulation/Schedule/ScheduleEvaluator.cs` | Pure functions; DailyWindows precomputed intervals; EasternTimeZone constant; IsOnlineAt, GetCurrentSessionEnd, GetNextSessionStart, SimTimeToEt, EtToSimTime, GetEarliestNextSessionStart |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleEvaluatorTests.cs` | Unit tests for schedule evaluation |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleSimulationIntegrationTests.cs` | Integration tests: schedule comparison, determinism, backward compat, group constraints, attempt-end boundary |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/PlayerSnapshotDto.cs` | Added `Schedule?: WeeklyScheduleSnapshotDto` |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotDto.cs` | Added `EventStartTimeEt?: string` |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `EventStartTimeEt` from `eventStartTimeUtc` (batch.CreatedAt); populate `PlayerSnapshotDto.Schedule` from `profile.WeeklySchedule`; new overload `BuildSnapshotJsonAsync(eventId, eventStartTimeUtc?)` |
| `BingoSim.Application/Services/SimulationBatchService.cs` | Pass `batch.CreatedAt` to `BuildSnapshotJsonAsync` |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Schedule integration: filter online players; attempt-end policy; fast-forward when queue empty; optional ILogger for debug logging |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Added `DistributedBatch_WithScheduleSnapshot_CompletesSuccessfully` |

---

## Implementation Details

### 1. Schedule Evaluation API

- **DailyWindows.Build(schedule):** Precomputes (day, startMin, endMin) intervals. Midnight-spanning sessions split into two intervals. Used for fast `Contains` and session-end lookup.
- **IsOnlineAt(schedule, timestampEt):** Builds DailyWindows, returns true if any interval contains the (day, minutes) in ET. Null/empty schedule ⇒ always online.
- **GetCurrentSessionEnd(schedule, timestampEt):** Uses precomputed intervals; returns session end if player is online; null if offline.
- **GetNextSessionStart(schedule, fromEt):** Uses raw sessions (avoids duplicate starts from midnight-spanning splits).
- **SimTimeToEt / EtToSimTime:** Map sim seconds ↔ ET timestamps.
- **GetEarliestNextSessionStart(snapshot, currentEt):** Earliest session start across all players for fast-forward.
- **EasternTimeZone:** `America/New_York` — explicit timezone for all schedule interpretation.

Sessions use `DayOfWeek` (0–6), `StartLocalTimeMinutes` (0–1439), `DurationMinutes`. Midnight-spanning sessions supported.

### 2. Event Start Time

- `EventStartTimeEt` is set from `batch.CreatedAt` converted to ET when building the snapshot.
- Stored as ISO8601 string in snapshot JSON.
- Null/absent ⇒ no schedule enforcement (all players always online).

### 3. SimulationRunner Integration

- Parse `EventStartTimeEt` at start; if null, skip all schedule checks.
- **ScheduleEventsForPlayers:** Filter `playerIndicesToSchedule` to online players only before assignments.
- **Attempt-end policy:** Before enqueueing, compute `attemptEndEt`. For each group member with a schedule, get `GetCurrentSessionEnd`. If any has `attemptEndEt >= sessionEnd`, skip scheduling the attempt (attempt must finish strictly inside session).
- **Fast-forward:** When queue empty and `simTime < durationSeconds`, compute `GetEarliestNextSessionStart`, advance `simTime`, re-schedule for all teams, continue loop.

### 4. Group Formation

- Group formation uses the same online filter: only online players are in `assignments` and `sameWork`.
- If `minGroupSize` cannot be met with online players, the group is not formed.

### 5. Backward Compatibility

- Snapshots without `EventStartTimeEt` or with null `Schedule` on players behave as before (all players always online).
- Existing tests and batches continue to work.

### 6. Debug Logging

- `SimulationRunner` accepts optional `ILogger<SimulationRunner>`. When provided and LogLevel.Debug enabled:
  - Fast-forward: logs simTime advance when queue empty
  - Attempt skipped: logs when attempt would end at/past session end

---

## How to Run Tests

```bash
# Application unit tests (includes schedule tests)
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj

# Schedule-specific tests
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj --filter "ScheduleEvaluatorTests|ScheduleSimulationIntegrationTests"

# Integration tests (requires Docker for Postgres)
dotnet test Tests/BingoSim.Infrastructure.IntegrationTests/BingoSim.Infrastructure.IntegrationTests.csproj

# Distributed schedule smoke test
dotnet test Tests/BingoSim.Infrastructure.IntegrationTests/BingoSim.Infrastructure.IntegrationTests.csproj --filter "DistributedBatch_WithScheduleSnapshot"

# All tests
dotnet test
```

---

## Manual Verification (Using Seeded Data)

1. **Seed the database:**
   ```bash
   dotnet run --project BingoSim.Seed
   ```

2. **Edit a PlayerProfile** to add a weekly schedule (e.g. one session Mon–Fri 18:00–20:00 ET).

3. **Start the app:**
   ```bash
   dotnet run --project BingoSim.Web
   ```

4. **Create or use an Event** with teams. Ensure at least one team has a player with a non-empty schedule.

5. **Run a batch** (e.g. 50 runs) with a fixed seed.

6. **Verify:**
   - Batch completes successfully
   - Compare teams: a team with a player who has a restrictive schedule (e.g. 1h/day) should progress less than a team with always-online players
   - Re-run with same seed → identical results (determinism)

7. **Distributed mode:** Start Worker (`dotnet run --project BingoSim.Worker`) and run with Distributed execution; batch should complete and results reflect schedule behavior.

---

## Version Tolerance

- Snapshots without `EventStartTimeEt` → no schedule enforcement
- Snapshots without `Schedule` on players → always online
- Empty `Sessions` list → always online

---

*End of implementation document.*
