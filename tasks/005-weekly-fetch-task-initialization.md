# Task 005: Weekly Fetch Task Database Initialization

**Status:** INVESTIGATED ✅
**Priority:** High
**Created:** 2025-01-24
**Investigation Completed:** 2025-01-24

## Investigation Results

### Finding
The `WeeklyLetterCheck` scheduled task is **NOT automatically created** in the database on application startup. The system expects this task to exist but never creates it.

### Evidence
1. **SchedulingService** has full support for executing `WeeklyLetterCheck` task:
   - src/Aula/Scheduling/SchedulingService.cs:220-221
   - Comprehensive test coverage in SchedulingServiceTests.cs

2. **Repository Pattern** only supports:
   - `GetScheduledTasksAsync()` - retrieve tasks
   - `GetScheduledTaskAsync(string name)` - get specific task
   - `UpdateScheduledTaskAsync(ScheduledTask task)` - update existing task
   - **NO CREATE METHOD EXISTS**

3. **No Initialization Code Found**:
   - No SQL scripts to seed the database
   - No startup initialization in Program.cs
   - No migration files creating default tasks

### Current Problem
Users must manually insert the `WeeklyLetterCheck` task into the database for automatic week letter checking to work. Without this task, the scheduling service runs but never checks for new week letters.

## Proposed Solution

### Option 1: Add Creation Method to Repository (Recommended)
1. Add `CreateScheduledTaskAsync` method to `IScheduledTaskRepository`
2. Implement in `ScheduledTaskRepository`
3. Check and create default task on startup in Program.cs

### Option 2: Database Migration
1. Create SQL migration script
2. Insert default `WeeklyLetterCheck` task
3. Run migration on startup

### Implementation Plan

#### Step 1: Extend Repository
```csharp
// IScheduledTaskRepository.cs
Task<ScheduledTask> CreateScheduledTaskAsync(ScheduledTask task);
```

#### Step 2: Add Startup Initialization
```csharp
// Program.cs - After SupabaseService initialization
private static async Task InitializeDefaultScheduledTasks(ISupabaseService supabaseService, ILogger logger)
{
    var existingTask = await supabaseService.GetScheduledTaskAsync("WeeklyLetterCheck");
    if (existingTask == null)
    {
        logger.LogInformation("Creating default WeeklyLetterCheck scheduled task");
        var weeklyTask = new ScheduledTask
        {
            Name = "WeeklyLetterCheck",
            Description = "Check for new weekly letters every Sunday at 4 PM",
            CronExpression = "0 16 * * 0", // Sundays at 4 PM
            Enabled = true,
            RetryIntervalHours = 2,
            MaxRetryHours = 48,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await supabaseService.CreateScheduledTaskAsync(weeklyTask);
    }
}
```

#### Step 3: Default Tasks Configuration
```json
{
  "DefaultScheduledTasks": [
    {
      "Name": "WeeklyLetterCheck",
      "CronExpression": "0 16 * * 0",
      "Description": "Check for new weekly letters",
      "RetryIntervalHours": 2,
      "MaxRetryHours": 48
    },
    {
      "Name": "ReminderCheck",
      "CronExpression": "*/10 * * * *",
      "Description": "Check for pending reminders",
      "Enabled": true
    }
  ]
}
```

## Files to Modify
- src/Aula/Repositories/IScheduledTaskRepository.cs
- src/Aula/Repositories/ScheduledTaskRepository.cs
- src/Aula/Services/SupabaseService.cs
- src/Aula/Program.cs
- appsettings.json

## Testing
- Verify task is created on first run
- Verify task is not duplicated on subsequent runs
- Test that weekly letter checking works automatically
- Verify cron expression triggers at correct time

## Impact
This fix will enable zero-configuration operation - users won't need to manually create database entries for the core scheduling functionality to work.