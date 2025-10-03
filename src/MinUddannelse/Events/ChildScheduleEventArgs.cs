using Newtonsoft.Json.Linq;

namespace MinUddannelse.Events;

/// <summary>
/// Event arguments for child schedule events.
/// </summary>
public class ChildScheduleEventArgs : ChildEventArgs
{
    public DateOnly ScheduledDate { get; init; }
    public string TaskType { get; init; } = string.Empty;

    public ChildScheduleEventArgs(string childId, string childFirstName, DateOnly scheduledDate, string taskType, JObject? data = null)
        : base(childId, childFirstName, "schedule", data)
    {
        ScheduledDate = scheduledDate;
        TaskType = taskType ?? throw new ArgumentNullException(nameof(taskType));
    }
}
