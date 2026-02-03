# Slice 10: WeeklySchedule Usage (Player Availability Windows) — Plan Only

**Scope:** Integrate player weekly schedules into simulation so players act only during their online sessions. Simulation time maps to real-world ET; group formation and attempt scheduling respect player availability.

**Source of truth:**
- `Docs/06_Acceptance_Tests.md` — schedule realism expectations (PlayerProfile with weekly schedule)
- `Docs/02_Domain.md` — PlayerProfile, WeeklySchedule, ScheduledSession model
- `Docs/08_Feature_Audit_2025.md` — schedule exists but is not used; players act 24/7

**Constraints:** Clean Architecture; determinism (same seed + schedule ⇒ same outcomes); backward compatibility for snapshots without schedule data.

---

## 1) Schedule Evaluation API (Pure Functions)

### 1.1 Core Types

**Location:** `BingoSim.Application/Simulation/Schedule/` (new folder)

| Type | Purpose |
|------|---------|
| `ScheduledSessionSnapshotDto` | Snapshot DTO for one session: `DayOfWeek`, `StartLocalTime` (TimeOnly or int minutes-from-midnight), `DurationMinutes` |
| `WeeklyScheduleSnapshotDto` | Snapshot DTO for schedule: `List<ScheduledSessionSnapshotDto> Sessions` |

**Design choice:** Use `int` for `StartLocalTimeMinutes` (0–1439) for JSON simplicity and to avoid TimeOnly serialization quirks across runtimes. Alternatively, use `string` "HH:mm" and parse. **Recommendation:** `StartLocalTimeMinutes` (int) for deterministic, portable snapshot.

### 1.2 Pure Functions

| Function | Signature | Purpose |
|----------|-----------|---------|
| `IsOnlineAt` | `(WeeklyScheduleSnapshotDto schedule, DateTimeOffset timestampEt) → bool` | Returns true if any session covers the given ET timestamp. |
| `GetNextSessionStart` | `(WeeklyScheduleSnapshotDto schedule, DateTimeOffset fromEt) → DateTimeOffset?` | Returns the next session start time at or after `fromEt` (for advancing sim time to next online window). Null if no sessions. |
| `GetSessionEndAt` | `(ScheduledSessionSnapshotDto session, DateTimeOffset sessionStartEt) → DateTimeOffset` | Given a session and the real-world start of that session (derived from `fromEt`), returns the session end time. |
| `GetCurrentSessionEnd` | `(WeeklyScheduleSnapshotDto schedule, DateTimeOffset timestampEt) → DateTimeOffset?` | If the player is online at `timestampEt`, returns the end time of the session containing that moment. Null if offline. Used for attempt-end policy. |

**IsOnlineAt logic:**
- Convert `timestampEt` to ET (America/New_York) if not already.
- Extract `DayOfWeek` and `TimeOfDay` (TimeOnly or minutes-from-midnight).
- For each session:
  - If `session.DayOfWeek != timestamp.DayOfWeek`, check if session spans midnight from previous day (see below).
  - Session window: `[StartLocalTime, EndLocalTime)` where `EndLocalTime = StartLocalTime + DurationMinutes`.
  - If `EndLocalTime <= StartLocalTime` (spans midnight): session covers `[Start, 24:00)` on DayOfWeek and `[00:00, End)` on (DayOfWeek + 1) mod 7.
  - Otherwise: session covers `[Start, End)` on DayOfWeek only.
- Return true if timestamp falls within any session.

**Empty schedule:** If `Sessions` is null or empty, treat as **always online** (backward compatibility; current behavior).

### 1.3 Sim Time ↔ Real-World Mapping

| Function | Signature | Purpose |
|----------|-----------|---------|
| `SimTimeToEt` | `(DateTimeOffset eventStartEt, int simTimeSeconds) → DateTimeOffset` | `eventStartEt + TimeSpan.FromSeconds(simTimeSeconds)`. |
| `EtToSimTime` | `(DateTimeOffset eventStartEt, DateTimeOffset timestampEt) → int` | `(int)(timestampEt - eventStartEt).TotalSeconds`. |

