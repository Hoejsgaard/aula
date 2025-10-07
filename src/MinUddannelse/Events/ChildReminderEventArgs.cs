using MinUddannelse.Repositories.DTOs;

namespace MinUddannelse.Events;

/// <summary>
/// Event arguments for child reminder events.
/// </summary>
public class ChildReminderEventArgs : ChildEventArgs
{
    public string ReminderText { get; init; }
    public DateOnly RemindDate { get; init; }
    public TimeOnly RemindTime { get; init; }
    public int ReminderId { get; init; }

    public ChildReminderEventArgs(string childId, string childFirstName, Reminder reminder)
        : base(childId, childFirstName, "reminder", null)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        ReminderText = reminder.Text;
        RemindDate = reminder.RemindDate;
        RemindTime = reminder.RemindTime;
        ReminderId = reminder.Id;
    }
}
