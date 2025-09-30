using Aula.Configuration;

namespace Aula.Scheduling;

/// <summary>
/// Rate limiter specifically for scheduling operations to prevent resource exhaustion.
/// </summary>
public interface IChildSchedulingRateLimiter
{
	/// <summary>
	/// Check if child can schedule a new task.
	/// </summary>
	Task<bool> CanScheduleTaskAsync(Child child);

	/// <summary>
	/// Check if child can execute a task now.
	/// </summary>
	Task<bool> CanExecuteTaskAsync(Child child, string taskName);

	/// <summary>
	/// Record that a task was scheduled.
	/// </summary>
	Task RecordTaskScheduledAsync(Child child);

	/// <summary>
	/// Record that a task was executed.
	/// </summary>
	Task RecordTaskExecutedAsync(Child child, string taskName);

	/// <summary>
	/// Get current task count for a child.
	/// </summary>
	Task<int> GetScheduledTaskCountAsync(Child child);

	/// <summary>
	/// Get execution count in the current time window.
	/// </summary>
	Task<int> GetExecutionCountAsync(Child child, TimeSpan window);
}