**Timezone:** All real-world timestamps are in America/New_York (ET). Use `TimeZoneInfo.FindSystemTimeZoneById("America/New_York")` for conversions.

---

## 2) Integration Points: "Online Players" in Scheduler/Runner

### 2.1 Snapshot Requirements

- **EventSnapshotDto:** Add `EventStartTimeEt` (DateTimeOffset or ISO8601 string). Required for schedule evaluation. **Backward compat:** If null/absent, treat all players as always online.
- **PlayerSnapshotDto:** Add `Schedule` (WeeklyScheduleSnapshotDto?). If null/absent, treat as always online.
- **EventSnapshotBuilder:** Populate `EventStartTimeEt` from Event (new field) or from batch/request. Populate `PlayerSnapshotDto.Schedule` from `PlayerProfile.WeeklySchedule`.

### 2.2 Event Start Time Source

**Decision:** Add `EventStartTime` (DateTimeOffset) to Event entity. When building snapshot, use `evt.StartTime`. If Event has no StartTime (legacy), use a default: **Monday 00:00 ET** of the week containing batch creation, or require it for schedule-enabled runs.

**Simpler alternative:** Add `EventStartTimeEt` to `StartSimulationBatchRequest` (optional). If provided, use it; else use `Event.StartTime` if present; else use default (e.g. Monday 00:00 ET) and log a warning. For v1, we can add `Event.StartTime` and require it when any team has players with non-empty schedules.

**Recommendation:** Add `Event.StartTime` (DateTimeOffset) to Event. Default in UI: "next Monday 00:00 ET" from today. Snapshot captures it. No change to batch request.

### 2.3 SimulationRunner Integration

| Location | Change |
|----------|--------|
| **Execute()** | Read `EventStartTimeEt` from snapshot. If null, skip all schedule checks (current behavior). |
| **ScheduleEventsForPlayers** | Before considering a player for assignment: 1) Convert `simTime` to ET via `SimTimeToEt(eventStartEt, simTime)`. 2) For each player in `playerIndicesToSchedule`, call `IsOnlineAt(player.Schedule, timestampEt)`. 3) Filter to only online players. Pass only online player indices into the rest of the logic. |
| **Attempt scheduling** | Before enqueueing a SimEvent: 1) Check that the attempt would **finish** within the session (see Policy below). 2) If not, do not schedule the attempt for that player/group at this simTime. |
| **Group formation** | `sameWork` and group formation already filter by eligible players. Add schedule filter: only include players where `IsOnlineAt(schedule, simTimeEt)` is true. If `sameWork` has fewer than `minGroupSize` online players for a group-only activity, do not form the group (skip). |

### 2.4 Advancing Past Offline Periods

**Current loop:** Dequeue event → process → `ScheduleEventsForPlayers` for the players who just completed. All players are always eligible.

**With schedule:** When a player completes an attempt at `simTime`, they may be offline at `simTime`. We only reschedule for players who are **online** at `simTime`. Offline players will not get new work until they come back online. But the main loop advances when **any** event completes. So we can have a situation: all players are offline, but the queue has no events (we didn't schedule anything). The loop would exit with `eventQueue.Count == 0` while `simTime < durationSeconds`.

**Solution:** When the queue is empty and `simTime < durationSeconds`, we must "fast-forward" to the next moment when at least one player is online. Options:
- **A) Next-event heuristic:** Find the minimum `GetNextSessionStart` across all players. Advance `simTime` to that moment (in sim seconds). Re-run initial scheduling for all teams. Enqueue new events. Continue loop.
- **B) Idle advance:** If queue empty and simTime < duration, advance simTime by a fixed step (e.g. 60 seconds) and re-check. Repeat until someone is online or we hit duration. Simpler but may spin.

