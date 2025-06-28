using Microsoft.Extensions.Logging;
using NCrontab;
using System.Security.Cryptography;
using System.Text;

namespace Aula;

public interface ISchedulingService
{
    Task StartAsync();
    Task StopAsync();
}

public class SchedulingService : ISchedulingService
{
    private readonly ILogger _logger;
    private readonly ISupabaseService _supabaseService;
    private readonly IAgentService _agentService;
    private readonly SlackInteractiveBot _slackBot;
    private readonly TelegramInteractiveBot? _telegramBot;
    private readonly Config _config;
    private Timer? _schedulingTimer;
    private readonly object _lockObject = new object();
    private bool _isRunning;

    public SchedulingService(
        ILoggerFactory loggerFactory,
        ISupabaseService supabaseService,
        IAgentService agentService,
        SlackInteractiveBot slackBot,
        TelegramInteractiveBot? telegramBot,
        Config config)
    {
        _logger = loggerFactory.CreateLogger<SchedulingService>();
        _supabaseService = supabaseService;
        _agentService = agentService;
        _slackBot = slackBot;
        _telegramBot = telegramBot;
        _config = config;
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
        _schedulingTimer = new Timer(CheckScheduledTasksWrapper, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        _logger.LogInformation("Scheduling service timer started - checking every 10 seconds");

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

        _schedulingTimer?.Dispose();
        _logger.LogInformation("Scheduling service stopped");

        return Task.CompletedTask;
    }

    private void CheckScheduledTasksWrapper(object? state)
    {
        _logger.LogInformation("🔥 TIMER FIRED - CheckScheduledTasksWrapper called at {Time}", DateTime.Now);
        
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
            _logger.LogInformation("🔔 Timer fired: Checking scheduled tasks and reminders at {LocalTime} (UTC: {UtcTime})", DateTime.Now, DateTime.UtcNow);

            // ALWAYS check for pending reminders every 10 seconds
            await ExecutePendingReminders();

            // Only check database-driven scheduled tasks every minute (every 6th call)
            var currentSecond = DateTime.Now.Second;
            
            // Run scheduled tasks only at the top of each minute (when seconds are 0-9)
            if (currentSecond < 10)
            {
                _logger.LogInformation("⏰ Running scheduled tasks check at {Time}", DateTime.Now);
                
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
                : schedule.GetNextOccurrence(now.AddMinutes(-1));

            // Allow for a 1-minute window to account for timing variations
            return now >= nextRun && now <= nextRun.AddMinutes(1);
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
            _logger.LogInformation("📋 ExecutePendingReminders called at {LocalTime} (UTC: {UtcTime})", DateTime.Now, DateTime.UtcNow);

            var pendingReminders = await _supabaseService.GetPendingRemindersAsync();

            if (!pendingReminders.Any())
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

    private async Task SendReminderNotification(Reminder reminder)
    {
        string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";
        string message = $"🔔 *Reminder*{childInfo}: {reminder.Text}";

        // Send to Slack if enabled
        if (_config.Slack.EnableInteractiveBot)
        {
            try
            {
                await _slackBot.SendMessage(message);
                _logger.LogInformation("Sent reminder {ReminderId} to Slack", reminder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder {ReminderId} to Slack", reminder.Id);
            }
        }

        // Send to Telegram if enabled
        if (_config.Telegram.Enabled && _telegramBot != null)
        {
            try
            {
                // ChatId constructor handles both string usernames (@channel) and numeric IDs
                await _telegramBot.SendMessage(_config.Telegram.ChannelId, message);
                _logger.LogInformation("Sent reminder {ReminderId} to Telegram", reminder.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder {ReminderId} to Telegram", reminder.Id);
            }
        }
    }

    private async Task ExecuteWeeklyLetterCheck(ScheduledTask task)
    {
        try
        {
            _logger.LogInformation("Executing weekly letter check");

            // Get all children
            var children = await _agentService.GetAllChildrenAsync();
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
        try
        {
            _logger.LogInformation("Checking week letter for {ChildName}", child.FirstName);

            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);
            var year = DateTime.Now.Year;

            // Check if we've already posted this week letter
            var alreadyPosted = await _supabaseService.HasWeekLetterBeenPostedAsync(child.FirstName, weekNumber, year);
            if (alreadyPosted)
            {
                _logger.LogInformation("Week letter for {ChildName} week {WeekNumber}/{Year} already posted",
                    child.FirstName, weekNumber, year);
                return;
            }

            // Try to get the week letter
            var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
            if (weekLetter == null)
            {
                _logger.LogWarning("No week letter available for {ChildName}, will retry later", child.FirstName);
                await _supabaseService.IncrementRetryAttemptAsync(child.FirstName, weekNumber, year);
                return;
            }

            // Extract content and compute hash
            var content = ExtractWeekLetterContent(weekLetter);
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Week letter content is empty for {ChildName}", child.FirstName);
                await _supabaseService.IncrementRetryAttemptAsync(child.FirstName, weekNumber, year);
                return;
            }

            var contentHash = ComputeContentHash(content);

            // Check if this exact content was already posted (in case of manual posting)
            var existingPosts = await _supabaseService.GetAppStateAsync($"last_posted_hash_{child.FirstName}");
            if (existingPosts == contentHash)
            {
                _logger.LogInformation("Week letter content unchanged for {ChildName}, marking as posted", child.FirstName);
                await _supabaseService.MarkWeekLetterAsPostedAsync(child.FirstName, weekNumber, year, contentHash, true, _config.Telegram.Enabled);
                return;
            }

            // Post the week letter
            await PostWeekLetter(child, weekLetter, content);

            // Mark as posted and store hash
            await _supabaseService.MarkWeekLetterAsPostedAsync(child.FirstName, weekNumber, year, contentHash, true, _config.Telegram.Enabled);
            await _supabaseService.SetAppStateAsync($"last_posted_hash_{child.FirstName}", contentHash);
            await _supabaseService.MarkRetryAsSuccessfulAsync(child.FirstName, weekNumber, year);

            _logger.LogInformation("Successfully posted week letter for {ChildName}", child.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing week letter for {ChildName}", child.FirstName);

            // Increment retry count
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);
            var year = DateTime.Now.Year;
            await _supabaseService.IncrementRetryAttemptAsync(child.FirstName, weekNumber, year);
        }
    }

    private string ExtractWeekLetterContent(dynamic weekLetter)
    {
        try
        {
            var ugebreve = weekLetter?["ugebreve"];
            if (ugebreve != null)
            {
                var count = ((dynamic)ugebreve).Count;
                if (count > 0)
                {
                    return ugebreve[0]?["indhold"]?.ToString() ?? "";
                }
            }
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting week letter content");
            return "";
        }
    }

    private string ComputeContentHash(string content)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private async Task PostWeekLetter(Child child, dynamic weekLetter, string content)
    {
        try
        {
            // Extract title
            var ugebreve = weekLetter?["ugebreve"];
            var weekLetterTitle = "";
            if (ugebreve != null)
            {
                var count = ((dynamic)ugebreve).Count;
                if (count > 0)
                {
                    var uge = ugebreve[0]?["uge"]?.ToString() ?? "";
                    var klasseNavn = ugebreve[0]?["klasseNavn"]?.ToString() ?? "";
                    weekLetterTitle = $"Uge {uge} - {klasseNavn}";
                }
            }

            // Convert HTML to markdown for Slack
            var html2MarkdownConverter = new Html2SlackMarkdownConverter();
            var markdownContent = html2MarkdownConverter.Convert(content).Replace("**", "*");

            // Post to Slack if enabled
            if (_config.Slack.EnableInteractiveBot)
            {
                await _slackBot.PostWeekLetter(child.FirstName, markdownContent, weekLetterTitle);
            }

            // Post to Telegram if enabled
            if (_config.Telegram.Enabled && _telegramBot != null)
            {
                await _telegramBot.PostWeekLetter(child.FirstName, weekLetter);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting week letter for {ChildName}", child.FirstName);
            throw;
        }
    }

    private async Task CheckForMissedReminders()
    {
        try
        {
            _logger.LogInformation("Checking for missed reminders on startup");

            var pendingReminders = await _supabaseService.GetPendingRemindersAsync();

            if (pendingReminders.Any())
            {
                _logger.LogWarning("Found {Count} missed reminders on startup", pendingReminders.Count);

                foreach (var reminder in pendingReminders)
                {
                    var reminderLocalDateTime = reminder.RemindDate.ToDateTime(reminder.RemindTime);
                    var missedBy = DateTime.Now - reminderLocalDateTime;

                    string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";
                    string message = $"⚠️ *Missed Reminder*{childInfo}: {reminder.Text}\n" +
                                   $"_Was scheduled for {reminderLocalDateTime:HH:mm} ({missedBy.TotalMinutes:F0} minutes ago)_";

                    // Send notification about missed reminder
                    if (_config.Slack.EnableInteractiveBot)
                    {
                        await _slackBot.SendMessage(message);
                    }

                    if (_config.Telegram.Enabled && _telegramBot != null)
                    {
                        await _telegramBot.SendMessage(_config.Telegram.ChannelId, message);
                    }

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