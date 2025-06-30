namespace Aula.Configuration;

public class Timers
{
    public int SchedulingIntervalSeconds { get; set; } = 10;
    public int SlackPollingIntervalSeconds { get; set; } = 5;
    public int CleanupIntervalHours { get; set; } = 1;
    public bool AdaptivePolling { get; set; } = true;
    public int MaxPollingIntervalSeconds { get; set; } = 30;
    public int MinPollingIntervalSeconds { get; set; } = 5;
}