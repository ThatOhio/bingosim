# User Flows

This document describes the primary ways a user interacts with the system via the Web UI. The system is a single-user, power-user–oriented application; flows prioritize flexibility and iteration speed over guardrails or polish.

The UI is a single application with multiple pages. There is no dashboard in v1.

---

## Primary Navigation Areas

- Players Library
- Activities Library
- Events
- Run Simulations
- Simulation Results

Libraries may be opened in separate tabs while configuring events or runs. Where relevant, configuration pages provide a manual refresh action to fetch newly created library items.

---

## Flow 1: Create or Update Player Library

**Goal:** Define reusable player profiles with skills, capabilities, and schedules.

1. Navigate to **Players Library**
2. Create a new PlayerProfile
   - Enter name
   - Set skill time multiplier
   - Assign Capabilities
   - Define weekly play schedule (one or more sessions per day)
3. Save PlayerProfile
4. Repeat for additional players as needed

**Notes:**
- Players are reusable across events.
- Editing a PlayerProfile immediately affects all future simulations that reference it.
- No validation is enforced beyond basic field presence.

---

## Flow 2: Create or Update Activity Library

**Goal:** Define reusable in-game activities and their probabilistic behavior.

1. Navigate to **Activities Library**
2. Create a new ActivityDefinition
   - Name and stable key
   - Declare solo and/or group support
   - Define min/max group sizes (if applicable)
3. Define one or more ActivityAttemptDefinitions
   - Specify roll scope (per-player or per-group)
   - Configure attempt time model
   - Define outcome distributions (multiple outcomes with weights)
4. Define GroupScalingRules
   - Add size bands with time and probability multipliers
5. Save ActivityDefinition

**Notes:**
- Activities are globally reusable.
- Changes to an ActivityDefinition affect all events and future simulation runs that reference it.
- Activities may define multiple independent loot/roll channels.

---

## Flow 3: Create an Event and Board

**Goal:** Define the structure of a specific community event.

1. Navigate to **Events**
2. Create a new Event
   - Enter name
   - Set event duration
   - Confirm unlock points per row (global default used unless overridden)
3. Define Rows
   - Rows are ordered and explicitly numbered
4. For each Row, define four Tiles
   - Assign point value (1–4)
   - Set required progress count
   - Define TileActivityRules:
     - Select one or more Activities
     - Declare accepted DropKeys
     - Define capability requirements and optional modifiers
5. Save Event

**Notes:**
- Tiles are event-specific and not reusable.
- Partial or incomplete event definitions may be saved.
- No strict validation is enforced in v1 (user responsibility).

---

## Flow 4: Assign Teams and Strategies

**Goal:** Draft teams and assign strategies for simulation.

1. From an Event, navigate to **Team Setup**
2. Create one or more Teams
3. Assign PlayerProfiles to each Team
4. For each Team:
   - Select a StrategyKey from the available list
   - Provide optional JSON strategy parameters
5. Save configuration

**Notes:**
- Teams are scoped to the event.
- Strategies are code-defined but parameterized via JSON.
- Different teams within the same event may use different strategies.

---

## Flow 5: Run Simulations (Batch)

**Goal:** Execute many simulations of the same event configuration to derive statistical results.

1. Navigate to **Run Simulations**
2. Select an Event configuration
3. Select Teams and Strategies (as defined)
4. Choose number of simulation runs (integer input)
5. Choose execution mode:
   - Local execution (internal worker, for quick sims / testing)
   - Distributed workers (external worker containers)
6. Optional: enable **Single-Run / Dry Run** mode for debugging
7. Optional: provide a **Seed** value for reproducible execution
8. Start simulation batch


**During Execution:**
- Simulations execute asynchronously via the selected execution mode
- UI displays coarse progress indicators (e.g. completed runs vs total)
- No pause or cancel controls in v1

---

## Flow 6: View Simulation Results

**Goal:** Analyze aggregated outcomes of a simulation batch.

1. Navigate to **Simulation Results**
2. Select a completed SimulationBatch
3. View per-team aggregated metrics:
   - Winner rate per team
   - Average total points
   - Average tiles completed
4. View timelines:
   - Row unlock times per team
   - Tile completion times per team
5. Inspect strategy configuration used for each team

**Notes:**
- Results are viewed one batch at a time in v1.
- No CSV/JSON export in v1.
- Comparison across batches is manual.

---

## Alternate / Iteration Flows

### Edit and Re-run
- User edits an Activity, Player, or Event
- Returns to Run Simulations
- Executes a new batch using updated definitions

### Strategy Tuning
- User adjusts strategy parameters (JSON)
- Re-runs the same event with the modified strategy
- Compares results across batches manually

### Debug Single Run
- User runs a single simulation (typically via local execution)
- Optionally provides a seed for reproducibility
- Confirms unlock order, tile progression, and timing behavior
- Uses results to refine strategy or activity definitions

---

## Validation and Error Handling Philosophy

- Minimal UI validation in v1
- Partial configurations are allowed and savable
- Errors during simulation execution surface as run failures
- User is expected to self-correct invalid configurations

---

## Intentional Non-Flows (Out of Scope for v1)

- No authentication or multi-user support
- No dashboard or overview landing page
- No batch cancellation or pause
- No duplication shortcuts (players, activities, events)
- No result export
- No automatic comparison across batches

---

## Summary

The system is designed for iterative experimentation:
- Define libraries
- Assemble events
- Run many simulations
- Analyze aggregate outcomes
- Adjust and repeat

User flows intentionally favor flexibility and speed over safety or polish.

