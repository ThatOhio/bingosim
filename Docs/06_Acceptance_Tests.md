# Acceptance Tests

This document defines acceptance criteria for v1 features as verifiable behaviors. Tests are written to support incremental implementation (“vertical slices”) and to minimize ambiguous interpretation.

Conventions:
- “UI” refers to the server-rendered Web UI.
- “Distributed workers” refers to external BingoSim.Worker containers.
- “Local execution” refers to a Web-hosted internal worker/executor for quick sims and testing.
- “Seed” refers to a reproducibility seed used to drive simulation RNG.

---

## 0) Walking Skeleton (End-to-End)

### Scenario: Create minimal libraries, event, run batch, view results
**Given**
- The system is running with Postgres and RabbitMQ available
- The Web UI is accessible

**When**
1. I create at least 2 PlayerProfiles
2. I create at least 1 ActivityDefinition with at least 1 loot line
3. I create an Event with at least 1 Row and 4 Tiles (1–4 points)
4. I draft 2 Teams and assign players
5. I assign strategies to both teams
6. I run a batch of 100 simulations
7. I open the results page for the batch

**Then**
- The batch completes successfully
- Results show, per team:
  - winner rate
  - mean/min/max total points
  - mean/min/max tiles completed
  - row reached
  - row unlock timelines
  - tile completion timelines

---

## 1) Player Library

### Scenario: Create a PlayerProfile
**When**
- I create a PlayerProfile with:
  - Name
  - Skill time multiplier
  - Capabilities list
  - Weekly schedule with multiple sessions

**Then**
- The PlayerProfile appears in the Players Library list
- It can be selected when drafting a team

### Scenario: Edit a PlayerProfile
**Given**
- A PlayerProfile exists

**When**
- I edit its schedule, capabilities, or skill multiplier and save

**Then**
- The updated values are persisted and shown on refresh
- Future simulation runs using that PlayerProfile use the updated values

### Scenario: Delete a PlayerProfile with confirmation
**Given**
- A PlayerProfile exists

**When**
- I click Delete
- I am prompted to confirm
- I confirm deletion

**Then**
- The PlayerProfile is removed from the library list

---

## 2) Activity Library

### Scenario: Create an ActivityDefinition with multiple loot lines
**When**
- I create an ActivityDefinition with:
  - SupportsSolo=true
  - SupportsGroup=true
  - 2 ActivityAttemptDefinitions with RollScope=PerPlayer
  - Each attempt definition has outcomes with weighted probabilities
  - At least one outcome grants a progress key with Units=+1
  - At least one rare outcome grants Units=+3

**Then**
- The ActivityDefinition appears in the Activities Library
- The ActivityDefinition can be referenced by TileActivityRules

### Scenario: Create an ActivityDefinition with a team-scoped rare roll
**When**
- I create an ActivityAttemptDefinition with RollScope=PerGroup and rare outcomes

**Then**
- A tile can accept DropKeys from this per-group attempt definition
- Simulation can attribute per-group grants to team progress allocation

### Scenario: Group scaling bands are supported
**Given**
- An ActivityDefinition supports group play

**When**
- I define GroupSizeBands (e.g., 1, 2–4, 5–8)
- Each band defines a time multiplier and probability multiplier

**Then**
- The ActivityDefinition persists these bands
- A simulation run uses the band matching the chosen group size
- Bands may apply over ranges (MinSize..MaxSize)

### Scenario: Edit and delete Activities with confirmation
**Given**
- An ActivityDefinition exists

**When**
- I edit it and save

**Then**
- It persists changes and is reflected in the UI

**When**
- I delete it and confirm

**Then**
- It is removed from the Activities list

---

## 3) Event & Board Configuration

### Scenario: Create an Event with ordered rows and tiles
**When**
- I create an Event with:
  - Duration
  - Rows with explicit indices
  - Each row contains exactly four tiles with Points 1,2,3,4
  - Each tile has RequiredCount
  - Each tile has 1+ TileActivityRules

**Then**
- The Event is persisted
- The Event can be selected from Run Simulations

### Scenario: A tile supports multiple Activities and per-activity rules
**Given**
- Two activities exist (A and B)

**When**
- I configure a Tile with AllowedActivities = [A, B]
- For Activity A, accepted DropKeys differ from Activity B
- Requirement and modifier capability lists differ per activity

**Then**
- Both rules persist
- Simulation can treat A and B differently for the same tile

### Scenario: Edit and delete Events with confirmation
**Given**
- An Event exists

**When**
- I edit tiles or row configuration and save

**Then**
- The Event reflects the changes

**When**
- I delete the Event and confirm

**Then**
- The Event is removed from the Events list

---

## 4) Team Drafting & Strategy Assignment

### Scenario: Draft teams and assign strategies
**Given**
- An Event exists
- PlayerProfiles exist

