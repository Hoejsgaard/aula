# Task 011: Eliminate ALL Context/Scoping Patterns

## ❌ ARCHITECTURAL PRINCIPLE VIOLATION ❌

**PROBLEM**: The codebase contains cancer patterns that violate the core architectural principle:
- ChildContextScope ❌
- IChildContext ❌
- ChildContext ❌
- IChildOperationExecutor ❌
- ChildOperationExecutor ❌
- ChildServiceCoordinator ❌ (uses scoping patterns)

**ARCHITECTURAL PRINCIPLE**:
> "Pass the fucking child as parameter to singletons, otherwise let the child hierarchy use concrete instances when needed!"

## CANCER PATTERNS TO ELIMINATE

### Current Anti-Patterns:
```csharp
// ❌ CANCER - Complex scoping bullshit
await _executor.ExecuteInChildContextAsync(child,
    async (serviceProvider) =>
    {
        var dataService = serviceProvider.GetRequiredService<IChildDataService>();
        return await dataService.GetOrFetchWeekLetterAsync(child, date, true);
    },
    "GetWeekLetter");
```

### Correct Pattern:
```csharp
// ✅ SIMPLE - Direct singleton calls with Child parameter
var dataService = _serviceProvider.GetRequiredService<IChildDataService>();
return await dataService.GetOrFetchWeekLetterAsync(child, date, true);
```

## EXECUTION PLAN - 7 PHASES

### Phase 1: Analysis & Dependencies
- [ ] Search entire codebase for ALL context pattern usage
- [ ] Map dependencies between cancer classes
- [ ] Identify all consumers that need updates
- [ ] Document current state

### Phase 2: ChildServiceCoordinator Elimination
- [ ] Replace all `_executor.ExecuteInChildContextAsync()` calls with direct service calls
- [ ] Update constructor to inject required services directly
- [ ] Remove IChildOperationExecutor dependency
- [ ] Test: Build + Run app + All tests green
- [ ] Commit: "refactor: replace executor pattern with direct service calls in ChildServiceCoordinator"

### Phase 3: Update ChildServiceCoordinator Consumers
- [ ] Find all classes using ChildServiceCoordinator
- [ ] Update calls to match new simplified signatures
- [ ] Test: Build + Run app + All tests green
- [ ] Commit: "fix: update ChildServiceCoordinator consumers"

### Phase 4: Delete Operation Executor Classes
- [ ] Remove IChildOperationExecutor interface
- [ ] Remove ChildOperationExecutor implementation
- [ ] Update DI registration in Program.cs
- [ ] Test: Build + Run app + All tests green
- [ ] Commit: "feat: eliminate ChildOperationExecutor pattern"

### Phase 5: Delete Context Classes
- [ ] Remove IChildContext interface
- [ ] Remove ChildContext implementation
- [ ] Remove ChildContextScope class
- [ ] Update DI registration in Program.cs
- [ ] Test: Build + Run app + All tests green
- [ ] Commit: "feat: eliminate ChildContext pattern"

### Phase 6: Update Tests & Architecture
- [ ] Delete tests for eliminated classes
- [ ] Update ArchitectureTests.cs to remove allowlisted types
- [ ] Fix any remaining test compilation issues
- [ ] Test: All tests green
- [ ] Commit: "test: remove tests for eliminated context patterns"

### Phase 7: Final Cleanup
- [ ] Search for any remaining references
- [ ] Remove any unused using statements
- [ ] Verify no context patterns remain
- [ ] Test: Build + Run app + All tests green
- [ ] Commit: "cleanup: remove final references to context patterns"

## SUCCESS CRITERIA

### ✅ MANDATORY OUTCOMES:
1. **Zero context patterns**: No ChildContext, ChildContextScope, or executor patterns remain
2. **Direct parameter passing**: All services accept Child parameters directly
3. **Simplified architecture**: No scoping complexity, just singleton services
4. **All tests green**: 1769+ tests passing without failures
5. **App runs successfully**: Integration test confirms functionality

### ❌ ZERO TOLERANCE FOR:
- Any remaining ChildContext usage
- Any remaining ChildContextScope usage
- Any remaining IChildOperationExecutor usage
- Any scoping/executor patterns
- Any "I'll keep this for now" compromises

## VALIDATION COMMANDS

```bash
# Build validation
dotnet build src/Aula.sln

# Test validation
dotnet test src/Aula.Tests

# Integration test
cd src/Aula && timeout 120 dotnet run

# Search validation
grep -r "ChildContext" src/ --exclude-dir=bin --exclude-dir=obj
grep -r "ChildOperationExecutor" src/ --exclude-dir=bin --exclude-dir=obj
grep -r "ExecuteInChildContextAsync" src/ --exclude-dir=bin --exclude-dir=obj
```

---

**REMEMBER**: No compromises. No "temporary" solutions. Complete elimination of ALL context patterns.