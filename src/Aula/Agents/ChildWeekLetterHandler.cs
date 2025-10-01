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
	private readonly ILogger<ChildWeekLetterHandler> _logger;
	private readonly Html2SlackMarkdownConverter _html2MarkdownConverter;

	public ChildWeekLetterHandler(
		Child child,
		ILogger<ChildWeekLetterHandler> logger)
	{
		_child = child ?? throw new ArgumentNullException(nameof(child));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_html2MarkdownConverter = new Html2SlackMarkdownConverter();
	}

	/// <summary>
	/// Handles a week letter event by posting it to the child's Slack channel via the interactive bot.
	/// </summary>
	public async Task HandleWeekLetterEventAsync(ChildWeekLetterEventArgs args, ChildAwareSlackInteractiveBot? slackBot)
	{
		// Filter by child name (defensive check)
		if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		_logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

		try
		{
			if (slackBot != null && args.WeekLetter != null)
			{
				// Format the week letter message
				var message = FormatWeekLetterMessage(args.WeekLetter, args.WeekNumber, args.Year);

				// Post via the existing interactive bot
				await slackBot.SendMessageToSlack(message);
				var success = true;

				if (success)
				{
					_logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
				}
				else
				{
					_logger.LogError("Failed to post week letter to Slack for {ChildName}", args.ChildFirstName);
				}
			}
			else if (args.WeekLetter != null)
			{
				_logger.LogWarning("Slack bot not available for {ChildName}", args.ChildFirstName);
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