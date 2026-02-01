# Scope

This document defines the functional and non-functional scope of the simulation system. It exists to clearly state what the system *will* model, what assumptions it makes, and what is explicitly out of scope.

## Target Platforms

- The Web UI is hosted in Docker and accessed via modern desktop web browsers
- Simulation workers run in Docker containers
- Workers have direct network access to required infrastructure (database, coordination services)
- No public or internet-facing deployment requirements exist at this time

## Core Concepts That Are Simulated

### Players
- Represent individual participants in the event
- Have fixed skill levels defined at event start
- Follow realistic play-session behavior (e.g. daily play windows, session duration)
- Do not learn or improve during the course of an event

### Event Teams
- Groups of players cooperating under a shared strategy
- Team-level strategy determines how players are assigned work
- Players execute the best available action they are capable of, given team strategy and personal eligibility

### Events
- Represent a full community event with a fixed real-world duration (e.g. one week)
- All event rules, tiles, and configurations are defined at event start
- Events always terminate at their configured end time
- Many events may be simulated with varying parameters for statistical comparison

### Event Board
- The board consists of ordered rows (“packs”) of tiles
- Each row contains four tiles: 1-point, 2-point, 3-point, and 4-point tiles
- All teams begin with only the first row unlocked

### Tile Unlock Rules
- To unlock the next row, a team must complete **at least 5 points worth of tiles from the immediately preceding row**
- Teams may complete additional tiles in a row beyond the 5 points, but only 5 points are required to unlock the next row
- Tile unlock progression is monotonic; tiles and rows never relock

## Tile Simulation Model

- All tiles are abstracted as probabilistic time-based tasks
- A tile attempt consumes a configurable amount of simulated time
- Each attempt has a fixed probability of producing progress or completion
- Probabilities are defined by the underlying activities; tiles determine how activity outcomes are interpreted as progress
- Progress on tiles never regresses

### Eligibility & Requirements
- Tiles may require players to meet prerequisites (items, quests, capabilities)
- Player eligibility is evaluated when determining task assignment
- Ineligible players will never be assigned to attempt a tile

### Skill Effects
- Player skill affects time-to-attempt (faster or slower than baseline)
- Skill does not affect probability directly unless explicitly configured

### Group Tiles
- Some tiles require or allow group participation
- Group size may affect:
  - Attempt duration
  - Probability of success or rare outcomes
- Large teams may form multiple independent groups to attempt the same tile concurrently

### Failure Modeling
- Tile attempts may fail and waste time
- Failures may include penalties such as additional time loss (e.g. death and recovery)
- Failures do not permanently block progress

## Time Model

- The underlying game operates on ticks (600ms), but this complexity is intentionally abstracted away
- Simulation time is modeled using coarse-grained real-world units (seconds/minutes)
- The system prioritizes realistic aggregate behavior over tick-accurate modeling

## Strategy & Decision Making

- Player behavior is driven by team-level strategy
- Strategies are pluggable and replaceable
- No machine learning or adaptive behavior is used
- All players have full knowledge of the event board and tile definitions
- Decisions are constrained only by unlock state and player eligibility

## Execution Model

- Simulations are executed by generic worker processes
- Thousands to millions of simulations may be run
- The Web UI may display coarse progress indicators (e.g. progress bars)
- Real-time detailed visualization of simulation internals is not required for v1

## Persistence

- A database is used for long-term storage of:
  - Event configurations
  - Simulation results
  - Aggregated statistics
- Persistence is required for comparison across simulation runs

## Explicit Non-Goals

The system explicitly does **not** aim to:
- Simulate exact in-game mechanics or engine-level behavior
- Model real-time gameplay or user interaction
- Support AI or machine learning–based strategies
- Support multiple users, authentication, or permissions
- Act as a general-purpose simulation framework
- Optimize for production-scale deployment or public access