**Recommendation:** Option A. Add a helper `GetEarliestNextSessionStart(snapshot, currentEt)` that returns the earliest session start across all players at or after `currentEt`. When queue is empty and `simTime < durationSeconds`, compute `nextEt = GetEarliestNextSessionStart(...)`. If null, we're done (no one has sessions = all always online, shouldn't happen). Set `simTime = EtToSimTime(eventStartEt, nextEt)`, then re-run initial scheduling for all teams and continue.

---

## 3) Attempt Crossing Session End — Policy

**Policy (recommended):** **Do not start an attempt if it would not finish inside the player's current session.**

**Rationale:**
- Deterministic: we never "split" an attempt across sessions.
- Simple: no need to model "player goes offline mid-attempt" or partial completion.
- Conservative: players may underutilize the tail of a session, but behavior is predictable.

**Implementation:**
- Before scheduling an attempt for a player (or group), compute `attemptEndTime = simTime + SampleAttemptDuration(...)`.
- Convert `simTime` and `attemptEndTime` to ET.
- For **solo:** Check `IsOnlineAt(player.Schedule, attemptEndEt)` — actually we need "is the player online for the entire interval [simTimeEt, attemptEndEt]"? 
- **Refinement:** The rule is "attempt must finish inside session". So we need: the attempt end time must fall within a session. Check `IsOnlineAt(schedule, attemptEndEt)`. But that's not enough: the attempt could start in one session and end in another. The policy says "would not finish inside session" — meaning the attempt end must be before the session end. So we need: `attemptEndEt` is within the same session that contains `simTimeEt`, and `attemptEndEt < sessionEnd`.

**Simpler formulation:** Do not start the attempt if `attemptEndEt` is **after** the end of the current session. We need a helper: `GetCurrentSessionEnd(schedule, timestampEt) → DateTimeOffset?` — the end time of the session that contains `timestampEt`, or null if offline. Then: `if (GetCurrentSessionEnd(schedule, simTimeEt) is { } end && attemptEndEt > end) → do not start`.

**Final policy:**
1. Compute `attemptEndEt = SimTimeToEt(eventStartEt, simTime + duration)`.
2. For each player in the group, get `sessionEnd = GetCurrentSessionEnd(player.Schedule, simTimeEt)`.
3. If any player has `sessionEnd != null && attemptEndEt > sessionEnd`, do **not** schedule the attempt. (Group: use the minimum session end among members — the group can only run until the first player goes offline.)

**Document in code and in this plan.**

---

## 4) Snapshot Changes

### 4.1 New DTOs

| File | Content |
|------|---------|
| `ScheduledSessionSnapshotDto.cs` | `DayOfWeek`, `StartLocalTimeMinutes` (int 0–1439), `DurationMinutes` |
| `WeeklyScheduleSnapshotDto.cs` | `List<ScheduledSessionSnapshotDto> Sessions` (nullable, empty = always online) |

### 4.2 PlayerSnapshotDto

Add:
- `Schedule?: WeeklyScheduleSnapshotDto` — null or empty = always online.

### 4.3 EventSnapshotDto

Add:
- `EventStartTimeEt?: string` — ISO8601 in ET (e.g. "2025-02-03T00:00:00-05:00"). Null = legacy, treat all always online.

### 4.4 EventSnapshotBuilder

- When building `PlayerSnapshotDto`: Include `Schedule` from `profile.WeeklySchedule` (map Sessions to `ScheduledSessionSnapshotDto`).
- When building `EventSnapshotDto`: Include `EventStartTimeEt` from `evt.StartTime` when non-null. Format as ISO8601 in ET. If null, omit (legacy behavior).

### 4.5 Event Entity

Add:
- `StartTime: DateTimeOffset?` — when the event begins in real-world time (nullable for backward compat). Stored in UTC; interpreted as ET for schedule (user enters in ET, we store).

