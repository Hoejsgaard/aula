namespace Aula.Tools;

public interface IAiToolsManager
{
	Task<string> CreateReminderAsync(string description, string dateTime, string? childName = null);
	Task<string> ListRemindersAsync(string? childName = null);
	Task<string> DeleteReminderAsync(int reminderNumber);
	string GetWeekLetters(string? childName = null);
	string GetChildActivities(string childName, string? date = null);
	string GetCurrentDateTime();
	string GetHelp();
}
