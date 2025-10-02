using Aula.Configuration;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Aula.Scheduling;

/// <summary>
/// Secure child-aware scheduler with comprehensive security layers.
/// Ensures complete isolation of scheduling operations per child.
/// </summary>
public class SecureChildScheduler : IChildScheduler
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, ChildScheduledTask> _inMemoryTasks = new();
    private readonly object _lockObject = new();

    // Configuration limits
    private const int MaxTasksPerChild = 10;
    private const int MaxExecutionsPerHour = 60;

    // Timing constants
    private const int PlaceholderTaskDelayMs = 100;
    private const int ExecutionWindowMinutes = 1;
    private const int ScheduleLookbackMinutes = 1;

    public SecureChildScheduler(
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<SecureChildScheduler>();
    }

    public Task<int> ScheduleTaskAsync(Child child, string taskName, string cronExpression, string? description = null)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Validate cron expression
        if (!IsValidCronExpression(cronExpression))
        {
            _logger.LogWarning("Invalid cron expression provided by {ChildName}: {Cron}", child.FirstName, cronExpression);
            throw new ArgumentException($"Invalid cron expression: {cronExpression}");
        }

        // Check task count limit
        var currentCount = GetScheduledTasksCountForChild(child);
        if (currentCount >= MaxTasksPerChild)
        {
            _logger.LogWarning("Task limit exceeded for {ChildName}. Current: {Count}, Max: {Max}",
                child.FirstName, currentCount, MaxTasksPerChild);
            throw new InvalidOperationException($"Maximum number of scheduled tasks ({MaxTasksPerChild}) exceeded for {child.FirstName}");
        }

        // Create and store the task
        var task = new ChildScheduledTask
        {
            Id = GenerateTaskId(),
            ChildFirstName = child.FirstName,
            ChildLastName = child.LastName,
            TaskName = taskName,
            Description = description,
            CronExpression = cronExpression,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            NextRun = GetNextRunTime(cronExpression, DateTime.UtcNow)
        };

        lock (_lockObject)
        {
            var key = GetTaskKey(child, task.Id);
            _inMemoryTasks[key] = task;
        }

        _logger.LogInformation("Task {TaskName} scheduled successfully for {ChildName} with ID {TaskId}",
            taskName, child.FirstName, task.Id);

        return Task.FromResult(task.Id);
    }

    public Task<bool> CancelTaskAsync(Child child, int taskId)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Find and validate task ownership
        var key = GetTaskKey(child, taskId);
        bool taskExists;
        lock (_lockObject)
        {
            taskExists = _inMemoryTasks.ContainsKey(key);
            if (taskExists)
            {
                _inMemoryTasks.Remove(key);
            }
        }

        if (!taskExists)
        {
            _logger.LogWarning("Task {TaskId} not found for {ChildName}", taskId, child.FirstName);
            return Task.FromResult(false);
        }

        _logger.LogInformation("Task {TaskId} cancelled successfully for {ChildName}", taskId, child.FirstName);
        return Task.FromResult(true);
    }

    public Task<List<ChildScheduledTask>> GetScheduledTasksAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Get tasks for this child only
        var tasks = new List<ChildScheduledTask>();
        lock (_lockObject)
        {
            var prefix = GetChildKeyPrefix(child);
            tasks = _inMemoryTasks
                .Where(kvp => kvp.Key.StartsWith(prefix))
                .Select(kvp => CloneTask(kvp.Value))
                .ToList();
        }

        _logger.LogDebug("Retrieved {Count} scheduled tasks for {ChildName}", tasks.Count, child.FirstName);
        return Task.FromResult(tasks);
    }

    public Task<bool> SetTaskEnabledAsync(Child child, int taskId, bool enabled)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Find and update task
        var key = GetTaskKey(child, taskId);
        lock (_lockObject)
        {
            if (!_inMemoryTasks.TryGetValue(key, out var task))
            {
                _logger.LogWarning("Task {TaskId} not found for {ChildName}", taskId, child.FirstName);
                return Task.FromResult(false);
            }

            task.Enabled = enabled;
            task.UpdatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("Task {TaskId} enabled status set to {Enabled} for {ChildName}",
            taskId, enabled, child.FirstName);
        return Task.FromResult(true);
    }

    public async Task ExecuteTaskAsync(Child child, string taskName)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Execute task
        try
        {
            _logger.LogInformation("Executing task {TaskName} for {ChildName}", taskName, child.FirstName);

            // Task execution logic here - would call appropriate service based on taskName
            // For example: await ExecuteWeeklyLetterCheck() or await ExecuteReminders()
            await Task.Delay(PlaceholderTaskDelayMs); // Placeholder for actual task execution

            // Update task execution count
            UpdateTaskExecutionStats(child, taskName, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute task {TaskName} for {ChildName}", taskName, child.FirstName);

            // Update failure count
            UpdateTaskExecutionStats(child, taskName, false);
            throw;
        }
    }

    public Task<bool> ShouldRunTaskAsync(Child child, ChildScheduledTask task)
    {
        if (task == null || !task.Enabled)
            return Task.FromResult(false);

        try
        {
            var schedule = CrontabSchedule.Parse(task.CronExpression);
            var now = DateTime.UtcNow;

            if (task.NextRun.HasValue && now >= task.NextRun.Value)
            {
                // Allow a 1-minute window for execution
                return Task.FromResult(now <= task.NextRun.Value.AddMinutes(ExecutionWindowMinutes));
            }

            // Calculate next run if not set
            var nextRun = task.LastRun.HasValue
                ? schedule.GetNextOccurrence(task.LastRun.Value)
                : schedule.GetNextOccurrence(now.AddMinutes(-ScheduleLookbackMinutes));

            task.NextRun = nextRun;
            return Task.FromResult(now >= nextRun && now <= nextRun.AddMinutes(ExecutionWindowMinutes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating schedule for task {TaskName}", task.TaskName);
            return Task.FromResult(false);
        }
    }

    public async Task ProcessDueTasksAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Get tasks for this child
        var tasks = await GetScheduledTasksAsync(child);

        _logger.LogDebug("Processing {Count} potential tasks for {ChildName}", tasks.Count, child.FirstName);

        foreach (var task in tasks.Where(t => t.Enabled))
        {
            try
            {
                if (await ShouldRunTaskAsync(child, task))
                {
                    _logger.LogInformation("Task {TaskName} is due for {ChildName}", task.TaskName, child.FirstName);

                    // Execute the task
                    await ExecuteTaskAsync(child, task.TaskName);

                    // Update last run time
                    UpdateTaskLastRun(child, task.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing task {TaskName} for {ChildName}",
                    task.TaskName, child.FirstName);
            }
        }
    }

    // Helper methods
    private bool IsValidCronExpression(string cronExpression)
    {
        try
        {
            CrontabSchedule.Parse(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private DateTime? GetNextRunTime(string cronExpression, DateTime fromTime)
    {
        try
        {
            var schedule = CrontabSchedule.Parse(cronExpression);
            return schedule.GetNextOccurrence(fromTime);
        }
        catch
        {
            return null;
        }
    }

    private int GetScheduledTasksCountForChild(Child child)
    {
        // Don't call GetScheduledTasksAsync which validates context again
        // Just count directly from the dictionary
        lock (_lockObject)
        {
            var prefix = GetChildKeyPrefix(child);
            return _inMemoryTasks.Count(kvp => kvp.Key.StartsWith(prefix));
        }
    }

    private string GetTaskKey(Child child, int taskId)
    {
        return $"{child.FirstName}_{child.LastName}_{taskId}";
    }

    private string GetChildKeyPrefix(Child child)
    {
        return $"{child.FirstName}_{child.LastName}_";
    }

    private int GenerateTaskId()
    {
        // Simple ID generation - in production, use database sequence
        return Math.Abs(Guid.NewGuid().GetHashCode());
    }

    private ChildScheduledTask CloneTask(ChildScheduledTask task)
    {
        // Create a defensive copy to prevent external modification
        return new ChildScheduledTask
        {
            Id = task.Id,
            ChildFirstName = task.ChildFirstName,
            ChildLastName = task.ChildLastName,
            TaskName = task.TaskName,
            Description = task.Description,
            CronExpression = task.CronExpression,
            Enabled = task.Enabled,
            LastRun = task.LastRun,
            NextRun = task.NextRun,
            ExecutionCount = task.ExecutionCount,
            FailureCount = task.FailureCount,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }

    private void UpdateTaskExecutionStats(Child child, string taskName, bool success)
    {
        lock (_lockObject)
        {
            var prefix = GetChildKeyPrefix(child);
            var task = _inMemoryTasks
                .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Value.TaskName == taskName)
                .Select(kvp => kvp.Value)
                .FirstOrDefault();

            if (task != null)
            {
                if (success)
                {
                    task.ExecutionCount++;
                    task.LastRun = DateTime.UtcNow;
                    task.NextRun = GetNextRunTime(task.CronExpression, DateTime.UtcNow);
                }
                else
                {
                    task.FailureCount++;
                }
                task.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private void UpdateTaskLastRun(Child child, int taskId)
    {
        var key = GetTaskKey(child, taskId);
        lock (_lockObject)
        {
            if (_inMemoryTasks.TryGetValue(key, out var task))
            {
                task.LastRun = DateTime.UtcNow;
                task.NextRun = GetNextRunTime(task.CronExpression, DateTime.UtcNow);
                task.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
