using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Tools;

public class AiToolsManager : IAiToolsManager
{
    private readonly ISupabaseService _supabaseService;
    private readonly IDataService _dataService;
    private readonly ILogger _logger;

    public AiToolsManager(ISupabaseService supabaseService, IDataService dataService, ILoggerFactory loggerFactory)
    {
        _supabaseService = supabaseService;
        _dataService = dataService;
        _logger = loggerFactory.CreateLogger<AiToolsManager>();
    }

    public async Task<string> CreateReminderAsync(string description, string dateTime, string? childName = null)
    {
        try
        {
            if (!DateTime.TryParse(dateTime, out var parsedDateTime))
            {
                return "Error: Invalid date format. Please use 'yyyy-MM-dd HH:mm' format.";
            }

            var date = DateOnly.FromDateTime(parsedDateTime);
            var time = TimeOnly.FromDateTime(parsedDateTime);

            var reminderId = await _supabaseService.AddReminderAsync(description, date, time, childName);

            var childInfo = string.IsNullOrEmpty(childName) ? "" : $" for {childName}";
            _logger.LogInformation("Created reminder: {Description} at {DateTime}{ChildInfo}", description, parsedDateTime, childInfo);

            return $"✅ Reminder created: '{description}' for {parsedDateTime:yyyy-MM-dd HH:mm}{childInfo}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create reminder");
            return "❌ Failed to create reminder. Please try again.";
        }
    }

    public async Task<string> ListRemindersAsync(string? childName = null)
    {
        try
        {
            var reminders = await _supabaseService.GetAllRemindersAsync();

            if (!string.IsNullOrEmpty(childName))
            {
                reminders = reminders.Where(r => string.Equals(r.ChildName, childName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (reminders.Count == 0)
            {
                var filterInfo = string.IsNullOrEmpty(childName) ? "" : $" for {childName}";
                return $"No active reminders found{filterInfo}.";
            }

            var reminderList = reminders
                .OrderBy(r => r.RemindDate).ThenBy(r => r.RemindTime)
                .Select((r, index) =>
                {
                    var dateTime = r.RemindDate.ToDateTime(r.RemindTime);
                    return $"{index + 1}. {r.Text} - {dateTime:yyyy-MM-dd HH:mm}" +
                           (string.IsNullOrEmpty(r.ChildName) ? "" : $" ({r.ChildName})");
                })
                .ToList();

            return "📋 Active reminders:\n" + string.Join("\n", reminderList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list reminders");
            return "❌ Failed to retrieve reminders. Please try again.";
        }
    }

    public async Task<string> DeleteReminderAsync(int reminderNumber)
    {
        try
        {
            var reminders = await _supabaseService.GetAllRemindersAsync();

            if (reminderNumber < 1 || reminderNumber > reminders.Count)
            {
                return $"❌ Invalid reminder number. Please use a number between 1 and {reminders.Count}.";
            }

            var reminderToDelete = reminders.OrderBy(r => r.RemindDate).ThenBy(r => r.RemindTime).ElementAt(reminderNumber - 1);
            await _supabaseService.DeleteReminderAsync(reminderToDelete.Id);

            _logger.LogInformation("Deleted reminder: {Text}", reminderToDelete.Text);
            return $"✅ Deleted reminder: '{reminderToDelete.Text}'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete reminder");
            return "❌ Failed to delete reminder. Please try again.";
        }
    }

    public string GetWeekLetters(string? childName = null)
    {
        try
        {
            var allChildren = _dataService.GetChildren();
            var children = string.IsNullOrEmpty(childName)
                ? allChildren.ToList()
                : allChildren.Where(c => $"{c.FirstName} {c.LastName}".Contains(childName, StringComparison.OrdinalIgnoreCase)).ToList();

            if (children.Count == 0)
            {
                return $"❌ No children found matching '{childName}'.";
            }

            // Get current week number and year
            var now = DateTime.Now;
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(now);
            var year = now.Year;

            var weekLetterSummaries = new List<string>();
            foreach (var child in children)
            {
                var weekLetter = _dataService.GetWeekLetter(child, weekNumber, year);
                if (weekLetter != null)
                {
                    var summary = ExtractSummaryFromWeekLetter(weekLetter);
                    weekLetterSummaries.Add($"📝 **{child.FirstName} {child.LastName}** - Week Letter:\n{summary}");
                }
                else
                {
                    weekLetterSummaries.Add($"📝 **{child.FirstName} {child.LastName}** - No week letter available");
                }
            }

            return string.Join("\n\n", weekLetterSummaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get week letters");
            return "❌ Failed to retrieve week letters. Please try again.";
        }
    }

    public string GetChildActivities(string childName, string? date = null)
    {
        try
        {
            var targetDate = DateTime.Today;
            if (!string.IsNullOrEmpty(date) && !DateTime.TryParse(date, out targetDate))
            {
                return $"❌ Invalid date format: '{date}'. Please use a valid date format.";
            }
            var child = _dataService.GetChildren().FirstOrDefault(c =>
                $"{c.FirstName} {c.LastName}".Contains(childName, StringComparison.OrdinalIgnoreCase));

            if (child == null)
            {
                return $"❌ Child '{childName}' not found.";
            }

            // Get week number and year for the target date
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(targetDate);
            var year = targetDate.Year;

            var weekLetter = _dataService.GetWeekLetter(child, weekNumber, year);
            if (weekLetter == null)
            {
                return $"📝 No week letter available for {child.FirstName} {child.LastName}.";
            }

            // Extract activities for the specific date from the week letter
            var dayOfWeek = targetDate.DayOfWeek;
            var dayName = dayOfWeek.ToString();

            var content = ExtractContentFromWeekLetter(weekLetter);
            var lines = content.Split('\n')
                .Where(line => line.Contains(dayName, StringComparison.OrdinalIgnoreCase) ||
                              line.Contains(targetDate.ToString("dd/MM"), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count > 0)
            {
                return $"📅 **{child.FirstName} {child.LastName}** activities for {targetDate:yyyy-MM-dd} ({dayName}):\n" +
                       string.Join("\n", lines.Select(l => $"• {l.Trim()}"));
            }

            return $"📅 No specific activities found for {child.FirstName} {child.LastName} on {targetDate:yyyy-MM-dd} ({dayName}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get child activities");
            return "❌ Failed to retrieve activities. Please try again.";
        }
    }

    public string GetCurrentDateTime()
    {
        var now = DateTime.Now;
        return $"📅 Today is {now:dddd, yyyy-MM-dd} and the current time is {now:HH:mm}.";
    }

    public string GetHelp()
    {
        return @"🤖 **Available Commands:**

**Reminder Management:**
• Create reminders for specific dates and times
• List all your active reminders  
• Delete reminders by number

**Information Retrieval:**
• Get week letters for your children
• Get specific child activities by date
• Get current date and time

**Natural Language Examples:**
• ""Remind me tomorrow at 8 AM that Søren Johannes has soccer practice""
• ""What activities does Emma have on Friday?""
• ""Show me this week's letter for all children""
• ""List my reminders and delete the second one""

Just ask me naturally and I'll help you! 🚀";
    }

    private string ExtractSummaryFromWeekLetter(Newtonsoft.Json.Linq.JObject weekLetter)
    {
        // Try to extract a summary or use the first part of content
        if (weekLetter["summary"] != null)
        {
            return weekLetter["summary"]?.ToString() ?? "";
        }

        var content = ExtractContentFromWeekLetter(weekLetter);
        return content.Length > 300 ? content.Substring(0, 300) + "..." : content;
    }

    private string ExtractContentFromWeekLetter(Newtonsoft.Json.Linq.JObject weekLetter)
    {
        // Extract content from the week letter structure
        if (weekLetter["content"] != null)
        {
            return weekLetter["content"]?.ToString() ?? "";
        }

        // Fallback to other possible content fields
        if (weekLetter["text"] != null)
        {
            return weekLetter["text"]?.ToString() ?? "";
        }

        return weekLetter.ToString();
    }
}