using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using Aula.Configuration;

namespace Aula.Tools;

public class ReminderCommandHandler
{
    private readonly ILogger _logger;
    private readonly ISupabaseService _supabaseService;
    private readonly Dictionary<string, Child> _childrenByName;

    public ReminderCommandHandler(
        ILogger logger,
        ISupabaseService supabaseService,
        Dictionary<string, Child> childrenByName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _childrenByName = childrenByName ?? throw new ArgumentNullException(nameof(childrenByName));
    }

    public async Task<(bool handled, string? response)> TryHandleReminderCommand(string text, bool isEnglish)
    {
        text = text.Trim();

        // Check for various reminder command patterns
        var addResult = await TryHandleAddReminder(text, isEnglish);
        if (addResult.handled) return addResult;

        var listResult = await TryHandleListReminders(text, isEnglish);
        if (listResult.handled) return listResult;

        var deleteResult = await TryHandleDeleteReminder(text, isEnglish);
        if (deleteResult.handled) return deleteResult;

        return (false, null);
    }

    private async Task<(bool handled, string? response)> TryHandleAddReminder(string text, bool isEnglish)
    {
        // Patterns: "remind me tomorrow at 8:00 that TestChild1 has Haver til maver"
        //           "husk mig i morgen kl 8:00 at TestChild1 har Haver til maver"

        var reminderPatterns = new[]
        {
            @"remind me (tomorrow|today|\d{4}-\d{2}-\d{2}|\d{1,2}\/\d{1,2}) at (\d{1,2}:\d{2}) that (.+)",
            @"husk mig (i morgen|i dag|\d{4}-\d{2}-\d{2}|\d{1,2}\/\d{1,2}) kl (\d{1,2}:\d{2}) at (.+)"
        };

        foreach (var pattern in reminderPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    var dateStr = match.Groups[1].Value.ToLowerInvariant();
                    var timeStr = match.Groups[2].Value;
                    var reminderText = match.Groups[3].Value;

                    // Parse date
                    DateOnly date;
                    if (dateStr == "tomorrow" || dateStr == "i morgen")
                    {
                        date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
                    }
                    else if (dateStr == "today" || dateStr == "i dag")
                    {
                        date = DateOnly.FromDateTime(DateTime.Today);
                    }
                    else if (DateOnly.TryParse(dateStr, out var parsedDate))
                    {
                        date = parsedDate;
                    }
                    else
                    {
                        // Try parsing DD/MM format
                        var dateParts = dateStr.Split('/');
                        if (dateParts.Length == 2 &&
                            int.TryParse(dateParts[0], out var day) &&
                            int.TryParse(dateParts[1], out var month))
                        {
                            var year = DateTime.Now.Year;
                            if (month < DateTime.Now.Month || (month == DateTime.Now.Month && day < DateTime.Now.Day))
                            {
                                year++; // Next year if date has passed
                            }
                            date = new DateOnly(year, month, day);
                        }
                        else
                        {
                            throw new FormatException("Invalid date format");
                        }
                    }

                    // Parse time
                    if (!TimeOnly.TryParse(timeStr, out var time))
                    {
                        throw new FormatException("Invalid time format");
                    }

                    // Extract child name if mentioned
                    string? childName = null;
                    foreach (var child in _childrenByName.Values)
                    {
                        string firstName = child.FirstName.Split(' ')[0];
                        if (reminderText.Contains(firstName, StringComparison.OrdinalIgnoreCase))
                        {
                            childName = child.FirstName;
                            break;
                        }
                    }

                    // Add reminder to database
                    var reminderId = await _supabaseService.AddReminderAsync(reminderText, date, time, childName);

                    string successMessage = isEnglish
                        ? $"✅ Reminder added (ID: {reminderId}) for {date:dd/MM} at {time:HH:mm}: {reminderText}"
                        : $"✅ Påmindelse tilføjet (ID: {reminderId}) for {date:dd/MM} kl {time:HH:mm}: {reminderText}";

                    return (true, successMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding reminder");

                    string errorMessage = isEnglish
                        ? "❌ Failed to add reminder. Please check the date and time format."
                        : "❌ Kunne ikke tilføje påmindelse. Tjek venligst dato- og tidsformat.";

                    return (true, errorMessage);
                }
            }
        }

        return (false, null);
    }

    private async Task<(bool handled, string? response)> TryHandleListReminders(string text, bool isEnglish)
    {
        var listPatterns = new[]
        {
            @"^(list reminders|show reminders)$",
            @"^(vis påmindelser|liste påmindelser)$"
        };

        foreach (var pattern in listPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                try
                {
                    var reminders = await _supabaseService.GetAllRemindersAsync();

                    if (!reminders.Any())
                    {
                        string noRemindersMessage = isEnglish
                            ? "📝 No reminders found."
                            : "📝 Ingen påmindelser fundet.";

                        return (true, noRemindersMessage);
                    }

                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine(isEnglish ? "📝 <b>Your Reminders:</b>" : "📝 <b>Dine Påmindelser:</b>");
                    messageBuilder.AppendLine();

                    foreach (var reminder in reminders.OrderBy(r => r.RemindDate).ThenBy(r => r.RemindTime))
                    {
                        string status = reminder.IsSent ?
                            (isEnglish ? "✅ Sent" : "✅ Sendt") :
                            (isEnglish ? "⏳ Pending" : "⏳ Afventer");

                        string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";

                        messageBuilder.AppendLine($"<b>ID {reminder.Id}:</b> {reminder.Text}{childInfo}");
                        messageBuilder.AppendLine($"📅 {reminder.RemindDate:dd/MM/yyyy} ⏰ {reminder.RemindTime:HH:mm} - {status}");
                        messageBuilder.AppendLine();
                    }

                    return (true, messageBuilder.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing reminders");

                    string errorMessage = isEnglish
                        ? "❌ Failed to retrieve reminders."
                        : "❌ Kunne ikke hente påmindelser.";

                    return (true, errorMessage);
                }
            }
        }

        return (false, null);
    }

    private async Task<(bool handled, string? response)> TryHandleDeleteReminder(string text, bool isEnglish)
    {
        var deletePatterns = new[]
        {
            @"^delete reminder (\d+)$",
            @"^slet påmindelse (\d+)$"
        };

        foreach (var pattern in deletePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    var reminderId = int.Parse(match.Groups[1].Value);

                    await _supabaseService.DeleteReminderAsync(reminderId);

                    string successMessage = isEnglish
                        ? $"✅ Reminder {reminderId} deleted."
                        : $"✅ Påmindelse {reminderId} slettet.";

                    return (true, successMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting reminder");

                    string errorMessage = isEnglish
                        ? "❌ Failed to delete reminder. Please check the ID."
                        : "❌ Kunne ikke slette påmindelse. Tjek venligst ID'et.";

                    return (true, errorMessage);
                }
            }
        }

        return (false, null);
    }
}