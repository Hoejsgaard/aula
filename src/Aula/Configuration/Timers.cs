namespace Aula.Configuration;

public class Timers
{
    // Scheduling
    public int SchedulingIntervalSeconds { get; set; } = 10;

    // Retention
    public int ProcessedMessageRetentionHours { get; set; } = 24;

    // Task execution
    public int TaskExecutionWindowMinutes { get; set; } = 1;
    public int InitialOccurrenceOffsetMinutes { get; set; } = 1;

    // Timeouts
    public int HttpTimeoutSeconds { get; set; } = 30;

    // Retry intervals
    public int WeekLetterRetryIntervalHours { get; set; } = 2;
}
