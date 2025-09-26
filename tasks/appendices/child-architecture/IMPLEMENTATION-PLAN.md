# Implementation Plan: Child-Centric Architecture

## The Story We're Building

**From:** "Program.cs does everything in a messy way with children scattered everywhere"

**To:** "Program starts → Creates Orchestrator → Orchestrator manages Child Coordinators → Each Coordinator handles one child's world → Clean, testable, beautiful"

## Core Principles

✅ **Incremental Changes** - Each phase builds on the last
✅ **Test at Every Step** - Unit tests AND integration tests
✅ **Use the Right Agent** - @backend for services, @architect for design, @infrastructure for DB
✅ **Proper DI** - Dependencies injected, not created
✅ **Tell a Clean Story** - Code should read like documentation

## Configuration Note

During implementation, configure both children with SAME channel IDs:
```json
"Children": [
  {
    "FirstName": TestChild1,
    "SlackChannelId": "C07PZ584Z37",  // Same for now
    "TelegramChatId": "-1001234567890"  // Same for now
  },
  {
    "FirstName": "TestChild2",
    "SlackChannelId": "C07PZ584Z37",  // Same for now
    "TelegramChatId": "-1001234567890"  // Same for now
  }
]
```

---

## Phase 1: Foundation - ChildOrchestrator
**Week 1 | Agent: @backend | Tests: Required**

### 1.1 Create ChildOrchestrator
```csharp
public interface IChildOrchestrator
{
    Task StartAsync();
    ChildCoordinator GetCoordinator(string childName);
}

public class ChildOrchestrator : IChildOrchestrator
{
    private readonly List<Child> _children;  // NOT IConfiguration!
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, ChildCoordinator> _coordinators = new();

    public ChildOrchestrator(List<Child> children, IServiceProvider services)
    {
        _children = children;
        _services = services;
    }

    public async Task StartAsync()
    {
        foreach (var child in _children)
        {
            var coordinator = _services.GetRequiredService<ChildCoordinator>();
            coordinator.Initialize(child.FirstName);
            _coordinators[child.FirstName] = coordinator;
        }
    }
}
```

### 1.2 Register in DI
```csharp
// In Program.cs ConfigureServices
services.AddSingleton<IChildOrchestrator>(sp =>
{
    var config = sp.GetRequiredService<Config>();
    return new ChildOrchestrator(config.MinUddannelse.Children, sp);
});
```

### 1.3 Tests
- ✅ Unit: ChildOrchestrator creates coordinators for each child
- ✅ Unit: GetCoordinator returns correct coordinator
- ✅ Integration: Run program, verify orchestrator starts

---

## Phase 2: ChildCoordinator with Proper DI
**Week 1-2 | Agent: @backend | Tests: Required**

### 2.1 Create ChildCoordinator
```csharp
public class ChildCoordinator
{
    private string _childName = string.Empty;

    // Properly injected dependencies
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
        _agent = agent;
        _data = data;
        _supabase = supabase;
        _channels = channels;
        _logger = logger;
    }

    public void Initialize(string childName)
    {
        _childName = childName;
    }

    public async Task CheckWeekLetterAsync()
    {
        _logger.LogInformation("Checking week letter for {Child}", _childName);

        // Clean facade pattern
        var letter = await _agent.GetWeekLetterAsync(_childName, DateOnly.Now);
        if (letter != null && IsNew(letter))
        {
            await _channels.SendAsync($"New week letter for {_childName}", letter);
            await _supabase.StoreWeekLetterAsync(_childName, GetWeek(), GetYear(),
                GetHash(letter), letter.ToString());
        }
    }
}
```

### 2.2 Register in DI
```csharp
services.AddTransient<ChildCoordinator>();  // Transient - created per child
```

### 2.3 Tests
- ✅ Unit: ChildCoordinator delegates to services correctly
- ✅ Unit: CheckWeekLetterAsync with mocked services
- ✅ Integration: Run program with coordinators active

---

## Phase 3: Wire into Program.cs
**Week 2 | Agent: @architect | Tests: Required**

### 3.1 Clean Program.cs
```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Start orchestrator
        var orchestrator = host.Services.GetRequiredService<IChildOrchestrator>();
        await orchestrator.StartAsync();

        // Start scheduling
        var scheduler = host.Services.GetRequiredService<ISchedulingService>();
        await scheduler.StartAsync();

        // Start channels
        var channels = host.Services.GetRequiredService<IChannelManager>();
        await channels.StartAsync();

        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.Configure<Config>(context.Configuration);
                services.AddChildOrchestration();
                services.AddScheduling();
                services.AddChannels();
            });
}
```

### 3.2 Tests
- ✅ Integration: Full program runs with both children
- ✅ Integration: Both children use same channels (temporary)
- ✅ Verify: No functional regression

---

## Phase 4: Fix Reminders
**Week 3 | Agent: @backend | Tests: Required**

### 4.1 Update Reminder Model
```csharp
public class Reminder
{
    public int Id { get; set; }
    public string ChildName { get; set; } = string.Empty;  // NEW
    public string Text { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
}
```

