using MinUddannelse.Events;

namespace MinUddannelse.Scheduling;

public interface ISchedulingService
{
    event EventHandler<ChildWeekLetterEventArgs>? ChildWeekLetterReady;
    event EventHandler<ChildReminderEventArgs>? ReminderReady;
    event EventHandler<ChildMessageEventArgs>? MessageReady;

    Task StartAsync();
    Task StopAsync();
    void TriggerChildWeekLetterReady(ChildWeekLetterEventArgs args);
}
