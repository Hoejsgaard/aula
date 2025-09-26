# Task 007: Implement Child-Centric Architecture

## ⚠️ IMPORTANT: USE ULTRATHINK MODE
**All implementers must use ultrathink mode when executing this task. Think through each change carefully before implementing.**

## Executive Summary

Transform Aula from service-centric to child-centric architecture where children become first-class citizens with proper isolation and clean code flow.

**Story:** Program → ChildOrchestrator → ChildCoordinators (one per child) → Services (singletons with child parameters)

## Implementation Plan

### Core Principles
- ✅ **Incremental changes** with tests at every step
- ✅ **Proper DI** - always inject dependencies
- ✅ **Use right agents** - @backend for services, @architect for design
- ✅ **Integration test** after each phase with actual program
- ✅ **Clean story** - code should read like documentation

### Configuration During Development
Both children use SAME channels initially:
```json
"Children": [
  {
    "FirstName": TestChild1,
    "SlackChannelId": "C07PZ584Z37",  // Same temporarily
    "TelegramChatId": "-1001234567890"
  },
  {
    "FirstName": "TestChild2",
    "SlackChannelId": "C07PZ584Z37",  // Same temporarily
    "TelegramChatId": "-1001234567890"
  }
]
```

---

## Phase 1: Foundation - ChildOrchestrator (Week 1)
**Agent: @backend | Ultrathink: Required**

### 1.1 Create IChildOrchestrator and ChildOrchestrator
```csharp
public interface IChildOrchestrator
{
    Task StartAsync();
    ChildCoordinator GetCoordinator(string childName);
}

public class ChildOrchestrator : IChildOrchestrator
{
    private readonly List<Child> _children;  // Takes ONLY children, not full config
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, ChildCoordinator> _coordinators = new();

    public ChildOrchestrator(List<Child> children, IServiceProvider services)
    {
        _children = children;
        _services = services;
    }
}
```

### 1.2 Register in DI (Program.cs)
```csharp
services.AddSingleton<IChildOrchestrator>(sp =>
{
    var config = sp.GetRequiredService<Config>();
    return new ChildOrchestrator(config.MinUddannelse.Children, sp);
});
```

### 1.3 Required Tests
- Unit: Orchestrator creates coordinators for each child
- Unit: GetCoordinator returns correct instance
- **Integration: Run actual program, verify it starts**

---

## Phase 2: ChildCoordinator with Proper DI (Week 1-2)
**Agent: @backend | Ultrathink: Required**

### 2.1 Create ChildCoordinator with Injected Dependencies
```csharp
public class ChildCoordinator
{
    private string _childName = string.Empty;

    // ALL dependencies properly injected
    private readonly IAgentService _agent;
    private readonly IDataService _data;
    private readonly ISupabaseService _supabase;
    private readonly IChannelManager _channels;
    private readonly ILogger<ChildCoordinator> _logger;

    public ChildCoordinator(
        IAgentService agent,
        IDataService data,
        ISupabaseService supabase,
        IChannelManager channels,
        ILogger<ChildCoordinator> logger)
    {
        // Proper constructor injection
    }

    public void Initialize(string childName)
    {
        _childName = childName;
    }

    public async Task CheckWeekLetterAsync()
    {
        // Pass childName to singleton services
        var letter = await _agent.GetWeekLetterAsync(_childName, DateOnly.Now);
        // ... clean facade pattern
    }
}
```

### 2.2 Register as Transient
```csharp
services.AddTransient<ChildCoordinator>();  // New instance per child
```

### 2.3 Required Tests
- Unit: Coordinator delegates correctly with mocks
- **Integration: Run program with both children active**

---

## Phase 3: Clean Program.cs (Week 2)
**Agent: @architect | Ultrathink: Required**

