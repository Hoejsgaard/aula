namespace MinUddannelse.Configuration;

public class Scheduling
{
    public int IntervalSeconds { get; set; } = 10;
    public int TaskExecutionWindowMinutes { get; set; } = 1;
    public int InitialOccurrenceOffsetMinutes { get; set; } = 1;
    public string DefaultOnDateReminderTime { get; set; } = "06:45";
}
