# Task 018: Telegram Bot Instance Conflicts

## Problem Analysis

**Issue:** Multiple Telegram bot polling conflicts when running single application with multiple children.

**Error:**
```
[409] Conflict: terminated by other getUpdates request; make sure that only one bot instance is running
Telegram.Bot.Exceptions.ApiRequestException: Conflict: terminated by other getUpdates request
```

**Root Cause:**
- Both children (Søren Johannes & Hans Martin) use the same Telegram bot token
- Each child creates separate `TelegramInteractiveBot` instances
- Telegram API only allows **one active polling connection per bot token**
- When both children attempt polling simultaneously → 409 Conflict

**Scope:**
- Occurs with **single application instance** managing multiple children
- Not related to multiple application instances running
- Affects Telegram interactive functionality only (message sending works fine)

## Current Workaround (Applied)

**Solution:** Disable interactive polling for all children
```json
"Telegram": {
  "EnableInteractive": false  // Disables polling, keeps notifications
}
```

**Configuration Changes:**
- Both Søren Johannes and Hans Martin: `EnableInteractive: false`
- Telegram bots can still send notifications/reminders
- No interactive chat functionality (no user message processing)

**Result:** Eliminates 409 conflicts immediately.

## Long-term Solutions (Future Implementation)

### Option 1: Single Shared Telegram Bot Manager (Recommended)
```csharp
public class SharedTelegramBotManager
{
    private readonly TelegramBotClient _botClient;
    private readonly Dictionary<long, Child> _chatIdToChild;

    // Single bot instance handles all children
    // Routes messages based on ChatId
}
```

### Option 2: Create Separate Bot Tokens
- Create additional Telegram bots via @BotFather
- Assign unique tokens per child
- Full isolation but requires managing multiple bot identities

### Option 3: Chat-based Routing
- Single bot with different chat/channel assignments
- Route by chat ID instead of separate bot instances

## Status
- ✅ **Workaround Applied:** Interactive polling disabled
- ✅ **Immediate Issue Resolved:** No more 409 conflicts
- ⚠️ **Functionality Limited:** No interactive chat support
- **Future Work:** Implement proper shared bot management

## Impact
- **Notifications:** ✅ Working (reminders, week letters)
- **Interactive Chat:** ❌ Disabled (cannot process user messages)
- **System Stability:** ✅ Improved (no polling conflicts)