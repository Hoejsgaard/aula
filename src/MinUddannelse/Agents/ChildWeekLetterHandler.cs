using System;
using MinUddannelse.Content.Processing;
using System.Threading.Tasks;
using MinUddannelse.Bots;
using MinUddannelse.Configuration;
using MinUddannelse.Events;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Agents;

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

    public async Task HandleWeekLetterEventAsync(ChildWeekLetterEventArgs args, SlackInteractiveBot? slackBot, TelegramInteractiveBot? telegramBot = null)
    {
        if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

        try
        {
            if (args.WeekLetter != null)
            {
                if (slackBot != null)
                {
                    var slackMessage = FormatWeekLetterMessageForSlack(args.WeekLetter, args.WeekNumber, args.Year);
                    await slackBot.SendMessageToSlack(slackMessage);
                    _logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
                }

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

    private string FormatWeekLetterMessageForSlack(JObject weekLetter, int weekNumber, int year)
    {
        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? weekNumber.ToString();

        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        var letterText = _html2MarkdownConverter.Convert(htmlContent).Replace("**", "*");

        var title = $"Ugebrev for {@class} uge {week}";

        return $"{title}\n\n{letterText}";
    }

    private string FormatWeekLetterMessageForTelegram(JObject weekLetter, int weekNumber, int year)
    {
        var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
        var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? weekNumber.ToString();

        var htmlContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        _logger.LogInformation("Original HTML content length: {Length}, content: {Content}", htmlContent.Length, htmlContent);
        var letterText = _html2TelegramConverter.Convert(htmlContent);
        _logger.LogInformation("Converted text length: {Length}, content: {Content}", letterText.Length, letterText);

        var title = $"<b>Ugebrev for {@class} uge {week}</b>";

        return $"{title}\n\n{letterText}";
    }
}
