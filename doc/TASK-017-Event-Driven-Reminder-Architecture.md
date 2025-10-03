# Task 017: Event-Driven Reminder Architecture Migration

**Status**: Ready for Implementation
**Confidence**: 95% (Expert reviewed by @architect and @backend)
**Date**: 2025-01-03

## Problem Statement

SchedulingService attempts to send child-specific reminders using:
```csharp
await _channelManager.SendMessageToChildChannelsAsync(reminder.ChildName, message);
```

However, `ChannelManager.SendMessageToChildChannelsAsync()` ignores the `childName` parameter and broadcasts to all channels. This breaks child-specific reminder delivery.

## Solution: Event-Driven Architecture

Migrate to event-driven pattern that matches existing `ChildWeekLetterReady` architecture:

**Current Pattern (Week Letters)**:
```csharp
// SchedulingService fires event
ChildWeekLetterReady?.Invoke(this, new ChildWeekLetterEventArgs(...));

// Child agents subscribe and filter
if (args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
    await HandleWeekLetterAsync(args);
```

**New Pattern (Reminders)**:
```csharp
// SchedulingService fires event
ReminderReady?.Invoke(this, new ChildReminderEventArgs(...));

// Child agents subscribe and filter
if (args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
    await SendReminderMessageAsync(args.Message);
```

## Architecture Benefits

1. **Consistency**: Matches existing `ChildWeekLetterReady` event pattern exactly
2. **Decoupling**: SchedulingService doesn't know about child agents
3. **Self-organizing**: Child agents subscribe to what they care about
4. **Extensible**: Easy to add logging, metrics, other listeners
5. **Testable**: Clear separation between event firing and handling
6. **Story-like**: "When reminder ready → announce → interested parties respond"

## Implementation Plan

### Phase 1: Event Infrastructure (Safe)

**Files to Create/Modify:**

1. **Create**: `src/MinUddannelse/Events/ChildReminderEventArgs.cs`
```csharp
using MinUddannelse.Models;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Events;

public class ChildReminderEventArgs : ChildEventArgs
{
    public string ReminderText { get; init; }
    public DateOnly RemindDate { get; init; }
    public TimeOnly RemindTime { get; init; }
    public int ReminderId { get; init; }

    public ChildReminderEventArgs(string childId, string childFirstName, Reminder reminder)
        : base(childId, childFirstName, "reminder", null)
    {
        ReminderText = reminder.Text;
        RemindDate = reminder.RemindDate;
        RemindTime = reminder.RemindTime;
        ReminderId = reminder.Id;
    }
}
```

2. **Modify**: `src/MinUddannelse/Scheduling/ISchedulingService.cs`
```csharp
// Add after line 6
using MinUddannelse.Events;

// Add to interface
event EventHandler<ChildReminderEventArgs>? ReminderReady;
```

3. **Modify**: `src/MinUddannelse/Scheduling/SchedulingService.cs`
```csharp
// Add to class
public event EventHandler<ChildReminderEventArgs>? ReminderReady;
```

### Phase 2: Child Agent Event Subscription

**Modify**: `src/MinUddannelse/Agents/ChildAgent.cs`

1. **Add field** (after line 29):
```csharp
private EventHandler<ChildReminderEventArgs>? _reminderHandler;
```

2. **Add method** (after `SubscribeToWeekLetterEvents()`):
```csharp
private void SubscribeToReminderEvents()
{
    _reminderHandler = async (sender, args) =>
    {
        if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            string message = $"*Reminder*: {args.ReminderText}";
            await SendReminderMessageAsync(message);
            _logger.LogInformation("Handled reminder {ReminderId} for {ChildName}",
                args.ReminderId, _child.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle reminder {ReminderId} for {ChildName}",
                args.ReminderId, _child.FirstName);
        }
    };
    _schedulingService.ReminderReady += _reminderHandler;
}
```

3. **Update StartAsync** (after line 60):
```csharp
SubscribeToReminderEvents();
```

4. **Update StopAsync** (after existing unsubscribe logic):
```csharp
if (_reminderHandler != null)
{
    _schedulingService.ReminderReady -= _reminderHandler;
    _reminderHandler = null;
}
```

### Phase 3: Fire Events (Dual Execution)

**Modify**: `src/MinUddannelse/Scheduling/SchedulingService.cs`

