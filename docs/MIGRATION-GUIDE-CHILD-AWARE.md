# Migration Guide: Child-Aware Architecture

## Overview

This guide helps you migrate from the legacy architecture (passing Child parameters) to the new child-aware architecture that uses scoped dependency injection and IChildContext.

## Why Migrate?

The old architecture had several issues:
- ❌ **Security Risk**: Services could accidentally access data from wrong children
- ❌ **No Isolation**: Cross-child data leakage was possible
- ❌ **Poor Testability**: Difficult to mock child-specific behavior
- ❌ **GDPR Compliance**: Hard to ensure data isolation per child

The new architecture provides:
- ✅ **Complete Isolation**: Each child operates in its own scope
- ✅ **Security by Design**: Impossible to access wrong child's data
- ✅ **Better Testing**: Easy to test child-specific scenarios
- ✅ **GDPR Ready**: Built-in data isolation and audit logging

## Migration Steps

### Step 1: Identify Usage of Legacy Interfaces

Look for these obsolete interfaces in your code:
- `IDataService` → Migrate to `IChildDataService`
- `IAgentService` → Migrate to `IChildAgentService`
- `IMinUddannelseClient` → Migrate to `IChildAuthenticationService`

### Step 2: Update Service Registrations

**Old Pattern:**
```csharp
services.AddSingleton<IDataService, DataService>();
services.AddSingleton<IAgentService, AgentService>();
```

**New Pattern:**
```csharp
// Register scoped child-aware services
services.AddScoped<IChildDataService, SecureChildDataService>();
services.AddScoped<IChildAgentService, SecureChildAgentService>();

// Register the coordinator for high-level operations
services.AddSingleton<IChildServiceCoordinator, ChildServiceCoordinator>();
```

### Step 3: Update Service Implementations

**Old Pattern:**
```csharp
public class MyService
{
    private readonly IDataService _dataService;

    public async Task<JObject> GetDataForChild(Child child)
    {
        return _dataService.GetWeekLetter(child, weekNumber, year);
    }
}
```

**New Pattern:**
```csharp
public class MyService
{
    private readonly IChildContext _childContext;
    private readonly IChildDataService _dataService;

    public async Task<JObject> GetData()
    {
        // Child is determined from context, not passed as parameter
        return await _dataService.GetWeekLetterAsync(weekNumber, year);
    }
}
```

### Step 4: Use ChildServiceCoordinator for Cross-Child Operations

When you need to perform operations for multiple children (only allowed in Program.cs or top-level orchestration):

**Old Pattern:**
```csharp
foreach (var child in children)
{
    var data = await agentService.GetWeekLetterAsync(child, date);
    // Process data
}
```

**New Pattern:**
```csharp
// Use the coordinator which handles scope creation
await coordinator.FetchWeekLettersForAllChildrenAsync(date);
```

### Step 5: Update Tests

**Old Pattern:**
```csharp
[Fact]
public async Task TestChildOperation()
{
    var child = new Child { FirstName = "Test" };
    var result = await service.ProcessChild(child);
    Assert.NotNull(result);
}
```

**New Pattern:**
```csharp
[Fact]
public async Task TestChildOperation()
{
    // Arrange
    var mockContext = new Mock<IChildContext>();
    mockContext.Setup(c => c.CurrentChild).Returns(new Child { FirstName = "Test" });

    var service = new MyService(mockContext.Object, ...);

    // Act
    var result = await service.Process();

    // Assert
    Assert.NotNull(result);
}
```

## Common Patterns

### Pattern 1: Service That Operates on Current Child

```csharp
public class SecureChildService : IChildService
{
    private readonly IChildContext _context;

    public SecureChildService(IChildContext context)
    {
        _context = context;
    }

    public async Task<string> GetChildData()
    {
        _context.ValidateContext(); // Ensures child is set
        var child = _context.CurrentChild!;

        // Service operates on the current child
        return $"Data for {child.FirstName}";
    }
}
```

### Pattern 2: Executing Operations in Child Scope

```csharp
public class OrchestrationService
{
    private readonly IChildOperationExecutor _executor;

    public async Task<string> ProcessChildData(Child child)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                var data = await dataService.GetAllWeekLettersAsync();
                return ProcessData(data);
            },
            "ProcessChildData");
    }
}
```

### Pattern 3: GDPR-Compliant Operations

```csharp
public class GdprService
{
    private readonly IChildOperationExecutor _executor;

    public async Task<string> ExportChildData(Child child)
    {
        return await _executor.ExportChildDataAsync(child);
    }

    public async Task<bool> DeleteChildData(Child child)
    {
        return await _executor.DeleteChildDataAsync(child);
    }
}
```

## Architecture Rules

The following rules are enforced by analyzers and tests:

1. **ARCH001**: No Child parameters in service interfaces (except coordinators)
2. **ARCH002**: No methods returning collections of Child
3. **ARCH003**: Obsolete interfaces must not be used
4. **ARCH004**: Child-aware services must inject IChildContext
5. **ARCH005**: No cross-child operations except in coordinator types

## Validation Checklist

After migration, ensure:

- [ ] All obsolete interface usages are removed
- [ ] No Child parameters in service methods
- [ ] Child-aware services inject IChildContext
- [ ] Tests use mocked IChildContext
- [ ] Architecture tests pass (`dotnet test --filter FullyQualifiedName~ArchitectureTests`)
- [ ] No compiler warnings about obsolete members
- [ ] GDPR operations use the executor pattern

## Performance Considerations

The new architecture has minimal overhead:
- Scope creation: ~0.1ms per operation
- Memory overhead: < 1KB per scope
- No significant impact on throughput

Benefits often outweigh the minimal overhead:
- Improved caching per child
- Better parallelization
- Reduced lock contention

## Troubleshooting

### Error: "Cannot resolve IChildContext"
**Solution**: Ensure you're in a scoped context. IChildContext is only available in scoped services.

### Error: "Child context not set"
**Solution**: Ensure operations are executed through ChildOperationExecutor or ChildServiceCoordinator.

### Warning: "CS0618: 'IDataService' is obsolete"
**Solution**: Migrate to IChildDataService following this guide.

### Test Failure: "ArchitectureTests failing"
**Solution**: Check for Child parameters in your new code. Only coordinator types are allowed to have them.

## Support

For questions about the migration:
1. Check the architecture tests for examples
2. Review the ChildServiceCoordinator implementation
3. See the example in ProgramChildAware.cs.example

## Timeline

- **Phase 1** (Current): Obsolete attributes added, analyzers available
- **Phase 2** (Next Release): Warnings become errors
- **Phase 3** (Future): Legacy interfaces removed completely

Start migrating now to avoid breaking changes in future releases.