**Migration:** Add `StartTime` column to Events (nullable `DateTimeOffset?`). Default for existing rows: null. When building snapshot, if `evt.StartTime` is null, omit `EventStartTimeEt` from snapshot → schedule checks disabled, all players always online (backward compat).

---

## 5) Test Plan

### 5.1 Unit Tests: Schedule Evaluation (`ScheduleEvaluatorTests` or similar)

| Test | Description |
|------|-------------|
| `IsOnlineAt_EmptySchedule_ReturnsTrue` | Empty or null schedule ⇒ always online (backward compat). |
| `IsOnlineAt_SingleSession_InsideWindow_ReturnsTrue` | Session Mon 09:00–12:00; timestamp Mon 10:00 ET ⇒ true. |
| `IsOnlineAt_SingleSession_BeforeWindow_ReturnsFalse` | Session Mon 09:00–12:00; timestamp Mon 08:00 ET ⇒ false. |
| `IsOnlineAt_SingleSession_AfterWindow_ReturnsFalse` | Session Mon 09:00–12:00; timestamp Mon 13:00 ET ⇒ false. |
| `IsOnlineAt_SingleSession_AtStart_ReturnsTrue` | Session Mon 09:00–12:00; timestamp Mon 09:00 ET ⇒ true. |
| `IsOnlineAt_SingleSession_AtEndExclusive_ReturnsFalse` | Session Mon 09:00–12:00; timestamp Mon 12:00 ET ⇒ false. |
| `IsOnlineAt_MultipleSessionsSameDay_InsideSecond_ReturnsTrue` | Sessions Mon 09:00–10:00 and Mon 14:00–16:00; timestamp Mon 15:00 ET ⇒ true. |
| `IsOnlineAt_MultipleSessionsSameDay_Between_ReturnsFalse` | Sessions Mon 09:00–10:00 and Mon 14:00–16:00; timestamp Mon 12:00 ET ⇒ false. |
| `IsOnlineAt_DifferentDays_MatchesCorrectDay` | Session Tue 18:00–20:00; timestamp Tue 19:00 ⇒ true; Mon 19:00 ⇒ false. |
| `IsOnlineAt_SessionSpansMidnight_InsideSecondDay_ReturnsTrue` | Session Mon 23:00–01:00 (120 min); timestamp Tue 00:30 ⇒ true. |
| `IsOnlineAt_SessionSpansMidnight_Outside_ReturnsFalse` | Session Mon 23:00–01:00; timestamp Tue 01:30 ⇒ false. |
| `GetNextSessionStart_EmptySchedule_ReturnsNull` | Empty schedule ⇒ null. |
| `GetNextSessionStart_FromBeforeSession_ReturnsSessionStart` | Session Mon 09:00; from=Sun 12:00 ⇒ Mon 09:00. |
| `GetNextSessionStart_FromInsideSession_ReturnsNextSessionOrNull` | Two sessions Mon 09:00 and Mon 14:00; from=Mon 10:00 ⇒ Mon 14:00. |

### 5.2 Simulation Integration Tests (`ScheduleSimulationIntegrationTests`)

