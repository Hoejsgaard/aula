namespace Aula.Tools;

public class StubAiToolsManager : IAiToolsManager
{
    public Task<string> CreateReminderAsync(string description, string dateTime, string? childName = null)
    {
        return Task.FromResult("Reminder tools disabled in current build");
    }

    public Task<string> ListRemindersAsync(string? childName = null)
    {
        return Task.FromResult("Reminder tools disabled in current build");
    }

    public Task<string> DeleteReminderAsync(int reminderNumber)
    {
        return Task.FromResult("Reminder tools disabled in current build");
    }

    public string GetWeekLetters(string? childName = null)
    {
        return "Week letter tools disabled in current build";
    }

    public string GetChildActivities(string childName, string? date = null)
    {
        return "Activity tools disabled in current build";
    }

    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public string GetHelp()
    {
        return "AI tools temporarily disabled in current build";
    }
}