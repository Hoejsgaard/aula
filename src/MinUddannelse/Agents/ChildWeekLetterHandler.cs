using System;
using MinUddannelse.Content.Processing;
using System.Threading.Tasks;
using MinUddannelse.Bots;
using MinUddannelse.Configuration;
using MinUddannelse.Events;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Agents;

/// <summary>
/// Handles week letter events for a specific child using child-aware channel management.
/// Operates within a child context scope and uses proper dependency injection.
/// </summary>
public class ChildWeekLetterHandler
{
    private readonly Child _child;
    private readonly ILogger _logger;
    private readonly Html2SlackMarkdownConverter _html2MarkdownConverter;
    private readonly Html2TelegramConverter _html2TelegramConverter;

    public ChildWeekLetterHandler(
        Child child,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _child = child;
        _logger = loggerFactory.CreateLogger<ChildWeekLetterHandler>();
        _html2MarkdownConverter = new Html2SlackMarkdownConverter();
        _html2TelegramConverter = new Html2TelegramConverter();
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
                // Post to Slack if bot available
                if (slackBot != null)
                {
                    var slackMessage = FormatWeekLetterMessageForSlack(args.WeekLetter, args.WeekNumber, args.Year);
                    await slackBot.SendMessageToSlack(slackMessage);
                    _logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
                }

                // Post to Telegram if bot available
                if (telegramBot != null)
                {
                    var telegramMessage = FormatWeekLetterMessageForTelegram(args.WeekLetter, args.WeekNumber, args.Year);
                    await telegramBot.SendMessageToTelegram(telegramMessage);
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
    /// Formats a week letter for display in Slack (Markdown formatting).
    /// </summary>
    private string FormatWeekLetterMessageForSlack(JObject weekLetter, int weekNumber, int year)
    {
        // Extract class and week information from the JSON structure
        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? weekNumber.ToString();

        // Extract HTML content and convert to Slack-compatible Markdown
        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        var letterText = _html2MarkdownConverter.Convert(htmlContent).Replace("**", "*");

        // Format the title
        var title = $"Ugebrev for {@class} uge {week}";

        return $"{title}\n\n{letterText}";
    }

    /// <summary>
    /// Formats a week letter for display in Telegram (HTML formatting).
    /// </summary>
    private string FormatWeekLetterMessageForTelegram(JObject weekLetter, int weekNumber, int year)
    {
        // Extract class and week information from the JSON structure
        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? weekNumber.ToString();

        // Extract HTML content and convert to Telegram-compatible HTML
        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        _logger.LogInformation("Original HTML content length: {Length}, content: {Content}", htmlContent.Length, htmlContent);
        var letterText = _html2TelegramConverter.Convert(htmlContent);
        _logger.LogInformation("Converted text length: {Length}, content: {Content}", letterText.Length, letterText);

        // Format the title with HTML bold tags
        var title = $"<b>Ugebrev for {@class} uge {week}</b>";

        return $"{title}\n\n{letterText}";
    }
}
