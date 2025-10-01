namespace Aula.Configuration;

public class Timers
{
    // Polling intervals
    public int SchedulingIntervalSeconds { get; set; } = 10;
    public int SlackPollingIntervalSeconds { get; set; } = 5;

    // Adaptive polling
    public bool AdaptivePolling { get; set; } = true;
    public int MaxPollingIntervalSeconds { get; set; } = 30;
    public int MinPollingIntervalSeconds { get; set; } = 5;

    // Cleanup and retention
    public int CleanupIntervalHours { get; set; } = 1;
    public int ProcessedMessageRetentionHours { get; set; } = 24;

    // Task execution
    public int TaskExecutionWindowMinutes { get; set; } = 1;
    public int InitialOccurrenceOffsetMinutes { get; set; } = 1;

    // Timeouts
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    // Retry intervals
    public int WeekLetterRetryIntervalHours { get; set; } = 2;
}
