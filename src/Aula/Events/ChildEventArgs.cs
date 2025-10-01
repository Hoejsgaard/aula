using Aula.Configuration;
using Newtonsoft.Json.Linq;

namespace Aula.Events;

/// <summary>
/// Event arguments for child-specific events.
/// Ensures events are properly scoped to individual children.
/// </summary>
public class ChildEventArgs : EventArgs
{
    public string ChildId { get; init; } = string.Empty;
    public string ChildFirstName { get; init; } = string.Empty;
    public DateTimeOffset EventTime { get; init; }
    public string EventType { get; init; } = string.Empty;
    public JObject? Data { get; init; }

    public ChildEventArgs(string childId, string childFirstName, string eventType, JObject? data = null)
    {
        ChildId = childId ?? throw new ArgumentNullException(nameof(childId));
        ChildFirstName = childFirstName ?? throw new ArgumentNullException(nameof(childFirstName));
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        EventTime = DateTimeOffset.UtcNow;
        Data = data;
    }
}

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

/// <summary>
/// Event arguments for child week letter events.
/// </summary>
public class ChildWeekLetterEventArgs : ChildEventArgs
{
    public int WeekNumber { get; init; }
    public int Year { get; init; }
    public JObject WeekLetter { get; init; }

    public ChildWeekLetterEventArgs(string childId, string childFirstName, int weekNumber, int year, JObject weekLetter)
        : base(childId, childFirstName, "week_letter", weekLetter)
    {
        WeekNumber = weekNumber;
        Year = year;
        WeekLetter = weekLetter ?? throw new ArgumentNullException(nameof(weekLetter));
    }
}