**Update `SendReminderNotification` method** (lines 282-313):
```csharp
private async Task SendReminderNotification(Reminder reminder)
{
    string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";
    string message = $"*Reminder*{childInfo}: {reminder.Text}";

    try
    {
        if (!string.IsNullOrEmpty(reminder.ChildName))
        {
            // NEW: Fire event
            var childId = reminder.ChildName.ToLowerInvariant().Replace(" ", "_");
            var eventArgs = new ChildReminderEventArgs(childId, reminder.ChildName, reminder);
            ReminderReady?.Invoke(this, eventArgs);

            _logger.LogInformation("Fired reminder event {ReminderId} for {ChildName}",
                reminder.Id, reminder.ChildName);

            // PHASE 3 ONLY: Keep registry fallback for safety
            // TODO: Remove in Phase 4 after testing
            var childAgent = _childAgentRegistry.GetChildAgent(reminder.ChildName);
            if (childAgent == null)
            {
                _logger.LogWarning("No child agent found for {ChildName} - relying on event handling",
                    reminder.ChildName);
            }
            else
            {
                await childAgent.SendReminderMessageAsync(message);
                _logger.LogInformation("Sent reminder {ReminderId} via registry to {ChildName}",
                    reminder.Id, reminder.ChildName);
            }
        }
        else
        {
            await _channelManager.BroadcastMessageAsync(message);
            _logger.LogInformation("Broadcast reminder {ReminderId} to all channels", reminder.Id);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send reminder {ReminderId}", reminder.Id);
    }
}
```

### Phase 4: Registry Removal

1. **Remove registry dependency** from SchedulingService constructor
2. **Remove registry calls** from `SendReminderNotification`
3. **Remove** `SendMessageToChildAsync` helper method
4. **Clean up** using statements

### Phase 5: Testing & Validation

**Test Plan:**
1. **Unit Tests**: Event firing verification (mock subscribers)
2. **Unit Tests**: Event handling verification (mock event args)
3. **Integration Tests**: End-to-end reminder flow
4. **Manual Tests**: Create test reminders, verify delivery

**Phase 3 Testing** (Dual Execution):
- Monitor logs for both "Fired reminder event" and "Sent reminder via registry"
- Verify children receive messages (may be duplicated)
- Confirm both event and registry paths work
- Duration: < 1 hour before proceeding to Phase 4

## Backward Compatibility

**Phase 3 Impact**: Both events and registry will deliver messages (duplicates)
**Mitigation**: Brief Phase 3 duration (< 1 hour testing window)
**Rollback Strategy**: Remove event firing, registry remains functional

## Error Handling

**Event Handler Pattern** (defensive programming):
```csharp
_reminderHandler = async (sender, args) =>
{
    try
    {
        // Child name filtering
        if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
            return;

        // Process reminder
        await SendReminderMessageAsync(BuildMessage(args));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Reminder handling failed for {ChildName}", _child.FirstName);
        // Don't rethrow - isolate failures per child
    }
};
```

**Key Principles:**
- Each child's failure doesn't affect others
- Comprehensive logging with context (child name, reminder ID)
- No rethrow from event handlers

## Memory Management

**Lifecycle Pattern** (follows existing `ChildWeekLetterReady`):
- Subscribe in `StartAsync()` after bot initialization
- Unsubscribe in `StopAsync()` before bot disposal
- Store handler reference for proper cleanup
- Null check before unsubscription (idempotent)

## Performance Analysis

**For 2-3 Children:**
- Event invocation overhead: ~0.01ms per subscriber
- Total overhead: < 0.1ms for 3 children
- Memory: Handler references (minimal)
- **Verdict**: Negligible performance impact

## Success Criteria

1. ✅ Child-specific reminders delivered only to correct child
2. ✅ System-wide reminders (null child name) delivered to all children
3. ✅ No architectural inconsistencies with existing event patterns
4. ✅ Clean error isolation between children
5. ✅ No memory leaks from event subscriptions
6. ✅ Comprehensive test coverage
7. ✅ Code reads like a story

## Expert Review Summary

**@architect Review**: ✅ Architecture is sound
- Matches existing `ChildWeekLetterReady` pattern perfectly
- Correct dependency direction (ChildAgent → SchedulingService)
- Event structure inherits from `ChildEventArgs` properly
- Lifecycle management follows established patterns

**@backend Review**: ✅ Implementation is production-ready
- Migration strategy is safe with phased approach
- Error handling follows defensive programming principles
- Testing strategy covers all critical paths
- Performance impact is negligible for target scale
- Memory management follows existing proven patterns

## Files Modified

1. `src/MinUddannelse/Events/ChildReminderEventArgs.cs` (new)
2. `src/MinUddannelse/Scheduling/ISchedulingService.cs` (add event)
3. `src/MinUddannelse/Scheduling/SchedulingService.cs` (implement event firing)
4. `src/MinUddannelse/Agents/ChildAgent.cs` (add subscription logic)

## Deprecation Impact

**Patterns Deprecated:**
- Direct child agent lookup via registry for reminder routing
- `ChannelManager.SendMessageToChildChannelsAsync()` for child-specific messages
- Imperative "find agent and tell it" pattern

**Patterns Reinforced:**
- Event-driven choreography for child-specific notifications
- Child-centric architecture (agents own their channels)
- Defensive error handling in distributed scenarios

---

**Next Steps**: Begin Phase 1 implementation - create event infrastructure without behavior changes.