### 4.2 Update Service Methods
```csharp
public interface ISupabaseService
{
    // Update all reminder methods
    Task<int> AddReminderAsync(string childName, string text, DateOnly date, TimeOnly time);
    Task<List<Reminder>> GetPendingRemindersAsync(string childName);
}
```

### 4.3 Database Migration
```sql
ALTER TABLE reminders ADD COLUMN child_name VARCHAR(100);
UPDATE reminders SET child_name = 'TestChild1' WHERE child_name IS NULL;  -- Temporary
```

### 4.4 Tests
- ✅ Unit: ReminderService with child scoping
- ✅ Integration: Reminders work per child
- ✅ Verify: Old reminders still function

---

## Phase 5: Bootstrap Per-Child Tasks
**Week 4 | Agent: @infrastructure | Tests: Required**

### 5.1 Create Bootstrap Script
```csharp
public class SchedulingBootstrap
{
    public async Task EnsurePerChildTasksAsync(List<Child> children)
    {
        foreach (var child in children)
        {
            // Weekly letter check - SUNDAY 14:00
            await CreateOrUpdateTaskAsync(
                $"weekly-check-{child.FirstName}",
                "0 14 * * SUN",
                "CheckWeekLetter",
                new { childName = child.FirstName }
            );

            // Reminders - every 10 minutes
            await CreateOrUpdateTaskAsync(
                $"reminders-{child.FirstName}",
                "*/10 * * * *",
                "ProcessReminders",
                new { childName = child.FirstName }
            );
        }

        // Remove old global task
        await RemoveTaskIfExistsAsync("weekly-check-global");
    }
}
```

### 5.2 Tests
- ✅ Unit: Bootstrap creates correct tasks
- ✅ Integration: Tasks appear in database
- ✅ Integration: Old task removed

---

## Phase 6: Update SchedulingService
**Week 5 | Agent: @backend | Tests: Required**

### 6.1 Add Child Context Support
```csharp
public class SchedulingService
{
    private async Task ExecuteScheduledTask(ScheduledTask task)
    {
        var context = JsonSerializer.Deserialize<TaskContext>(task.Context);

        switch (task.TaskType)
        {
            case "CheckWeekLetter":
                var coordinator = _orchestrator.GetCoordinator(context.ChildName);
                await coordinator.CheckWeekLetterAsync();
                break;

            case "ProcessReminders":
                var coordinator = _orchestrator.GetCoordinator(context.ChildName);
                await coordinator.ProcessRemindersAsync();
                break;
        }
    }
}
```

### 6.2 Tests
- ✅ Unit: Task execution with child context
- ✅ Integration: Scheduled tasks run per child
- ✅ Verify: Tasks execute at correct times

---

## Phase 7: Child-Aware Channels
**Week 6 | Agent: @backend | Tests: Required**

### 7.1 Add Child Routing
```csharp
public class ChannelManager : IChannelManager
{
    public async Task SendToChildAsync(string childName, string message)
    {
        var config = GetChildConfig(childName);

        // For now, same channels for both children
        // Later: route to child-specific channels

        if (config.SlackEnabled)
            await _slack.SendAsync(config.SlackChannelId, $"[{childName}] {message}");

        if (config.TelegramEnabled)
            await _telegram.SendAsync(config.TelegramChatId, $"[{childName}] {message}");
    }
}
```

### 7.2 Tests
- ✅ Unit: Messages include child context
- ✅ Integration: Both children's messages appear (same channel for now)
- ✅ Verify: Ready for per-child channels later

---

## Phase 8: Final Cleanup
**Week 7 | Agent: @architect | Tests: Required**

### 8.1 Simplify Program.cs to ~20 Lines
```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.Configure<Config>(context.Configuration);
                services.AddAulaServices();
            })
            .Build()
            .RunAulaAsync();  // Extension method encapsulates startup
    }
}
```

### 8.2 Final Testing
- ✅ Full integration test with both children
- ✅ Verify all features work
- ✅ Performance testing
- ✅ Load testing with scheduled tasks

---

## Success Criteria

### Functional
- ✅ Both children's week letters fetched independently
- ✅ Reminders scoped to correct child
- ✅ Scheduled tasks run per child
- ✅ No data leakage between children

### Code Quality
- ✅ Program.cs under 30 lines
- ✅ Clear separation of concerns
- ✅ Proper DI throughout
- ✅ Tests for every phase

### The Story Test
Read Program.cs → ChildOrchestrator → ChildCoordinator → Services

**Does it tell a clear story?** If yes, we've succeeded.

---

## Risk Mitigation

1. **Always test incrementally** - Never move to next phase without tests passing
2. **Keep old code working** - Feature flags if needed
3. **Integration test frequently** - Run the actual program after each phase
4. **Use the right agent** - @backend for services, @architect for design
5. **Document decisions** - Update docs as we go

---

## Timeline

- **Week 1-2**: Foundation (Orchestrator + Coordinator)
- **Week 3**: Reminders fix
- **Week 4**: Bootstrap scheduling
- **Week 5**: Update scheduling service
- **Week 6**: Channel routing
- **Week 7**: Cleanup and polish

Total: **7 weeks** with incremental delivery and testing throughout.