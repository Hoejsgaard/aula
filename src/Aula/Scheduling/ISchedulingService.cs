namespace Aula.Scheduling;

public interface ISchedulingService
{
    Task StartAsync();
    Task StopAsync();
}
