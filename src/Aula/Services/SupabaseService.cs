using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Aula.Configuration;
using Aula.Repositories;

namespace Aula.Services;

public class SupabaseService : ISupabaseService
{
    private readonly ILogger _logger;
    private readonly Config _config;
    private readonly ILoggerFactory _loggerFactory;
    private Client? _supabase;
    private IReminderRepository? _reminderRepository;
    private IWeekLetterRepository? _weekLetterRepository;
    private IAppStateRepository? _appStateRepository;
    private IRetryTrackingRepository? _retryTrackingRepository;
    private IScheduledTaskRepository? _scheduledTaskRepository;

    public SupabaseService(Config config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
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

            // Initialize repositories
            _reminderRepository = new ReminderRepository(_supabase, _loggerFactory);
            _weekLetterRepository = new WeekLetterRepository(_supabase, _loggerFactory);
            _appStateRepository = new AppStateRepository(_supabase, _loggerFactory);
            _retryTrackingRepository = new RetryTrackingRepository(_supabase, _loggerFactory, _config);
            _scheduledTaskRepository = new ScheduledTaskRepository(_supabase, _loggerFactory);

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
        if (_reminderRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _reminderRepository.AddReminderAsync(text, date, time, childName);
    }

    public async Task<List<Reminder>> GetPendingRemindersAsync()
    {
        if (_reminderRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _reminderRepository.GetPendingRemindersAsync();
    }

    public async Task MarkReminderAsSentAsync(int reminderId)
    {
        if (_reminderRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _reminderRepository.MarkReminderAsSentAsync(reminderId);
    }

    public async Task<List<Reminder>> GetAllRemindersAsync()
    {
        if (_reminderRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _reminderRepository.GetAllRemindersAsync();
    }

    public async Task DeleteReminderAsync(int reminderId)
    {
        if (_reminderRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _reminderRepository.DeleteReminderAsync(reminderId);
    }

    public async Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _weekLetterRepository.HasWeekLetterBeenPostedAsync(childName, weekNumber, year);
    }

    public async Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _weekLetterRepository.MarkWeekLetterAsPostedAsync(childName, weekNumber, year, contentHash, postedToSlack, postedToTelegram);
    }

    public async Task StoreWeekLetterAsync(string childName, int weekNumber, int year, string contentHash, string rawContent, bool postedToSlack = false, bool postedToTelegram = false)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _weekLetterRepository.StoreWeekLetterAsync(childName, weekNumber, year, contentHash, rawContent, postedToSlack, postedToTelegram);
    }

    public async Task<string?> GetStoredWeekLetterAsync(string childName, int weekNumber, int year)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _weekLetterRepository.GetStoredWeekLetterAsync(childName, weekNumber, year);
    }

    public async Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _weekLetterRepository.GetStoredWeekLettersAsync(childName, year);
    }

    public async Task<StoredWeekLetter?> GetLatestStoredWeekLetterAsync(string childName)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _weekLetterRepository.GetLatestStoredWeekLetterAsync(childName);
    }

    public async Task DeleteWeekLetterAsync(string childName, int weekNumber, int year)
    {
        if (_weekLetterRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _weekLetterRepository.DeleteWeekLetterAsync(childName, weekNumber, year);
    }

    public async Task<string?> GetAppStateAsync(string key)
    {
        if (_appStateRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _appStateRepository.GetAppStateAsync(key);
    }

    public async Task SetAppStateAsync(string key, string value)
    {
        if (_appStateRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _appStateRepository.SetAppStateAsync(key, value);
    }

    public async Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year)
    {
        if (_retryTrackingRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _retryTrackingRepository.GetRetryAttemptsAsync(childName, weekNumber, year);
    }

    public async Task IncrementRetryAttemptAsync(string childName, int weekNumber, int year)
    {
        if (_retryTrackingRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _retryTrackingRepository.IncrementRetryAttemptAsync(childName, weekNumber, year);
    }

    public async Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year)
    {
        if (_retryTrackingRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _retryTrackingRepository.MarkRetryAsSuccessfulAsync(childName, weekNumber, year);
    }

    public async Task<List<ScheduledTask>> GetScheduledTasksAsync()
    {
        if (_scheduledTaskRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _scheduledTaskRepository.GetScheduledTasksAsync();
    }

    public async Task<ScheduledTask?> GetScheduledTaskAsync(string name)
    {
        if (_scheduledTaskRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        return await _scheduledTaskRepository.GetScheduledTaskAsync(name);
    }

    public async Task UpdateScheduledTaskAsync(ScheduledTask task)
    {
        if (_scheduledTaskRepository == null) throw new InvalidOperationException("Supabase client not initialized");
        await _scheduledTaskRepository.UpdateScheduledTaskAsync(task);
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