namespace Aula.Events;

/// <summary>
/// Event arguments for child week letter events.
/// </summary>
public class ChildWeekLetterEventArgs : ChildEventArgs
{
    public int WeekNumber { get; init; }
    public int Year { get; init; }
    public JObject WeekLetter { get; init; }

    public ChildWeekLetterEventArgs(string childId, string childFirstName, int weekNumber, int year, JObject weekLetter)
        : base(childId, childFirstName, "week_letter", weekLetter)
    {
        WeekNumber = weekNumber;
        Year = year;
        WeekLetter = weekLetter ?? throw new ArgumentNullException(nameof(weekLetter));
    }
}
