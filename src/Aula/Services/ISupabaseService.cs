using Aula.Configuration;

namespace Aula.Services;

public interface ISupabaseService
{
    Task InitializeAsync();
    Task<bool> TestConnectionAsync();

    // Reminders
    Task<int> AddReminderAsync(string text, DateOnly date, TimeOnly time, string? childName = null);
    Task<List<Reminder>> GetPendingRemindersAsync();
    Task MarkReminderAsSentAsync(int reminderId);
    Task<List<Reminder>> GetAllRemindersAsync();
    Task DeleteReminderAsync(int reminderId);

    // Posted letters tracking
    Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year);
    Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false);

    // Week letter storage and retrieval
    Task StoreWeekLetterAsync(string childName, int weekNumber, int year, string contentHash, string rawContent, bool postedToSlack = false, bool postedToTelegram = false);
    Task<string?> GetStoredWeekLetterAsync(string childName, int weekNumber, int year);
    Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null);
    Task<StoredWeekLetter?> GetLatestStoredWeekLetterAsync(string childName);

    // App state
    Task<string?> GetAppStateAsync(string key);
    Task SetAppStateAsync(string key, string value);

    // Retry attempts
    Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year);
    Task IncrementRetryAttemptAsync(string childName, int weekNumber, int year);
    Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year);

    // Scheduled tasks
    Task<List<ScheduledTask>> GetScheduledTasksAsync();
    Task<ScheduledTask?> GetScheduledTaskAsync(string name);
    Task UpdateScheduledTaskAsync(ScheduledTask task);
}