### Goal: Program.cs under 30 lines
```csharp
public static async Task Main(string[] args)
{
    var host = CreateHostBuilder(args).Build();

    var orchestrator = host.Services.GetRequiredService<IChildOrchestrator>();
    await orchestrator.StartAsync();

    await host.RunAsync();
}
```

### Required Tests
- **Integration: Full program runs without regression**
- Verify both children process correctly

---

## Phase 4: Fix Reminders - Add Child Scope (Week 3)
**Agent: @backend | Ultrathink: Required**

### 4.1 Update Reminder Model
```csharp
public class Reminder
{
    public string ChildName { get; set; } = string.Empty;  // KEY ADDITION
    // ... rest of properties
}
```

### 4.2 Update ALL Reminder Methods
```csharp
Task<int> AddReminderAsync(string childName, string text, DateOnly date, TimeOnly time);
Task<List<Reminder>> GetPendingRemindersAsync(string childName);
```

### 4.3 Database Migration
```sql
ALTER TABLE reminders ADD COLUMN child_name VARCHAR(100);
```

### Required Tests
- Unit: Child-scoped reminder operations
- **Integration: Reminders work per child**

---

## Phase 5: Bootstrap Per-Child Scheduled Tasks (Week 4)
**Agent: @infrastructure | Ultrathink: Required**

### 5.1 Create Bootstrap Script
```csharp
foreach (var child in children)
{
    // Weekly check - SUNDAY 14:00 (not Monday!)
    await CreateTaskAsync(
        $"weekly-check-{child.FirstName}",
        "0 14 * * SUN",
        "CheckWeekLetter",
        new { childName = child.FirstName }
    );

    // Reminders every 10 minutes
    await CreateTaskAsync(
        $"reminders-{child.FirstName}",
        "*/10 * * * *",
        "ProcessReminders",
        new { childName = child.FirstName }
    );
}

// Remove old global task
await RemoveTaskAsync("weekly-check-global");
```

### Required Tests
- Verify tasks in database
- **Integration: Scheduled tasks execute per child**

---

## Phase 6: Update SchedulingService (Week 5)
**Agent: @backend | Ultrathink: Required**

### Handle Child Context in Tasks
```csharp
case "CheckWeekLetter":
    var childName = context["childName"];
    var coordinator = _orchestrator.GetCoordinator(childName);
    await coordinator.CheckWeekLetterAsync();
    break;
```

### Required Tests
- Unit: Task execution with child context
- **Integration: Tasks run at correct times per child**

---

## Phase 7: Child-Aware Channel Routing (Week 6)
**Agent: @backend | Ultrathink: Required**

### Add Child Parameter to Channel Methods
```csharp
public async Task SendToChildAsync(string childName, string message)
{
    // For now, same channel IDs for both children
    // Later: route to child-specific channels
    var formattedMessage = $"[{childName}] {message}";
    await _channels.SendAsync(formattedMessage);
}
```

### Required Tests
- Unit: Messages include child context
- **Integration: Both children's messages appear**

---

## Phase 8: Final Cleanup & Testing (Week 7)
**Agent: @architect | Ultrathink: Required**

### Checklist
- [ ] Program.cs under 30 lines
- [ ] All tests passing
- [ ] Integration test with both children
- [ ] Performance acceptable
- [ ] Documentation updated

---

## Success Criteria

1. **The Story Test**: Can you read Program → Orchestrator → Coordinator → Services and understand immediately?
2. **Isolation Test**: TestChild1's data never appears in TestChild2's context
3. **Simplicity Test**: No over-engineering, clean facades
4. **Test Coverage**: Every phase has tests, integration tests work

## Risk Mitigation

- **Never skip tests** - Each phase must have passing tests
- **Integration test frequently** - Run actual program after changes
- **Keep working code working** - Feature flags if needed
- **Use ultrathink** - Think before implementing

## Appendices

See `tasks/appendices/child-architecture/` for:
- Detailed architecture diagrams
- Security considerations
- Migration strategies

---

**Remember: ULTRATHINK MODE for all implementation!**