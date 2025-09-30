using Aula.Configuration;

namespace Aula.Scheduling;

/// <summary>
/// Child-aware scheduling service that operates within a child context.
/// All operations are isolated per child with no cross-child interference.
/// </summary>
public interface IChildScheduler
{
	/// <summary>
	/// Schedule a new task for the current child context.
	/// </summary>
	Task<int> ScheduleTaskAsync(string taskName, string cronExpression, string? description = null);

	/// <summary>
	/// Cancel a scheduled task for the current child.
	/// </summary>
	Task<bool> CancelTaskAsync(int taskId);

	/// <summary>
	/// Get all scheduled tasks for the current child.
	/// </summary>
	Task<List<ChildScheduledTask>> GetScheduledTasksAsync();

	/// <summary>
	/// Enable or disable a scheduled task for the current child.
	/// </summary>
	Task<bool> SetTaskEnabledAsync(int taskId, bool enabled);

	/// <summary>
	/// Execute a specific task immediately for the current child.
	/// </summary>
	Task ExecuteTaskAsync(string taskName);

	/// <summary>
	/// Check if a task should run based on its schedule.
	/// </summary>
	Task<bool> ShouldRunTaskAsync(ChildScheduledTask task);

	/// <summary>
	/// Process all due tasks for the current child.
	/// </summary>
	Task ProcessDueTasksAsync();
}

/// <summary>
/// Represents a scheduled task for a specific child.
/// </summary>
public class ChildScheduledTask
{
	public int Id { get; set; }
	public string ChildFirstName { get; set; } = string.Empty;
	public string ChildLastName { get; set; } = string.Empty;
	public string TaskName { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string CronExpression { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
	public DateTime? LastRun { get; set; }
	public DateTime? NextRun { get; set; }
	public int ExecutionCount { get; set; }
	public int FailureCount { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
}
