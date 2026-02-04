# Strategy System Refactor Overview

## Current State
The strategy system only controls **grant allocation** (where drops go when multiple tiles could accept them). Task assignment (which tile/activity a player works on) is fixed across all strategies.

## Goal
Expand strategies to control **both**:
1. **Task Assignment**: Which tile/activity each player should work on
2. **Grant Allocation**: Which tile receives a grant when multiple tiles are eligible

## Key Changes

### 1. Rename & Expand Interface
- Rename `IProgressAllocator` → `ITeamStrategy`
- Add new method for task selection alongside existing grant allocation method
- Strategies must handle their own fallback logic (always return valid results or null when no valid options exist)

### 2. New Task Selection Method
```csharp
public interface ITeamStrategy
{
    // Existing method (renamed for clarity)
    string? SelectTargetTileForGrant(GrantAllocationContext context);
    
    // New method for task assignment
    (Guid? activityId, TileActivityRuleSnapshotDto? rule)? SelectTaskForPlayer(TaskSelectionContext context);
}
```

### 3. Context Objects
- `GrantAllocationContext` (renamed from `AllocatorContext`) - existing data structure
- `TaskSelectionContext` (new) - player info, capabilities, unlocked rows, tiles, tile progress, etc.

### 4. Strategy Factory Update
- Rename `ProgressAllocatorFactory` → `TeamStrategyFactory`
- Return `ITeamStrategy` instances
- Remove old RowRush and GreedyPoints placeholders

### 5. Simulation Runner Updates
- Replace fixed `GetFirstEligibleActivity` logic with strategy-driven task selection
- Update grant allocation calls to use renamed interface/methods
- Factory injection remains the same pattern

### 6. New Row Unlocking Strategy
Implements `ITeamStrategy` with:

**Task Assignment Logic:**
1. Calculate optimal tile combinations to unlock next row (cached)
2. For given player: highest point tile in combination they can work on
3. Fallback: highest point tile on that row they can work on
4. Fallback: highest point tile anywhere they can work on
5. Return null if no valid tiles exist

**Grant Allocation Logic:**
1. Highest point tile on the furthest unlocked row that accepts the grant
2. Fallback: highest point tile anywhere that accepts the grant
3. Return null if no eligible tiles

## Implementation Strategy

### Phase 1: Refactor Existing Structure
1. Rename interfaces, classes, and files
2. Add new method signature to interface (with default/placeholder implementation)
3. Update factory and registration
4. Update SimulationRunner to call strategy for task selection

### Phase 2: Implement Row Unlocking Strategy
1. Create combination calculator for row unlocking
2. Implement task selection logic with fallbacks
3. Implement grant allocation logic with fallbacks
4. Add to strategy catalog and factory

### Phase 3: Testing & Validation
1. Test strategy switching
2. Validate row unlocking logic
3. Performance testing (caching effectiveness)

## Files That Need Changes

### Renames/Refactors:
- `IProgressAllocator.cs` → `ITeamStrategy.cs`
- `AllocatorContext.cs` → `GrantAllocationContext.cs`
- `ProgressAllocatorFactory.cs` → `TeamStrategyFactory.cs`
- `IProgressAllocatorFactory.cs` → `ITeamStrategyFactory.cs`

### New Files:
- `TaskSelectionContext.cs`
- `RowUnlockingStrategy.cs`

### Updates:
- `SimulationRunner.cs` (task assignment + grant allocation)
- `StrategyCatalog.cs` (add RowUnlocking key)
- Remove: `RowRushAllocator.cs`, `GreedyPointsAllocator.cs`

## Next Steps
Follow the numbered prompt files in sequence to implement this refactor.