**When**
- I draft two teams, assign players
- I choose strategy for each team from a dropdown:
  - Strategy A (baseline)
  - Strategy B (alternative)
- I optionally provide JSON parameters for each strategy

**Then**
- Team configuration is persisted for the run configuration
- StrategyKey and ParamsJson are stored and visible in Results

---

## 5) Simulation Execution

### Scenario: Run using local execution (internal worker)
**Given**
- Local execution is enabled

**When**
- I start a batch of 100 runs

**Then**
- The batch completes without requiring external worker containers
- Results are persisted and viewable

### Scenario: Run using distributed workers
**Given**
- Distributed workers are enabled and connected

**When**
- I start a batch of 1000 runs

**Then**
- Workers execute and persist results
- The Web UI can be closed without stopping execution
- Later, the UI shows the completed batch results

### Scenario: Unlock rules are enforced (row rush)
**Given**
- Board rows unlock after 5 points completed from the previous row

**When**
- Simulations run

**Then**
- For each team:
  - Row 1 is unlocked at time 0
  - A row is unlocked only after >= 5 points of tiles in the previous row are completed
  - Unlocks are monotonic and never revert

### Scenario: Strategy controls progress allocation to best eligible tile
**Given**
- Multiple tiles can accept the same DropKey at the same time
- Players with different capabilities are acting concurrently

**When**
- ProgressGrant events occur from activities

**Then**
- The strategy selects the allocation target tile(s)
- Progress is applied only to tiles that are eligible/unlocked for the team
- Tiles may be in-progress concurrently across different players

### Scenario: Retries and terminal failure are tracked
**Given**
- A run fails due to a transient error (simulated)

**When**
- The system retries up to 5 attempts

**Then**
- Attempt count is incremented and stored
- If it still fails after 5 attempts:
  - run is marked Failed
  - batch is marked Error
  - UI shows that the batch errored and why (high-level message)

---

## 6) Reproducibility & Seeds

### Scenario: Provide a seed in the UI
**When**
- I enter a seed value when starting a batch

**Then**
- The seed is stored with the batch/run configuration
- Each run uses deterministic RNG derived from the seed (and run index or a derived seed)

### Scenario: Repeatability with the same seed
**Given**
- I run a dry run with seed S and capture results

**When**
- I re-run the same configuration with the same seed S

**Then**
- The dry run produces identical results

### Scenario: Rerun a specific run by seed
**Given**
- A completed run has a recorded seed

**When**
- I request a re-run of that specific run (via UI action or a run-details view)

**Then**
- The rerun produces identical results and is stored as a new run or a linked rerun result

---

## 7) Results & Aggregations

### Scenario: Batch-level aggregates are stored and displayed
**Given**
- A batch completes successfully

**When**
- I view results

**Then**
- The UI does not recompute aggregates from scratch each time
- For each team, the UI shows:
  - winner rate
  - mean/min/max total points
  - mean/min/max tiles completed
  - row reached distribution or at minimum mean/min/max row reached

### Scenario: Timelines are available for analysis
**Given**
- A batch completes

**Then**
- For each team, results include:
  - row unlock times (per row)
  - tile completion times (per tile)

---

## 8) Observability & Performance Testability

### Scenario: Metrics are available for diagnosing throughput
**When**
- A batch is running

**Then**
- The system exposes basic metrics sufficient to diagnose performance:
  - runs completed/failed/retried
  - estimated runs/sec
  - batch duration

### Scenario: Multi-worker parallelism can be validated
**Given**
- A configuration exists to limit worker concurrency or artificially add delay (test mode)
- Two distributed workers are running

**When**
- I run a batch with test-mode throttling enabled

**Then**
- Observed throughput improves when using two workers vs one worker
- The test does not rely on perfect linear scaling, only a measurable improvement

---

## 9) UI Expectations (v1)

### Scenario: All pages exist and support CRUD
**Then**
- Players Library supports create/edit/delete (with delete confirmation)
- Activities Library supports create/edit/delete (with delete confirmation)
- Events supports create/edit/delete (with delete confirmation)
- Run Simulations supports selecting event + teams + strategies + run count + seed + local/distributed mode
- Simulation Results supports viewing one batch at a time with required metrics and timelines

---

## Notes for Implementation Slices

Recommended incremental slices (in order):
1. CRUD: PlayerProfiles
2. CRUD: ActivityDefinitions (single loot line)
3. CRUD: Events + Tiles referencing activities
4. Batch start + local execution (single run then 100 runs)
5. Results page with stored aggregates (mean/min/max)
6. Add strategies (2 strategies)
7. Add multi-activity tiles and progress allocation logic
8. Add per-group roll scope + group scaling
9. Add distributed workers + retry/terminal failure state
10. Add seed input + reproducibility + rerun-by-seed
11. Add basic metrics + parallelism test mode

