namespace MinUddannelse.Models;

public class StoredWeekLetter
{
    public string ChildName { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public int Year { get; set; }
    public string? RawContent { get; set; }
    public DateTime PostedAt { get; set; }
}
