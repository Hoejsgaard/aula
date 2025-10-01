using Microsoft.Extensions.Logging;
using NCrontab;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
using Aula.Channels;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Aula.Utilities;
using Aula.Events;

namespace Aula.Scheduling;

public class SchedulingService : ISchedulingService
{
    private readonly ILogger _logger;
    private readonly ISupabaseService _supabaseService;
    private readonly IWeekLetterService _weekLetterService;
    private readonly IChannelManager _channelManager;
    private readonly Config _config;
    private Timer? _schedulingTimer;
    private readonly object _lockObject = new object();
    private bool _isRunning;

    // Child-specific events
    // ChildScheduleReady event removed - not currently used
    public event EventHandler<ChildWeekLetterEventArgs>? ChildWeekLetterReady;

    // Public method to trigger week letter event (used for startup posting)
    public void TriggerChildWeekLetterReady(ChildWeekLetterEventArgs args)
    {
        ChildWeekLetterReady?.Invoke(this, args);
    }

    public SchedulingService(
        ILoggerFactory loggerFactory,
        ISupabaseService supabaseService,
        IWeekLetterService weekLetterService,
        IChannelManager channelManager,
        Config config)
    {
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<SchedulingService>();
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _weekLetterService = weekLetterService ?? throw new ArgumentNullException(nameof(weekLetterService));
        _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public Task StartAsync()
    {
        _logger.LogInformation("Starting scheduling service");

        lock (_lockObject)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Scheduling service is already running");
                return Task.CompletedTask;
            }

            _isRunning = true;
        }

        // Start the timer first, then check for missed reminders in background
        var timerInterval = TimeSpan.FromSeconds(_config.Scheduling.IntervalSeconds);
        _schedulingTimer = new Timer(CheckScheduledTasksWrapper, null, TimeSpan.Zero, timerInterval);
        _logger.LogInformation("Scheduling service timer started - checking every {IntervalSeconds} seconds", _config.Scheduling.IntervalSeconds);

