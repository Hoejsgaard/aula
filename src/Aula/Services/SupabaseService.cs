using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
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

public class SupabaseService : ISupabaseService
{
    private readonly ILogger _logger;
    private readonly Config _config;
    private Client? _supabase;

    public SupabaseService(Config config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _logger = loggerFactory.CreateLogger<SupabaseService>();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Supabase connection");

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false, // We don't need realtime for this use case
                AutoRefreshToken = false     // We're using service role key
            };

            _supabase = new Client(_config.Supabase.Url, _config.Supabase.ServiceRoleKey, options);
            await _supabase.InitializeAsync();

            _logger.LogInformation("Supabase client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Supabase client");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            if (_supabase == null)
            {
                _logger.LogWarning("Supabase client not initialized");
                return false;
            }

            // Try a simple query to test the connection
            var result = await _supabase
                .From<AppState>()
                .Select("key")
                .Limit(1)
                .Get();

            _logger.LogInformation("Supabase connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase connection test failed");
            return false;
        }
    }

    public async Task<int> AddReminderAsync(string text, DateOnly date, TimeOnly time, string? childName = null)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var reminder = new Reminder
        {
            Text = text,
            RemindDate = date,
            RemindTime = time,
            ChildName = childName,
            CreatedBy = "bot"
        };

        var insertResponse = await _supabase
            .From<Reminder>()
            .Insert(reminder);

        var insertedReminder = insertResponse.Models.FirstOrDefault();
        if (insertedReminder == null)
        {
            throw new InvalidOperationException("Failed to insert reminder");
        }

        _logger.LogInformation("Added reminder with ID {ReminderId}: {Text}", insertedReminder.Id, text);
        return insertedReminder.Id;
    }

    public async Task<List<Reminder>> GetPendingRemindersAsync()
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        // Use UTC for all internal calculations
        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now; // For display only

        _logger.LogInformation("Checking for pending reminders. Current UTC: {UtcNow}, Local: {LocalNow}",
            nowUtc, nowLocal);

        // Get all reminders and filter in memory (since we delete fired reminders, all existing ones are pending)
        var allReminders = await _supabase
            .From<Reminder>()
            .Get();

        var pendingReminders = allReminders.Models.Where(r =>
        {
            // Convert reminder date/time to UTC for comparison
            var reminderLocalDateTime = r.RemindDate.ToDateTime(r.RemindTime);
            var reminderUtcDateTime = TimeZoneInfo.ConvertTimeToUtc(reminderLocalDateTime, TimeZoneInfo.Local);

            bool isPending = reminderUtcDateTime <= nowUtc;

            _logger.LogInformation("Reminder '{Text}': Local={LocalTime}, UTC={UtcTime}, Due={IsDue}",
                r.Text, reminderLocalDateTime, reminderUtcDateTime, isPending);

            return isPending;
        }).ToList();

        _logger.LogInformation("Found {Count} pending reminders", pendingReminders.Count);

        foreach (var reminder in pendingReminders)
        {
            var reminderLocalDateTime = reminder.RemindDate.ToDateTime(reminder.RemindTime);
            _logger.LogInformation("Pending reminder: '{Text}' scheduled for {DateTime} (local time)",
                reminder.Text, reminderLocalDateTime);
        }

        return pendingReminders;
    }

    public async Task MarkReminderAsSentAsync(int reminderId)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        await _supabase
            .From<Reminder>()
            .Where(r => r.Id == reminderId)
            .Set(r => r.IsSent, true)
            .Update();

        _logger.LogInformation("Marked reminder {ReminderId} as sent", reminderId);
    }

    public async Task<List<Reminder>> GetAllRemindersAsync()
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var reminderResponse = await _supabase
            .From<Reminder>()
            .Get();

        return reminderResponse.Models;
    }

    public async Task DeleteReminderAsync(int reminderId)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        await _supabase
            .From<Reminder>()
            .Where(r => r.Id == reminderId)
            .Delete();

        _logger.LogInformation("Deleted reminder {ReminderId}", reminderId);
    }

    public async Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName && p.WeekNumber == weekNumber && p.Year == year)
            .Get();

        return result.Models.Any();
    }

    public async Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var postedLetter = new PostedLetter
        {
            ChildName = childName,
            WeekNumber = weekNumber,
            Year = year,
            ContentHash = contentHash,
            PostedToSlack = postedToSlack,
            PostedToTelegram = postedToTelegram
        };

        await _supabase
            .From<PostedLetter>()
            .Upsert(postedLetter);

        _logger.LogInformation("Marked week letter as posted for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task StoreWeekLetterAsync(string childName, int weekNumber, int year, string contentHash, string rawContent, bool postedToSlack = false, bool postedToTelegram = false)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var postedLetter = new PostedLetter
        {
            ChildName = childName,
            WeekNumber = weekNumber,
            Year = year,
            ContentHash = contentHash,
            RawContent = rawContent,
            PostedToSlack = postedToSlack,
            PostedToTelegram = postedToTelegram
        };

        await _supabase
            .From<PostedLetter>()
            .Upsert(postedLetter);

        _logger.LogInformation("Stored week letter for {ChildName}, week {WeekNumber}/{Year} with content",
            childName, weekNumber, year);
    }

    public async Task<string?> GetStoredWeekLetterAsync(string childName, int weekNumber, int year)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName && p.WeekNumber == weekNumber && p.Year == year)
            .Single();

        return result?.RawContent;
    }

    public async Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var query = _supabase.From<PostedLetter>().Select("*");

        if (!string.IsNullOrEmpty(childName))
        {
            query = query.Where(p => p.ChildName == childName);
        }

        if (year.HasValue)
        {
            query = query.Where(p => p.Year == year.Value);
        }

        var result = await query.Get();

        return result.Models.Select(p => new StoredWeekLetter
        {
            ChildName = p.ChildName,
            WeekNumber = p.WeekNumber,
            Year = p.Year,
            RawContent = p.RawContent,
            PostedAt = p.PostedAt
        }).ToList();
    }

    public async Task<StoredWeekLetter?> GetLatestStoredWeekLetterAsync(string childName)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName && p.RawContent != null)
            .Order("year", Supabase.Postgrest.Constants.Ordering.Descending)
            .Order("week_number", Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Single();

        if (result == null) return null;

        return new StoredWeekLetter
        {
            ChildName = result.ChildName,
            WeekNumber = result.WeekNumber,
            Year = result.Year,
            RawContent = result.RawContent,
            PostedAt = result.PostedAt
        };
    }

    public async Task<string?> GetAppStateAsync(string key)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<AppState>()
            .Where(a => a.Key == key)
            .Single();

        return result?.Value ?? null;
    }

    public async Task SetAppStateAsync(string key, string value)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var appState = new AppState
        {
            Key = key,
            Value = value
        };

        await _supabase
            .From<AppState>()
            .Upsert(appState);

        _logger.LogInformation("Set app state: {Key} = {Value}", key, value);
    }

    public async Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<RetryAttempt>()
            .Where(r => r.ChildName == childName && r.WeekNumber == weekNumber && r.Year == year)
            .Single();

        return result?.AttemptCount ?? 0;
    }

    public async Task IncrementRetryAttemptAsync(string childName, int weekNumber, int year)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        // Try to get existing retry attempt
        var existing = await _supabase
            .From<RetryAttempt>()
            .Where(r => r.ChildName == childName && r.WeekNumber == weekNumber && r.Year == year)
            .Single();

        if (existing != null)
        {
            // Update existing
            existing.AttemptCount += 1;
            existing.LastAttempt = DateTime.UtcNow;
            // Get retry settings from database
            var task = await GetScheduledTaskAsync("WeeklyLetterCheck");
            var retryHours = task?.RetryIntervalHours ?? 1;
            existing.NextAttempt = DateTime.UtcNow.AddHours(retryHours);

            await _supabase
                .From<RetryAttempt>()
                .Update(existing);
        }
        else
        {
            // Get retry settings from database
            var task = await GetScheduledTaskAsync("WeeklyLetterCheck");
            var retryHours = task?.RetryIntervalHours ?? 1;
            var maxRetryHours = task?.MaxRetryHours ?? 48;
            var maxAttempts = maxRetryHours / retryHours; // Calculate max attempts based on retry duration

            // Create new
            var retryAttempt = new RetryAttempt
            {
                ChildName = childName,
                WeekNumber = weekNumber,
                Year = year,
                AttemptCount = 1,
                LastAttempt = DateTime.UtcNow,
                NextAttempt = DateTime.UtcNow.AddHours(retryHours),
                MaxAttempts = maxAttempts
            };

            await _supabase
                .From<RetryAttempt>()
                .Insert(retryAttempt);
        }

        _logger.LogInformation("Incremented retry attempt for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        await _supabase
            .From<RetryAttempt>()
            .Where(r => r.ChildName == childName && r.WeekNumber == weekNumber && r.Year == year)
            .Set(r => r.IsSuccessful, true)
            .Update();

        _logger.LogInformation("Marked retry as successful for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task<List<ScheduledTask>> GetScheduledTasksAsync()
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var tasksResponse = await _supabase
            .From<ScheduledTask>()
            .Where(t => t.Enabled == true)
            .Get();

        return tasksResponse.Models;
    }

    public async Task<ScheduledTask?> GetScheduledTaskAsync(string name)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        var result = await _supabase
            .From<ScheduledTask>()
            .Where(t => t.Name == name)
            .Single();

        return result;
    }

    public async Task UpdateScheduledTaskAsync(ScheduledTask task)
    {
        if (_supabase == null) throw new InvalidOperationException("Supabase client not initialized");

        task.UpdatedAt = DateTime.UtcNow;

        await _supabase
            .From<ScheduledTask>()
            .Update(task);

        _logger.LogInformation("Updated scheduled task: {TaskName}", task.Name);
    }
}

