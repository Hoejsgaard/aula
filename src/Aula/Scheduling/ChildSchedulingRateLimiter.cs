using Aula.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aula.Scheduling;

public class ChildSchedulingRateLimiter : IChildSchedulingRateLimiter
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, SchedulingRateLimitState> _rateLimitStates = new();

    // Configuration limits
    private const int MaxScheduledTasksPerChild = 10;
    private const int MaxExecutionsPerHour = 60;
    private const int MaxScheduleOperationsPerDay = 20;

    public ChildSchedulingRateLimiter(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<ChildSchedulingRateLimiter>();
    }

    public Task<bool> CanScheduleTaskAsync(Child child)
    {
        var key = GetChildKey(child);
        var state = _rateLimitStates.GetOrAdd(key, _ => new SchedulingRateLimitState());

        // Check daily schedule operations limit
        var dailyOps = GetOperationsInWindow(state.ScheduleOperations, TimeSpan.FromDays(1));
        if (dailyOps >= MaxScheduleOperationsPerDay)
        {
            _logger.LogWarning("Child {ChildName} exceeded daily schedule operations limit: {Count}/{Max}",
                child.FirstName, dailyOps, MaxScheduleOperationsPerDay);
            return Task.FromResult(false);
        }

        // Check total task count
        if (state.TotalScheduledTasks >= MaxScheduledTasksPerChild)
        {
            _logger.LogWarning("Child {ChildName} reached maximum scheduled tasks: {Count}/{Max}",
                child.FirstName, state.TotalScheduledTasks, MaxScheduledTasksPerChild);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> CanExecuteTaskAsync(Child child, string taskName)
    {
        var key = GetChildKey(child);
        var state = _rateLimitStates.GetOrAdd(key, _ => new SchedulingRateLimitState());

        // Check hourly execution limit
        var hourlyExecutions = GetOperationsInWindow(state.ExecutionTimestamps, TimeSpan.FromHours(1));
        if (hourlyExecutions >= MaxExecutionsPerHour)
        {
            _logger.LogWarning("Child {ChildName} exceeded hourly execution limit for {TaskName}: {Count}/{Max}",
                child.FirstName, taskName, hourlyExecutions, MaxExecutionsPerHour);
            return Task.FromResult(false);
        }

        // Check per-task rate limiting (prevent rapid repeated execution)
        var taskKey = $"{key}:{taskName}";
        if (state.LastTaskExecution.TryGetValue(taskKey, out var lastExecution))
        {
            var timeSinceLastExecution = DateTime.UtcNow - lastExecution;
            if (timeSinceLastExecution < TimeSpan.FromMinutes(1))
            {
                _logger.LogWarning("Task {TaskName} for {ChildName} executed too recently: {Seconds}s ago",
                    taskName, child.FirstName, timeSinceLastExecution.TotalSeconds);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task RecordTaskScheduledAsync(Child child)
    {
        var key = GetChildKey(child);
        var state = _rateLimitStates.GetOrAdd(key, _ => new SchedulingRateLimitState());

        state.ScheduleOperations.Enqueue(DateTime.UtcNow);
        state.TotalScheduledTasks++;

        // Clean old entries
        CleanOldOperations(state.ScheduleOperations, TimeSpan.FromDays(2));

        _logger.LogDebug("Recorded task scheduled for {ChildName}. Total tasks: {Count}",
            child.FirstName, state.TotalScheduledTasks);

        return Task.CompletedTask;
    }

    public Task RecordTaskExecutedAsync(Child child, string taskName)
    {
        var key = GetChildKey(child);
        var state = _rateLimitStates.GetOrAdd(key, _ => new SchedulingRateLimitState());

        var now = DateTime.UtcNow;
        state.ExecutionTimestamps.Enqueue(now);

        var taskKey = $"{key}:{taskName}";
        state.LastTaskExecution[taskKey] = now;

        // Clean old entries
        CleanOldOperations(state.ExecutionTimestamps, TimeSpan.FromHours(2));

        _logger.LogDebug("Recorded task {TaskName} executed for {ChildName}",
            taskName, child.FirstName);

        return Task.CompletedTask;
    }

    public Task<int> GetScheduledTaskCountAsync(Child child)
    {
        var key = GetChildKey(child);
        if (_rateLimitStates.TryGetValue(key, out var state))
        {
            return Task.FromResult(state.TotalScheduledTasks);
        }
        return Task.FromResult(0);
    }

    public Task<int> GetExecutionCountAsync(Child child, TimeSpan window)
    {
        var key = GetChildKey(child);
        if (_rateLimitStates.TryGetValue(key, out var state))
        {
            var count = GetOperationsInWindow(state.ExecutionTimestamps, window);
            return Task.FromResult(count);
        }
        return Task.FromResult(0);
    }

    // Helper methods
    private string GetChildKey(Child child)
    {
        return $"{child.FirstName}_{child.LastName}".ToLowerInvariant();
    }

    private int GetOperationsInWindow(ConcurrentQueue<DateTime> timestamps, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return timestamps.Count(t => t > cutoff);
    }

    private void CleanOldOperations(ConcurrentQueue<DateTime> timestamps, TimeSpan retention)
    {
        var cutoff = DateTime.UtcNow - retention;
        while (timestamps.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            timestamps.TryDequeue(out _);
        }
    }

    // Internal state class
    private sealed class SchedulingRateLimitState
    {
        public int TotalScheduledTasks { get; set; }
        public ConcurrentQueue<DateTime> ScheduleOperations { get; } = new();
        public ConcurrentQueue<DateTime> ExecutionTimestamps { get; } = new();
        public ConcurrentDictionary<string, DateTime> LastTaskExecution { get; } = new();
    }
}
