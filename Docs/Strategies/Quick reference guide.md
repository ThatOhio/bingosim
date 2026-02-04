# Row Unlocking Strategy Implementation - Quick Reference

## Overview
This document provides a quick reference for implementing the Row Unlocking Strategy refactor. Follow the prompts in order.

## Implementation Sequence

### Phase 1: Refactor Existing Architecture
**Goal:** Expand strategy system to control both task assignment and grant allocation

#### Prompt 1: Rename and Expand Strategy Interface
- **File:** `prompt-1-rename-interface.md`
- **Duration:** ~30-45 minutes
- **Key Actions:**
  - Rename `IProgressAllocator` → `ITeamStrategy`
  - Create `TaskSelectionContext`
  - Rename `AllocatorContext` → `GrantAllocationContext`
  - Add `SelectTaskForPlayer` method signature
  - Update existing allocators with placeholder implementations
- **Deliverable:** `Docs/Strategies/phase1-part1-completed.md`

#### Prompt 2: Rename and Update Strategy Factory
- **File:** `prompt-2-rename-factory.md`
- **Duration:** ~20-30 minutes
- **Key Actions:**
  - Rename factory classes and interfaces
  - Update dependency injection
  - Update SimulationRunner to use new types
- **Deliverable:** `Docs/Strategies/phase1-part2-completed.md`

#### Prompt 3: Integrate Strategy-Driven Task Selection
- **File:** `prompt-3-integrate-task-selection.md`
- **Duration:** ~45-60 minutes
- **Key Actions:**
  - Replace fixed task assignment with strategy calls
  - Create TaskSelectionContext builder
  - Update placeholder strategies with actual logic
  - Test that simulation still works
- **Deliverable:** `Docs/Strategies/phase1-part3-completed.md`

---

### Phase 2: Implement Row Unlocking Strategy
**Goal:** Build the complete Row Unlocking Strategy with optimal tile combination logic

#### Prompt 4: Create Row Unlocking Strategy Shell
- **File:** `prompt-4-create-strategy-shell.md`
- **Duration:** ~20-30 minutes
- **Key Actions:**
  - Add `RowUnlocking` to catalog
  - Create strategy class with placeholder methods
  - Register in factory
  - Create implementation plan document
- **Deliverable:** `Docs/Strategies/phase2-part1-completed.md`

#### Prompt 5: Implement Grant Allocation Logic
- **File:** `prompt-5-implement-grant-allocation.md`
- **Duration:** ~30-45 minutes
- **Key Actions:**
  - Implement `SelectTargetTileForGrant`
  - Prioritize furthest row, highest points
  - Add fallback logic
  - Handle edge cases
- **Deliverable:** `Docs/Strategies/phase2-part2-completed.md`

#### Prompt 6: Implement Tile Combination Calculator
- **File:** `prompt-6-implement-combination-calculator.md`
- **Duration:** ~60-90 minutes
- **Key Actions:**
  - Create `TileCombination` class
  - Implement backtracking algorithm
  - Add caching mechanism
  - Test combination generation
- **Deliverable:** `Docs/Strategies/phase2-part3-completed.md`

#### Prompt 7: Implement Tile Completion Time Estimation
- **File:** `prompt-7-implement-time-estimation.md`
- **Duration:** ~45-60 minutes
- **Key Actions:**
  - Create `TileCompletionEstimator`
  - Calculate expected progress per attempt
  - Enrich combinations with time estimates
  - Integrate with caching system
- **Deliverable:** `Docs/Strategies/phase2-part4-completed.md`

#### Prompt 8: Implement Task Selection Logic
- **File:** `prompt-8-implement-task-selection.md`
- **Duration:** ~60-90 minutes
- **Key Actions:**
  - Implement `SelectTaskForPlayer`
  - Create helper methods for priority chain
  - Implement optimal combination selection
  - Add comprehensive fallback logic
- **Deliverable:** `Docs/Strategies/phase2-part5-completed.md`

---

### Phase 3: Cleanup and Validation
**Goal:** Remove old code, validate implementation, document everything

#### Prompt 9: Cleanup and Testing
- **File:** `prompt-9-cleanup-testing.md`
- **Duration:** ~90-120 minutes
- **Key Actions:**
  - Remove placeholder strategies
  - Update catalog and factory
  - Run comprehensive tests
  - Performance validation
  - Documentation updates
- **Deliverable:** `Docs/Strategies/phase3-cleanup-completed.md`

---

## Total Estimated Time
- **Phase 1:** 2-2.5 hours
- **Phase 2:** 4-6 hours
- **Phase 3:** 1.5-2 hours
- **Total:** 7.5-10.5 hours (spread across multiple sessions)

## Tips for Success

### Working with Cursor
1. Copy the entire prompt file content into Cursor
2. Let Cursor read and understand the full requirements
3. Review the changes before accepting them
4. Run builds frequently to catch issues early
5. Create the deliverable documentation as you go

### Handling Issues
- If Cursor gets stuck, break the prompt into smaller pieces
- If tests fail, roll back and debug before proceeding
- If performance is poor, review the caching strategy
- Document any deviations from the plan

### Testing Strategy
- Build after each prompt
- Run simulation tests after Phase 1 Part 3
- Test each strategy component in isolation during Phase 2
- Comprehensive end-to-end testing in Phase 3

### Documentation
- Keep all deliverable files in `Docs/Strategies/`
- Include code snippets in documentation
- Document decisions and trade-offs
- Update as you discover edge cases

## File Structure After Completion

```
BingoSim.Application/
├── Simulation/
│   ├── Allocation/
│   │   ├── ITeamStrategy.cs (renamed)
│   │   ├── ITeamStrategyFactory.cs (renamed)
│   │   ├── TeamStrategyFactory.cs (renamed)
│   │   ├── GrantAllocationContext.cs (renamed)
│   │   └── TaskSelectionContext.cs (new)
│   ├── Strategies/
│   │   ├── RowUnlockingStrategy.cs (new)
│   │   ├── TileCombination.cs (new)
│   │   ├── RowCombinationCalculator.cs (new)
│   │   └── TileCompletionEstimator.cs (new)
│   └── Runner/
│       └── SimulationRunner.cs (modified)
├── StrategyKeys/
│   └── StrategyCatalog.cs (modified)

Docs/
└── Strategies/
    ├── code-analysis.md (existing)
    ├── refactor-overview.md (new)
    ├── row-unlocking-implementation-plan.md (new)
    ├── strategy-comparison.md (new)
    ├── phase1-part1-completed.md (new)
    ├── phase1-part2-completed.md (new)
    ├── phase1-part3-completed.md (new)
    ├── phase2-part1-completed.md (new)
    ├── phase2-part2-completed.md (new)
    ├── phase2-part3-completed.md (new)
    ├── phase2-part4-completed.md (new)
    ├── phase2-part5-completed.md (new)
    └── phase3-cleanup-completed.md (new)
```

## Next Steps After Completion
1. Deploy to development environment
2. Run extended simulation tests
3. Monitor performance in real scenarios
4. Gather user feedback
5. Plan second strategy implementation using this framework
