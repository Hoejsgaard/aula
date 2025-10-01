using System;
using System.Threading.Tasks;
using Aula.Bots;
using Aula.Channels;
using Aula.Configuration;
using Aula.Events;
using Aula.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Agents;

/// <summary>
/// Handles week letter events for a specific child using child-aware channel management.
/// Operates within a child context scope and uses proper dependency injection.
/// </summary>
public class ChildWeekLetterHandler
{
    private readonly Child _child;
    private readonly ILogger _logger;
    private readonly Html2SlackMarkdownConverter _html2MarkdownConverter;

    public ChildWeekLetterHandler(
        Child child,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _child = child;
        _logger = loggerFactory.CreateLogger<ChildWeekLetterHandler>();
        _html2MarkdownConverter = new Html2SlackMarkdownConverter();
    }

    /// <summary>
    /// Handles a week letter event by posting it to the child's Slack and Telegram channels via the interactive bots.
    /// </summary>
    public async Task HandleWeekLetterEventAsync(ChildWeekLetterEventArgs args, SlackInteractiveBot? slackBot, TelegramInteractiveBot? telegramBot = null)
    {
        // Filter by child name (defensive check)
        if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

        try
        {
            if (args.WeekLetter != null)
            {
                // Format the week letter message
                var message = FormatWeekLetterMessage(args.WeekLetter, args.WeekNumber, args.Year);

                // Post to Slack if bot available
                if (slackBot != null)
                {
                    await slackBot.SendMessageToSlack(message);
                    _logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
                }

                // Post to Telegram if bot available
                if (telegramBot != null)
                {
                    await telegramBot.SendMessageToTelegram(message);
                    _logger.LogInformation("Posted week letter to Telegram for {ChildName}", args.ChildFirstName);
                }

                if (slackBot == null && telegramBot == null)
                {
                    _logger.LogWarning("No bots available for {ChildName}", args.ChildFirstName);
                }
            }
            else
            {
                _logger.LogWarning("No week letter to post for {ChildName}", args.ChildFirstName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing week letter event for child: {ChildName}", args.ChildFirstName);
        }
    }

    /// <summary>
    /// Formats a week letter for display in channels.
    /// </summary>
    private string FormatWeekLetterMessage(JObject weekLetter, int weekNumber, int year)
    {
        // Extract class and week information from the JSON structure
        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? weekNumber.ToString();

        // Extract HTML content and convert to readable text
        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        var letterText = _html2MarkdownConverter.Convert(htmlContent).Replace("**", "*");

        // Format the title
        var title = $"Ugebrev for {_child.FirstName} ({@class}) uge {week}";

        // Return formatted message
        return $"{title}\n\n{letterText}";
    }
}
