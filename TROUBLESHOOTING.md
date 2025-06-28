# Troubleshooting Guide

## Reminder System Issues

### Problem: Reminders Only Sent to Slack, Not Telegram

**Symptoms:**
- Reminders created successfully via Telegram or Slack
- Reminder appears in Slack channel when due
- Reminder does NOT appear in Telegram channel
- No error messages in logs
- App appears to run normally

**Root Cause:**
The SchedulingService timer was never starting due to a startup order issue. Interactive bots (particularly Slack polling) were blocking the main startup thread, preventing the SchedulingService initialization from completing.

**Investigation Steps:**
1. Check for "🔥 TIMER FIRED" messages in logs - if missing, timer isn't running
2. Look for "🚀 SchedulingService.StartAsync completed" - if missing, startup blocked
3. Verify reminder creation works but execution fails

**Solution:**
Move SchedulingService startup before interactive bot initialization in `Program.cs`:

```csharp
// BEFORE - SchedulingService started after bots (BROKEN)
await slackBot.Start();
await telegramBot.Start();
await schedulingService.StartAsync(); // Never reached!

// AFTER - SchedulingService started first (FIXED)
await schedulingService.StartAsync();
await slackBot.Start();
await telegramBot.Start();
```

**Additional Fixes Applied:**
- Fixed `async void` timer callback causing silent failures
- Added proper exception handling in timer execution
- Improved Telegram channel ID parsing with validation
- Enhanced logging with timestamps and reduced spam

**Files Modified:**
- `Program.cs` - Startup order change
- `SchedulingService.cs` - Timer callback fix and error handling
- `SlackInteractiveBot.cs` - Reduced log spam

**Verification:**
After fix, logs should show:
```
🔥 TIMER FIRED - CheckScheduledTasksWrapper called at [timestamp]
Sent reminder X to Slack
Sent reminder X to Telegram
```

---

## Common Debugging Patterns

### Timer Not Firing
**Check for:** Missing "🔥 TIMER FIRED" messages
**Cause:** SchedulingService not started or async void exceptions
**Fix:** Ensure proper startup order and Task.Run wrapper

### Startup Blocking
**Check for:** Logs stopping after bot initialization
**Cause:** Blocking calls in startup sequence
**Fix:** Move critical services before potentially blocking operations

### Silent Failures
**Check for:** Expected operations not logging
**Cause:** Unhandled exceptions in async void methods
**Fix:** Use proper async Task patterns with exception handling