| Test | Description |
|------|-------------|
| `PlayerWithOneHourPerDay_ProgressesLessThanAlwaysOnline` | Two teams: Team A = 1 player always online; Team B = 1 player with 1 session 09:00–10:00 ET daily. Same event, 24h duration. Run 50+ sims. Team A mean points >> Team B mean points. |
| `Determinism_SameSeedAndSchedule_SameOutcomes` | Fixed seed, same snapshot (with schedules). Run twice. Results identical (TotalPoints, TilesCompletedCount, RowReached per team). |
| `GroupActivity_OnlyOneEligiblePlayerOnline_DoesNotStartGroup` | Activity requires min 2 players. Two players on team; at simTime T, only 1 is online (other's session ended). Group attempt must not be scheduled. Verify by checking that with tight 1h window for one player, group attempts don't occur when only one online. |
| `BackwardCompat_NoScheduleInSnapshot_AllPlayersAlwaysOnline` | Snapshot with no `EventStartTimeEt` and no `Schedule` on players. Run completes; behavior matches current (no schedule) implementation. |

### 5.3 Distributed Mode Smoke Test

| Test | Description |
|------|-------------|
| `DistributedBatch_WithScheduleSnapshot_CompletesSuccessfully` | Create batch with snapshot that includes `EventStartTimeEt` and player schedules. Run distributed. Batch completes; aggregates present. |

---

## 6) File List (Modify / Create)

### New Files

| File | Purpose |
|------|---------|
| `BingoSim.Application/Simulation/Schedule/ScheduledSessionSnapshotDto.cs` | DTO for one session in snapshot |
| `BingoSim.Application/Simulation/Schedule/WeeklyScheduleSnapshotDto.cs` | DTO for weekly schedule in snapshot |
| `BingoSim.Application/Simulation/Schedule/ScheduleEvaluator.cs` | Pure functions: IsOnlineAt, GetNextSessionStart, GetCurrentSessionEnd, SimTimeToEt, EtToSimTime, GetEarliestNextSessionStart |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleEvaluatorTests.cs` | Unit tests for schedule evaluation |
| `Tests/BingoSim.Application.UnitTests/Simulation/ScheduleSimulationIntegrationTests.cs` | Integration tests for schedule in simulation |

### Modified Files

| File | Changes |
|------|---------|
| `BingoSim.Application/Simulation/Snapshot/PlayerSnapshotDto.cs` | Add `Schedule?: WeeklyScheduleSnapshotDto` |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotDto.cs` | Add `EventStartTimeEt?: string` |
| `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` | Populate `EventStartTimeEt` from Event.StartTime; populate `PlayerSnapshotDto.Schedule` from profile.WeeklySchedule |
| `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` | Integrate schedule: filter online players in ScheduleEventsForPlayers; apply attempt-end policy; handle empty queue with fast-forward to next session |
| `BingoSim.Core/Entities/Event.cs` | Add `StartTime: DateTimeOffset` (with migration default for existing rows) |
| `BingoSim.Infrastructure/Persistence/Configurations/EventConfiguration.cs` | Map `StartTime` |
| `BingoSim.Infrastructure/Persistence/Migrations/` | New migration for Event.StartTime |
| `BingoSim.Application/DTOs/EventResponse.cs` | Add StartTime |
| `BingoSim.Application/DTOs/CreateEventRequest.cs` | Add StartTime |
| `BingoSim.Application/DTOs/UpdateEventRequest.cs` | Add StartTime |
| `BingoSim.Application/Mapping/EventMapper.cs` | Map StartTime |
| `BingoSim.Web/Components/Pages/Events/EventCreate.razor` | Add StartTime input (date + time, ET) |
| `BingoSim.Web/Components/Pages/Events/EventEdit.razor` | Add StartTime input |
| `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` | Add `DistributedBatch_WithScheduleSnapshot_CompletesSuccessfully` |

### Optional (Event StartTime UI)

If Event.StartTime is required for schedule-enabled runs, the Event create/edit forms need a date-time picker. For v1, a reasonable default (e.g. "next Monday 00:00 ET") can be used when StartTime is null, with schedule checks disabled (all players always online).

---

## 7) Implementation Order

1. Add `ScheduledSessionSnapshotDto`, `WeeklyScheduleSnapshotDto`, `ScheduleEvaluator` with pure functions.
2. Add `ScheduleEvaluatorTests` (unit tests).
3. Add `Event.StartTime` and migration; extend EventSnapshotDto and PlayerSnapshotDto; update EventSnapshotBuilder.
4. Integrate schedule into SimulationRunner (filter online players, attempt-end policy, fast-forward).
5. Add `ScheduleSimulationIntegrationTests`.
6. Update Event CRUD/UI for StartTime (or document default).
7. Add distributed smoke test.
8. Update documentation (this plan → implementation doc).

---

*End of plan.*
