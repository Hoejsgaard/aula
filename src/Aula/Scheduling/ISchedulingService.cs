using Aula.Events;

namespace Aula.Scheduling;

public interface ISchedulingService
{
    event EventHandler<ChildWeekLetterEventArgs>? ChildWeekLetterReady;

    Task StartAsync();
    Task StopAsync();
    void TriggerChildWeekLetterReady(ChildWeekLetterEventArgs args);
}
