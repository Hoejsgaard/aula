namespace MinUddannelse.Configuration;

public class WeekLetter
{
    public int RetryIntervalHours { get; set; } = 2;
    public int MaxRetryDurationHours { get; set; } = 48;
    public bool PostOnStartup { get; set; }
}