// Supabase model classes
[Table("reminders")]
public class Reminder : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("text")]
    public string Text { get; set; } = string.Empty;

    [Column("remind_date")]
    public DateOnly RemindDate { get; set; }

    [Column("remind_time")]
    public TimeOnly RemindTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("is_sent")]
    public bool IsSent { get; set; }

    [Column("child_name")]
    public string? ChildName { get; set; }

    [Column("created_by")]
    public string CreatedBy { get; set; } = "bot";
}

[Table("posted_letters")]
public class PostedLetter : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("child_name")]
    public string ChildName { get; set; } = string.Empty;

    [Column("week_number")]
    public int WeekNumber { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [Column("posted_at")]
    public DateTime PostedAt { get; set; }

    [Column("posted_to_slack")]
    public bool PostedToSlack { get; set; }

    [Column("posted_to_telegram")]
    public bool PostedToTelegram { get; set; }

    [Column("raw_content")]
    public string? RawContent { get; set; }
}

[Table("app_state")]
public class AppState : BaseModel
{
    [PrimaryKey("key")]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

[Table("retry_attempts")]
public class RetryAttempt : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("child_name")]
    public string ChildName { get; set; } = string.Empty;

    [Column("week_number")]
    public int WeekNumber { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("last_attempt")]
    public DateTime LastAttempt { get; set; }

    [Column("next_attempt")]
    public DateTime? NextAttempt { get; set; }

    [Column("max_attempts")]
    public int MaxAttempts { get; set; }

    [Column("is_successful")]
    public bool IsSuccessful { get; set; }
}

[Table("scheduled_tasks")]
public class ScheduledTask : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("cron_expression")]
    public string CronExpression { get; set; } = string.Empty;

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("retry_interval_hours")]
    public int? RetryIntervalHours { get; set; }

    [Column("max_retry_hours")]
    public int? MaxRetryHours { get; set; }

    [Column("last_run")]
    public DateTime? LastRun { get; set; }

    [Column("next_run")]
    public DateTime? NextRun { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

// DTO for week letter retrieval
public class StoredWeekLetter
{
    public string ChildName { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public int Year { get; set; }
    public string? RawContent { get; set; }
    public DateTime PostedAt { get; set; }
}