        // Check for missed reminders in background to avoid blocking startup
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckForMissedReminders();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for missed reminders on startup");
            }
        });

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _logger.LogInformation("Stopping scheduling service");

        lock (_lockObject)
        {
            _isRunning = false;
        }

        try
        {
            _schedulingTimer?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Timer was already disposed
        }

        // Wait for running tasks to complete (with timeout)
        // await Task.WhenAny(Task.WhenAll(_runningTasks), Task.Delay(TimeSpan.FromSeconds(_config.Timers.ShutdownTimeoutSeconds)));

        _logger.LogInformation("Scheduling service stopped");

        return Task.CompletedTask;
    }

    private void CheckScheduledTasksWrapper(object? state)
    {
        _logger.LogInformation("TIMER FIRED - CheckScheduledTasksWrapper called at {Time}", DateTime.Now);

        // Don't use async void - use Fire and Forget pattern instead
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckScheduledTasks();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in scheduled task check");
            }
        });
    }

    private async Task CheckScheduledTasks()
    {
        if (!_isRunning) return;

        try
        {
            _logger.LogInformation("Timer fired: Checking scheduled tasks and reminders at {LocalTime} (UTC: {UtcTime})", DateTime.Now, DateTime.UtcNow);

            // ALWAYS check for pending reminders every 10 seconds
            await ExecutePendingReminders();

            // Only check database-driven scheduled tasks every minute (every 6th call)
            var currentSecond = DateTime.Now.Second;

            // Run scheduled tasks only at the top of each minute (when seconds are 0-9)
            if (currentSecond < 10)
            {
                _logger.LogInformation("Running scheduled tasks check at {Time}", DateTime.Now);

                var tasks = await _supabaseService.GetScheduledTasksAsync();
                var now = DateTime.UtcNow;

                foreach (var task in tasks)
                {
                    try
                    {
                        if (ShouldRunTask(task, now))
                        {
                            _logger.LogInformation("Executing scheduled task: {TaskName}", task.Name);

                            // Update last run time
                            task.LastRun = now;
                            task.NextRun = GetNextRunTime(task.CronExpression, now);
                            await _supabaseService.UpdateScheduledTaskAsync(task);

                            // Execute the task
                            await ExecuteTask(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing scheduled task: {TaskName}", task.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRITICAL: Error in CheckScheduledTasks - this could cause app crashes");
            // Don't rethrow - let the timer continue
        }
    }

    private bool ShouldRunTask(ScheduledTask task, DateTime now)
    {
        if (!task.Enabled)
        {
            return false;
        }

        try
        {
            var schedule = CrontabSchedule.Parse(task.CronExpression);
            var nextRun = task.LastRun != null
                ? schedule.GetNextOccurrence(task.LastRun.Value)
                : schedule.GetNextOccurrence(now.AddMinutes(-_config.Scheduling.InitialOccurrenceOffsetMinutes));

            // Allow for a window to account for timing variations
            return now >= nextRun && now <= nextRun.AddMinutes(_config.Scheduling.TaskExecutionWindowMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression for task {TaskName}: {CronExpression}",
                task.Name, task.CronExpression);
            return false;
        }
    }

    private DateTime? GetNextRunTime(string cronExpression, DateTime fromTime)
    {
        try
        {
            var schedule = CrontabSchedule.Parse(cronExpression);
            return schedule.GetNextOccurrence(fromTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating next run time for cron: {CronExpression}", cronExpression);
            return null;
        }
    }

    private async Task ExecuteTask(ScheduledTask task)
    {
        switch (task.Name)
        {
            case "MorningReminders": // Keep backwards compatibility
            case "ReminderCheck": // Better name
                await ExecutePendingReminders();
                break;

            case "WeeklyLetterCheck":
                await ExecuteWeeklyLetterCheck(task);
                break;

            default:
                _logger.LogWarning("Unknown scheduled task: {TaskName}", task.Name);
                break;
        }
    }

    private async Task ExecutePendingReminders()
    {
        try
        {
            _logger.LogInformation("ExecutePendingReminders called at {LocalTime} (UTC: {UtcTime})", DateTime.Now, DateTime.UtcNow);

            var pendingReminders = await _supabaseService.GetPendingRemindersAsync();

            if (pendingReminders.Count == 0)
            {
                _logger.LogInformation("No pending reminders found");
                return;
            }

            _logger.LogInformation("Found {Count} pending reminders to send", pendingReminders.Count);

            foreach (var reminder in pendingReminders)
            {
                try
                {
                    await SendReminderNotification(reminder);
                    await _supabaseService.DeleteReminderAsync(reminder.Id); // DELETE instead of mark as sent

                    _logger.LogInformation("Sent and deleted reminder {ReminderId}: {Text}", reminder.Id, reminder.Text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending reminder {ReminderId}", reminder.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing pending reminders");
        }
    }

    private Task SendReminderNotification(Reminder reminder)
    {
        string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";
        string message = $"*Reminder*{childInfo}: {reminder.Text}";

        // Send to all enabled channels
        try
        {
            _logger.LogInformation("Reminder {ReminderId} sending disabled in current build", reminder.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder {ReminderId} to channels", reminder.Id);
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteWeeklyLetterCheck(ScheduledTask task)
    {
        try
        {
            _logger.LogInformation("Executing weekly letter check");

            // Get all children from config
            var children = _config.MinUddannelse?.Children ?? new List<Child>();
            if (!children.Any())
            {
                _logger.LogWarning("No children configured for week letter check");
                return;
            }

            foreach (var child in children)
            {
                try
                {
                    await CheckAndPostWeekLetter(child, task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking week letter for child: {ChildName}", child.FirstName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing weekly letter check");
        }
    }

    private async Task CheckAndPostWeekLetter(Child child, ScheduledTask task)
    {
        var (weekNumber, year) = GetCurrentWeekAndYear();

        try
        {
            _logger.LogInformation("Checking week letter for {ChildName}", child.FirstName);

            if (await IsWeekLetterAlreadyPosted(child.FirstName, weekNumber, year))
                return;

            var weekLetter = await TryGetWeekLetter(child, weekNumber, year);
            if (weekLetter == null)
                return;

            var result = await ValidateAndProcessWeekLetterContent(weekLetter, child.FirstName, weekNumber, year);
            if (result.content == null)
                return;

            if (await IsContentAlreadyPosted(child, result.contentHash!, weekNumber, year))
                return;

            // Emit child-specific event instead of directly posting
            var childId = child.FirstName.ToLowerInvariant().Replace(" ", "_");
            var eventArgs = new ChildWeekLetterEventArgs(
                childId,
                child.FirstName,
                weekNumber,
                year,
                weekLetter);

            ChildWeekLetterReady?.Invoke(this, eventArgs);

            // Mark as posted after emitting event (subscribers will handle the actual posting)
            await _supabaseService.MarkWeekLetterAsPostedAsync(child.FirstName, weekNumber, year, result.contentHash!);

            _logger.LogInformation("Emitted week letter event for {ChildName}", child.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing week letter for {ChildName}", child.FirstName);
            await _supabaseService.IncrementRetryAttemptAsync(child.FirstName, weekNumber, year);
        }
    }

    private static (int weekNumber, int year) GetCurrentWeekAndYear()
    {
        var now = DateTime.Now;
        return (System.Globalization.ISOWeek.GetWeekOfYear(now), now.Year);
    }

    private async Task<bool> IsWeekLetterAlreadyPosted(string childName, int weekNumber, int year)
    {
        var alreadyPosted = await _supabaseService.HasWeekLetterBeenPostedAsync(childName, weekNumber, year);
        if (alreadyPosted)
        {
            _logger.LogInformation("Week letter for {ChildName} week {WeekNumber}/{Year} already posted",
                childName, weekNumber, year);
        }
        return alreadyPosted;
    }

    private async Task<dynamic?> TryGetWeekLetter(Child child, int weekNumber, int year)
    {
        var date = DateOnly.FromDateTime(DateTime.Now);
        var weekLetter = await _weekLetterService.GetOrFetchWeekLetterAsync(child, date, true);
        if (weekLetter == null)
        {
            _logger.LogWarning("No week letter available for {ChildName}, will retry later", child.FirstName);
            await _supabaseService.IncrementRetryAttemptAsync(child.FirstName, weekNumber, year);
        }
        return weekLetter;
    }

    private async Task<(string? content, string? contentHash)> ValidateAndProcessWeekLetterContent(dynamic weekLetter, string childName, int weekNumber, int year)
    {
        var content = ExtractWeekLetterContent(weekLetter);
        if (string.IsNullOrEmpty(content))
        {
            _logger.LogWarning("Week letter content is empty for {ChildName}", childName);
            await _supabaseService.IncrementRetryAttemptAsync(childName, weekNumber, year);
            return (null, null);
        }

        var contentHash = ComputeContentHash(content);
        return (content, contentHash);
    }

    private async Task<bool> IsContentAlreadyPosted(Child child, string contentHash, int weekNumber, int year)
    {
        var existingPosts = await _supabaseService.GetAppStateAsync($"last_posted_hash_{child.FirstName}");
        if (existingPosts == contentHash)
        {
            _logger.LogInformation("Week letter content unchanged for {ChildName}, marking as posted", child.FirstName);
            await _supabaseService.MarkWeekLetterAsPostedAsync(child.FirstName, weekNumber, year, contentHash, true, child.Channels?.Telegram?.Enabled == true);
            return true;
        }
        return false;
    }

    private async Task PostAndMarkWeekLetter(Child child, dynamic weekLetter, string content, string contentHash, int weekNumber, int year)
    {
        await PostWeekLetter(child, weekLetter, content);

        // Store the complete week letter with raw content for future retrieval
        await _supabaseService.StoreWeekLetterAsync(child.FirstName, weekNumber, year, contentHash, weekLetter.ToString(), true, child.Channels?.Telegram?.Enabled == true);
        await _supabaseService.SetAppStateAsync($"last_posted_hash_{child.FirstName}", contentHash);
        await _supabaseService.MarkRetryAsSuccessfulAsync(child.FirstName, weekNumber, year);
    }

    private string ExtractWeekLetterContent(dynamic weekLetter)
    {
        return WeekLetterContentExtractor.ExtractContent(weekLetter, _logger);
    }

    private string ComputeContentHash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private Task PostWeekLetter(Child child, dynamic weekLetter, string content)
    {
        try
        {
            // Extract title
            var ugebreve = weekLetter?["ugebreve"];
            var weekLetterTitle = "";
            if (ugebreve is JArray ugebreveArray && ugebreveArray.Count > 0)
            {
                var uge = ugebreveArray[0]?["uge"]?.ToString() ?? "";
                var klasseNavn = ugebreveArray[0]?["klasseNavn"]?.ToString() ?? "";
                weekLetterTitle = $"Uge {uge} - {klasseNavn}";
            }

            // Convert HTML to markdown for Slack
            var html2MarkdownConverter = new Html2SlackMarkdownConverter();
            var markdownContent = html2MarkdownConverter.Convert(content).Replace("**", "*");

            // Week letter posting is complex and platform-specific (Slack uses markdown, Telegram uses JSON)
            // For now, use channels directly until IChannel interface is enhanced with week letter capabilities

            _logger.LogInformation("Week letter posting disabled in current build for {ChildName}", child.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting week letter for {ChildName}", child.FirstName);
            throw;
        }

        return Task.CompletedTask;
    }

    private async Task CheckForMissedReminders()
    {
        try
        {
            _logger.LogInformation("Checking for missed reminders on startup");

            var pendingReminders = await _supabaseService.GetPendingRemindersAsync();

            if (pendingReminders.Count > 0)
            {
                _logger.LogWarning("Found {Count} missed reminders on startup", pendingReminders.Count);

                foreach (var reminder in pendingReminders)
                {
                    var reminderLocalDateTime = reminder.RemindDate.ToDateTime(reminder.RemindTime);
                    var missedBy = DateTime.Now - reminderLocalDateTime;

                    string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";
                    string message = $"⚠️ *Missed Reminder*{childInfo}: {reminder.Text}\n" +
                                   $"_Was scheduled for {reminderLocalDateTime:HH:mm} ({missedBy.TotalMinutes:F0} minutes ago)_";

                    // Send notification about missed reminder - disabled in current build
                    _logger.LogInformation("Missed reminder notification disabled in current build: {Message}", message);

                    // Delete the missed reminder so it doesn't keep showing up
                    await _supabaseService.DeleteReminderAsync(reminder.Id);

                    _logger.LogInformation("Notified about missed reminder: {Text}", reminder.Text);
                }
            }
            else
            {
                _logger.LogInformation("No missed reminders found on startup");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for missed reminders");
        }
    }
}
