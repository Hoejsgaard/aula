# Task 018: Fix Telegram Bot Polling Conflicts

**Status**: Ready for Investigation
**Priority**: Medium
**Date**: 2025-01-03

## Problem Statement

During reminder testing, multiple instances of the MinUddannelse application were running simultaneously, causing Telegram bot polling conflicts. This results in:

1. **Multiple polling sessions** competing for the same Telegram bot token
2. **Message delivery failures** due to webhook/polling conflicts
3. **Resource waste** from duplicate application instances
4. **Unpredictable behavior** in message routing

## Symptoms Observed

- Multiple background bash processes running `dotnet run`
- Telegram bot connection errors in logs
- Inconsistent message delivery
- Application startup conflicts

## Root Cause Analysis

**Primary Causes:**
1. **Multiple application instances** started during testing without proper cleanup
2. **No singleton enforcement** - application doesn't prevent multiple instances
3. **Background process management** - bash processes not properly terminated
4. **Development workflow** - easy to accidentally start multiple instances during testing

## Solution Options

### Option 1: Application-Level Singleton (Recommended)

Implement mutex-based singleton pattern to prevent multiple instances:

```csharp
// Program.cs - Add singleton check
private static readonly Mutex ApplicationMutex = new(true, "MinUddannelse.SingleInstance");

public static async Task Main(string[] args)
{
    if (!ApplicationMutex.WaitOne(TimeSpan.Zero, true))
    {
        Console.WriteLine("MinUddannelse is already running. Exiting.");
        return;
    }

    try
    {
        // Existing application logic
        await RunApplicationAsync();
    }
    finally
    {
        ApplicationMutex.ReleaseMutex();
    }
}
```

### Option 2: Process Management Improvements

**Development Workflow:**
- Add `--single-instance` flag check
- Improve process cleanup in development scripts
- Better error messages when conflicts detected

**Telegram Bot Configuration:**
- Implement bot session management
- Add connection retry with backoff
- Graceful handling of polling conflicts

### Option 3: Docker/Container Approach

**Production Deployment:**
- Run application in Docker container
- Container orchestration ensures single instance
- Proper lifecycle management

## Implementation Plan

### Phase 1: Immediate Fix (Application Singleton)

1. **Add Mutex-Based Singleton Check**
   - Modify `Program.cs` Main method
   - Use named mutex "MinUddannelse.SingleInstance"
   - Exit gracefully if instance already running

2. **Improve Error Messaging**
   - Clear message when singleton conflict detected
   - Guidance on how to stop existing instance
   - Log existing instance detection

### Phase 2: Enhanced Bot Management

1. **Telegram Bot Session Management**
   - Check for existing bot sessions before starting
   - Implement session cleanup on shutdown
   - Add connection health monitoring

2. **Graceful Shutdown Improvements**
   - Ensure all bot connections properly closed
   - Release mutex on all exit paths
   - Handle SIGTERM/SIGINT properly

### Phase 3: Development Experience

1. **Development Scripts**
   - Add `scripts/stop-all.sh` to kill all instances
   - Add `scripts/status.sh` to check running instances
   - Integrate into development workflow

2. **Configuration Validation**
   - Warn if multiple instances detected during startup
   - Validate Telegram token uniqueness
   - Check for port conflicts (future webhook mode)

## Implementation Details

### Singleton Implementation

```csharp
public class Program
{
    private static readonly Mutex ApplicationMutex = new(true, "MinUddannelse.SingleInstance.{Environment.UserName}");

    public static async Task Main(string[] args)
    {
        // Check for existing instance
        if (!ApplicationMutex.WaitOne(TimeSpan.Zero, true))
        {
            var logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<Program>();

            logger.LogError("Another instance of MinUddannelse is already running.");
            logger.LogInformation("To stop the existing instance, use: pkill -f 'dotnet.*MinUddannelse'");
            return;
        }

        try
        {
            var serviceProvider = ConfigureServices();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(nameof(Program));

            logger.LogInformation("Starting MinUddannelse (singleton instance acquired)");

            // Existing application logic...
        }
        finally
        {
            ApplicationMutex.ReleaseMutex();
        }
    }
}
```

### Development Scripts

**scripts/stop-all.sh:**
```bash
#!/bin/bash
echo "Stopping all MinUddannelse instances..."
pkill -f "dotnet.*MinUddannelse" || echo "No instances found"
echo "Cleanup complete"
```

**scripts/status.sh:**
```bash
#!/bin/bash
echo "Checking MinUddannelse instances..."
pgrep -af "dotnet.*MinUddannelse" || echo "No instances running"
```

## Testing Strategy

### Unit Tests
- Mock mutex behavior
- Test singleton enforcement
- Verify graceful exit when instance exists

### Integration Tests
- Start multiple instances simultaneously
- Verify only one succeeds
- Test proper cleanup on shutdown

### Manual Testing
1. Start application normally
2. Attempt to start second instance
3. Verify second instance exits gracefully
4. Stop first instance, verify mutex released
5. Start new instance, verify it succeeds

## Error Handling

### Mutex Acquisition Failure
```csharp
if (!ApplicationMutex.WaitOne(TimeSpan.Zero, true))
{
    Console.WriteLine("❌ MinUddannelse is already running");
    Console.WriteLine("📋 To stop existing instance: pkill -f 'dotnet.*MinUddannelse'");
    Console.WriteLine("📋 To check status: pgrep -af 'dotnet.*MinUddannelse'");
    Environment.Exit(1);
}
```

### Cleanup on Unexpected Exit
```csharp
AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
{
    try
    {
        ApplicationMutex?.ReleaseMutex();
    }
    catch (Exception ex)
    {
        // Log but don't throw during shutdown
        Console.WriteLine($"Error releasing mutex: {ex.Message}");
    }
};
```

## Success Criteria

1. ✅ Only one MinUddannelse instance can run at a time
2. ✅ Clear error message when attempting to start duplicate
3. ✅ Proper mutex cleanup on all exit scenarios
4. ✅ No Telegram bot polling conflicts
5. ✅ Graceful handling of development workflow
6. ✅ Easy way to stop all instances for development

## Related Issues

- **Telegram API conflicts** when multiple bots poll same token
- **Development workflow** confusion with multiple instances
- **Resource usage** from duplicate processes
- **Testing reliability** affected by instance conflicts

## Files to Modify

1. `src/MinUddannelse/Program.cs` - Add singleton enforcement
2. `scripts/stop-all.sh` - Development utility (new)
3. `scripts/status.sh` - Development utility (new)
4. `.gitignore` - Exclude script logs if needed

## Priority

**Medium Priority** - Not blocking core functionality but improves:
- Development experience
- System reliability
- Resource usage
- Debugging clarity

---

**Next Steps**: Implement Phase 1 (application singleton) after Task 017 (event-driven reminders) is complete.