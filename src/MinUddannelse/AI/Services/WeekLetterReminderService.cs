using Microsoft.Extensions.Logging;
using MinUddannelse.AI.Prompts;
using MinUddannelse.Configuration;
using MinUddannelse.Repositories;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MinUddannelse.AI.Services;

public class WeekLetterReminderService : IWeekLetterReminderService
{
    private readonly IOpenAiService _openAiClient;
    private readonly ILogger<WeekLetterReminderService> _logger;
    private readonly IWeekLetterRepository _weekLetterRepository;
    private readonly IReminderRepository _reminderRepository;
    private readonly string _aiModel;
    private readonly TimeOnly _defaultReminderTime;

    public WeekLetterReminderService(
        IOpenAiService openAiService,
        ILogger<WeekLetterReminderService> logger,
        IWeekLetterRepository weekLetterRepository,
        IReminderRepository reminderRepository,
        string aiModel,
        TimeOnly defaultReminderTime)
    {
        ArgumentNullException.ThrowIfNull(openAiService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(weekLetterRepository);
        ArgumentNullException.ThrowIfNull(reminderRepository);
        ArgumentException.ThrowIfNullOrWhiteSpace(aiModel);

        _openAiClient = openAiService;
        _logger = logger;
        _weekLetterRepository = weekLetterRepository;
        _reminderRepository = reminderRepository;
        _aiModel = aiModel;
        _defaultReminderTime = defaultReminderTime;
    }

    public async Task<ReminderExtractionResult> ExtractAndStoreRemindersAsync(
        string childName,
        int weekNumber,
        int year,
        JObject weekLetter,
        string contentHash)
    {
        try
        {
            _logger.LogInformation("Starting reminder extraction for {ChildName} week {WeekNumber}/{Year}",
                childName, weekNumber, year);

            // Check if we already processed this week letter
            var postedLetter = await _weekLetterRepository.GetPostedLetterByHashAsync(childName, weekNumber, year);
            if (postedLetter == null)
            {
                _logger.LogWarning("Cannot extract reminders - no posted letter found for {ChildName} week {WeekNumber}/{Year}",
                    childName, weekNumber, year);
                return new ReminderExtractionResult { Success = false, RemindersCreated = 0, ErrorMessage = "No posted letter found" };
            }

            if (postedLetter.AutoRemindersExtracted == true)
            {
                _logger.LogInformation("Reminders already extracted for {ChildName} week {WeekNumber}/{Year}, skipping",
                    childName, weekNumber, year);
                return new ReminderExtractionResult { Success = true, RemindersCreated = 0, AlreadyProcessed = true };
            }

            // Extract content from week letter JObject
            var weekLetterContent = ExtractContentFromWeekLetter(weekLetter);
            if (string.IsNullOrWhiteSpace(weekLetterContent))
            {
                _logger.LogWarning("No content found in week letter for {ChildName} week {WeekNumber}/{Year}",
                    childName, weekNumber, year);
                return new ReminderExtractionResult { Success = true, RemindersCreated = 0, NoRemindersFound = true };
            }

            // Extract events using AI
            var extractedEvents = await ExtractEventsFromWeekLetterAsync(weekLetterContent);

            if (!extractedEvents.Any())
            {
                _logger.LogInformation("No actionable events found in week letter for {ChildName} week {WeekNumber}/{Year}",
                    childName, weekNumber, year);

                // Mark as processed even if no reminders found
                await _weekLetterRepository.MarkAutoRemindersExtractedAsync(childName, weekNumber, year);

                return new ReminderExtractionResult { Success = true, RemindersCreated = 0, NoRemindersFound = true };
            }

            // Delete existing auto-extracted reminders for this week letter (delete/recreate pattern)
            var weekLetterId = postedLetter?.Id;
            if (weekLetterId.HasValue)
            {
                await _reminderRepository.DeleteAutoExtractedRemindersByWeekLetterIdAsync(weekLetterId.Value);
            }

            // Store reminders
            var remindersCreated = 0;
            var createdReminderInfos = new List<CreatedReminderInfo>();

            foreach (var extractedEvent in extractedEvents)
            {
                try
                {
                    var reminder = CreateReminderFromExtractedEvent(
                        extractedEvent, childName, weekLetterId);

                    await _reminderRepository.AddReminderAsync(
                        reminder.Text,
                        reminder.RemindDate,
                        reminder.RemindTime,
                        reminder.ChildName);
                    remindersCreated++;

                    // Track the created reminder info for channel messages
                    var eventTime = ExtractEventTimeFromContent(extractedEvent.Title, weekLetterContent);
                    createdReminderInfos.Add(new CreatedReminderInfo
                    {
                        Title = ImproveReminderTitle(extractedEvent.Title),
                        Date = extractedEvent.EventDate,
                        EventTime = eventTime
                    });

                    _logger.LogInformation("Created reminder for {ChildName}: {ReminderText}",
                        childName, reminder.Text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create reminder for extracted event: {EventTitle}",
                        extractedEvent.Title);
                }
            }

            // Mark week letter as processed
            await _weekLetterRepository.MarkAutoRemindersExtractedAsync(childName, weekNumber, year);

            _logger.LogInformation("Successfully created {Count} reminders for {ChildName} week {WeekNumber}/{Year}",
                remindersCreated, childName, weekNumber, year);

            return new ReminderExtractionResult
            {
                Success = true,
                RemindersCreated = remindersCreated,
                CreatedReminders = createdReminderInfos
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract reminders for {ChildName} week {WeekNumber}/{Year}",
                childName, weekNumber, year);
            return new ReminderExtractionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private string ExtractContentFromWeekLetter(JObject weekLetter)
    {
        try
        {
            // Try multiple possible content field names
            var contentFields = new[] { "content", "Content", "text", "Text", "body", "Body", "html", "Html" };

            foreach (var field in contentFields)
            {
                var fieldValue = weekLetter[field]?.ToString();
                if (!string.IsNullOrWhiteSpace(fieldValue))
                {
                    // Remove HTML tags and clean up
                    var cleaned = System.Text.RegularExpressions.Regex.Replace(fieldValue, @"<[^>]+>", " ");
                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
                    return cleaned.Trim();
                }
            }

            // Fallback: convert entire JObject to string
            return weekLetter.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<List<ExtractedEvent>> ExtractEventsFromWeekLetterAsync(string weekLetterContent)
    {
        try
        {
            var prompt = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(weekLetterContent, DateTime.Now);

            var completionResult = await _openAiClient.CreateCompletionAsync(prompt, _aiModel);

            if (!completionResult.IsSuccess || string.IsNullOrWhiteSpace(completionResult.Content))
            {
                _logger.LogWarning("OpenAI completion failed or returned empty content");
                return new List<ExtractedEvent>();
            }

            return ParseExtractedEvents(completionResult.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract events from week letter using AI");
            return new List<ExtractedEvent>();
        }
    }

    private List<ExtractedEvent> ParseExtractedEvents(string aiResponse)
    {
        try
        {
            // Clean response - remove potential markdown formatting
            var cleanedResponse = aiResponse.Trim();
            if (cleanedResponse.StartsWith("```json"))
            {
                cleanedResponse = cleanedResponse.Substring(7);
            }
            if (cleanedResponse.EndsWith("```"))
            {
                cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
            }
            cleanedResponse = cleanedResponse.Trim();

            var jsonEvents = JArray.Parse(cleanedResponse);
            return jsonEvents
                .Select(je => new ExtractedEvent
                {
                    Title = je["title"]?.ToString() ?? string.Empty,
                    Description = je["description"]?.ToString() ?? string.Empty,
                    EventDate = DateTime.Parse(je["date"]?.ToString() ?? DateTime.Today.ToString("yyyy-MM-dd")),
                    EventType = je["type"]?.ToString() ?? "event",
                    ConfidenceScore = je["confidence"]?.Value<double>() ?? 0.8
                })
                .Where(e => e.ConfidenceScore >= 0.6)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI response as JSON: {Response}", aiResponse);
            return new List<ExtractedEvent>();
        }
    }



    private Reminder CreateReminderFromExtractedEvent(
        ExtractedEvent extractedEvent, string childName, int? weekLetterId)
    {
        var reminderDate = DateOnly.FromDateTime(extractedEvent.EventDate);

        return new Reminder
        {
            Text = $"{extractedEvent.Title} - {extractedEvent.Description}",
            RemindDate = reminderDate,
            RemindTime = _defaultReminderTime,
            ChildName = childName,
            CreatedBy = "ai_extraction",
            Source = "auto_extracted",
            WeekLetterId = weekLetterId,
            EventType = extractedEvent.EventType,
            EventTitle = extractedEvent.Title,
            ExtractedDateTime = DateTime.UtcNow,
            ConfidenceScore = (decimal)extractedEvent.ConfidenceScore
        };
    }

    private string? ExtractEventTimeFromContent(string eventTitle, string weekLetterContent)
    {
        try
        {
            // Look for time patterns near the event title in the content
            var titleIndex = weekLetterContent.IndexOf(eventTitle, StringComparison.OrdinalIgnoreCase);
            if (titleIndex == -1) return null;

            // Extract a window of text around the title (100 chars before and after)
            var start = Math.Max(0, titleIndex - 100);
            var end = Math.Min(weekLetterContent.Length, titleIndex + eventTitle.Length + 100);
            var contextWindow = weekLetterContent.Substring(start, end - start);

            // Look for time patterns like "10:45", "kl. 10:45", "10.45-11.30"
            var timePattern = @"(?:kl\.?\s*)?(\d{1,2}[:.]\d{2}(?:-\d{1,2}[:.]\d{2})?)";
            var timeMatch = Regex.Match(contextWindow, timePattern, RegexOptions.IgnoreCase);

            if (timeMatch.Success)
            {
                return timeMatch.Groups[1].Value.Replace(":", ".");
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string ImproveReminderTitle(string originalTitle)
    {
        // Clean up and improve the title for better readability
        var improved = originalTitle.Trim();

        // Remove common prefixes that add no value
        var prefixesToRemove = new[] { "Event: ", "Deadline: ", "Activity: ", "Task: " };
        foreach (var prefix in prefixesToRemove)
        {
            if (improved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                improved = improved.Substring(prefix.Length).Trim();
            }
        }

        // Ensure first letter is capitalized
        if (improved.Length > 0)
        {
            improved = char.ToUpper(improved[0]) + improved.Substring(1);
        }

        return improved;
    }
}

public class ExtractedEvent
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } = "event";
    public double ConfidenceScore { get; set; } = 0.8;
}