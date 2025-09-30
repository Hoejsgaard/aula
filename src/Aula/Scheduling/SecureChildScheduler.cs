using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Aula.Scheduling;

/// <summary>
/// Secure child-aware scheduler with comprehensive security layers.
/// Ensures complete isolation of scheduling operations per child.
/// </summary>
public class SecureChildScheduler : IChildScheduler
{
	private readonly IChildContext _context;
	private readonly IChildContextValidator _contextValidator;
	private readonly IChildAuditService _auditService;
	private readonly IChildSchedulingRateLimiter _rateLimiter;
	private readonly ILogger<SecureChildScheduler> _logger;
	private readonly Dictionary<string, ChildScheduledTask> _inMemoryTasks = new();
	private readonly object _lockObject = new();

	// Configuration limits
	private const int MaxTasksPerChild = 10;
	private const int MaxExecutionsPerHour = 60;

	public SecureChildScheduler(
		IChildContext context,
		IChildContextValidator contextValidator,
		IChildAuditService auditService,
		IChildSchedulingRateLimiter rateLimiter,
		ILogger<SecureChildScheduler> logger)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
		_auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
		_rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<int> ScheduleTaskAsync(string taskName, string cronExpression, string? description = null)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "schedule:create"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to schedule tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "schedule:create", SecuritySeverity.Warning);
			throw new UnauthorizedAccessException($"Child {child.FirstName} does not have permission to schedule tasks");
		}

		// Layer 3: Validate cron expression
		if (!IsValidCronExpression(cronExpression))
		{
			_logger.LogWarning("Invalid cron expression provided by {ChildName}: {Cron}", child.FirstName, cronExpression);
			throw new ArgumentException($"Invalid cron expression: {cronExpression}");
		}

		// Layer 4: Rate limiting - check task count
		var currentCount = GetScheduledTasksCountForChild(child);
		if (currentCount >= MaxTasksPerChild)
		{
			_logger.LogWarning("Task limit exceeded for {ChildName}. Current: {Count}, Max: {Max}",
				child.FirstName, currentCount, MaxTasksPerChild);
			await _auditService.LogSecurityEventAsync(child, "TaskLimitExceeded", taskName, SecuritySeverity.Warning);
			throw new InvalidOperationException($"Maximum number of scheduled tasks ({MaxTasksPerChild}) exceeded for {child.FirstName}");
		}

		// Layer 5: Check if child can schedule (rate limiting)
		if (!await _rateLimiter.CanScheduleTaskAsync(child))
		{
			_logger.LogWarning("Rate limit exceeded for {ChildName} scheduling tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "schedule:create", SecuritySeverity.Warning);
			throw new InvalidOperationException("Rate limit exceeded for scheduling tasks");
		}

		// Layer 6: Create and store the task
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

		// Layer 7: Audit logging
		await _rateLimiter.RecordTaskScheduledAsync(child);
		await _auditService.LogDataAccessAsync(child, "ScheduleTask", taskName, true);

		_logger.LogInformation("Task {TaskName} scheduled successfully for {ChildName} with ID {TaskId}",
			taskName, child.FirstName, task.Id);

		return task.Id;
	}

	public async Task<bool> CancelTaskAsync(int taskId)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "schedule:delete"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to cancel tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "schedule:delete", SecuritySeverity.Warning);
			return false;
		}

		// Layer 3: Find and validate task ownership
		var key = GetTaskKey(child, taskId);
		bool taskExists;
		lock (_lockObject)
		{
			taskExists = _inMemoryTasks.ContainsKey(key);
			if (taskExists)
			{
				// Layer 4: Remove the task
				_inMemoryTasks.Remove(key);
			}
		}

		if (!taskExists)
		{
			_logger.LogWarning("Task {TaskId} not found for {ChildName}", taskId, child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "TaskNotFound", $"TaskId:{taskId}", SecuritySeverity.Information);
			return false;
		}

		// Layer 5: Audit logging
		await _auditService.LogDataAccessAsync(child, "CancelTask", $"TaskId:{taskId}", true);

		_logger.LogInformation("Task {TaskId} cancelled successfully for {ChildName}", taskId, child.FirstName);
		return true;
	}

	public async Task<List<ChildScheduledTask>> GetScheduledTasksAsync()
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "schedule:read"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to read scheduled tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "schedule:read", SecuritySeverity.Warning);
			return new List<ChildScheduledTask>();
		}

		// Layer 3: Get tasks for this child only
		var tasks = new List<ChildScheduledTask>();
		lock (_lockObject)
		{
			var prefix = GetChildKeyPrefix(child);
			tasks = _inMemoryTasks
				.Where(kvp => kvp.Key.StartsWith(prefix))
				.Select(kvp => CloneTask(kvp.Value))
				.ToList();
		}

		// Layer 4: Audit logging
		await _auditService.LogDataAccessAsync(child, "GetScheduledTasks", $"Count:{tasks.Count}", true);

		_logger.LogDebug("Retrieved {Count} scheduled tasks for {ChildName}", tasks.Count, child.FirstName);
		return tasks;
	}

	public async Task<bool> SetTaskEnabledAsync(int taskId, bool enabled)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "schedule:update"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to update tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "schedule:update", SecuritySeverity.Warning);
			return false;
		}

		// Layer 3: Find and update task
		var key = GetTaskKey(child, taskId);
		lock (_lockObject)
		{
			if (!_inMemoryTasks.TryGetValue(key, out var task))
			{
				_logger.LogWarning("Task {TaskId} not found for {ChildName}", taskId, child.FirstName);
				return false;
			}

			task.Enabled = enabled;
			task.UpdatedAt = DateTime.UtcNow;
		}

		// Layer 4: Audit logging
		await _auditService.LogDataAccessAsync(child, "SetTaskEnabled", $"TaskId:{taskId},Enabled:{enabled}", true);

		_logger.LogInformation("Task {TaskId} enabled status set to {Enabled} for {ChildName}",
			taskId, enabled, child.FirstName);
		return true;
	}

	public async Task ExecuteTaskAsync(string taskName)
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Permission validation
		if (!await _contextValidator.ValidateChildPermissionsAsync(child, "schedule:execute"))
		{
			_logger.LogWarning("Permission denied for {ChildName} to execute tasks", child.FirstName);
			await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "schedule:execute", SecuritySeverity.Warning);
			throw new UnauthorizedAccessException($"Child {child.FirstName} does not have permission to execute tasks");
		}

		// Layer 3: Rate limiting for execution
		if (!await _rateLimiter.CanExecuteTaskAsync(child, taskName))
		{
			_logger.LogWarning("Execution rate limit exceeded for {ChildName} task {TaskName}",
				child.FirstName, taskName);
			await _auditService.LogSecurityEventAsync(child, "ExecutionRateLimitExceeded", taskName, SecuritySeverity.Warning);
			throw new InvalidOperationException("Execution rate limit exceeded");
		}

		// Layer 4: Execute task with context preservation
		try
		{
			_logger.LogInformation("Executing task {TaskName} for {ChildName}", taskName, child.FirstName);

			// Context is already preserved through DI scope
			// Task execution logic here - would call appropriate service based on taskName
			// For example: await ExecuteWeeklyLetterCheck() or await ExecuteReminders()
			await Task.Delay(100); // Placeholder for actual task execution

			// Layer 5: Record execution
			await _rateLimiter.RecordTaskExecutedAsync(child, taskName);
			await _auditService.LogDataAccessAsync(child, "ExecuteTask", taskName, true);

			// Update task execution count
			UpdateTaskExecutionStats(child, taskName, true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to execute task {TaskName} for {ChildName}", taskName, child.FirstName);

			// Update failure count
			UpdateTaskExecutionStats(child, taskName, false);

			await _auditService.LogDataAccessAsync(child, "ExecuteTask", taskName, false);
			throw;
		}
	}

	public Task<bool> ShouldRunTaskAsync(ChildScheduledTask task)
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
				return Task.FromResult(now <= task.NextRun.Value.AddMinutes(1));
			}

			// Calculate next run if not set
			var nextRun = task.LastRun.HasValue
				? schedule.GetNextOccurrence(task.LastRun.Value)
				: schedule.GetNextOccurrence(now.AddMinutes(-1));

			task.NextRun = nextRun;
			return Task.FromResult(now >= nextRun && now <= nextRun.AddMinutes(1));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error evaluating schedule for task {TaskName}", task.TaskName);
			return Task.FromResult(false);
		}
	}

	public async Task ProcessDueTasksAsync()
	{
		// Layer 1: Context validation
		_context.ValidateContext();
		var child = _context.CurrentChild!;

		// Layer 2: Get tasks for this child
		var tasks = await GetScheduledTasksAsync();

		_logger.LogDebug("Processing {Count} potential tasks for {ChildName}", tasks.Count, child.FirstName);

		foreach (var task in tasks.Where(t => t.Enabled))
		{
			try
			{
				if (await ShouldRunTaskAsync(task))
				{
					_logger.LogInformation("Task {TaskName} is due for {ChildName}", task.TaskName, child.FirstName);

					// Execute the task
					await ExecuteTaskAsync(task.TaskName);

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
