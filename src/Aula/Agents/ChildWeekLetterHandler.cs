using System;
using System.Threading.Tasks;
using Aula.Channels;
using Aula.Configuration;
using Aula.Context;
using Aula.Events;
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
	private readonly IChildChannelManager _channelManager;
	private readonly ILogger<ChildWeekLetterHandler> _logger;

	public ChildWeekLetterHandler(
		Child child,
		IChildChannelManager channelManager,
		ILogger<ChildWeekLetterHandler> logger)
	{
		_child = child ?? throw new ArgumentNullException(nameof(child));
		_channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Handles a week letter event by posting it to the child's configured channels.
	/// </summary>
	public async Task HandleWeekLetterEventAsync(ChildWeekLetterEventArgs args)
	{
		// Filter by child name (defensive check)
		if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		_logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

		try
		{
			// Check if child has configured channels
			if (!await _channelManager.HasConfiguredChannelsAsync())
			{
				_logger.LogWarning("No channels configured for {ChildName}", args.ChildFirstName);
				return;
			}

			// Validate week letter content
			if (args.WeekLetter == null)
			{
				_logger.LogWarning("No week letter to post for {ChildName}", args.ChildFirstName);
				return;
			}

			// Format week letter message
			var message = FormatWeekLetterMessage(args.WeekLetter, args.WeekNumber, args.Year);

			// Send to child's channels using IChildChannelManager
			var success = await _channelManager.SendMessageAsync(message, MessageFormat.Markdown);

			if (success)
			{
				_logger.LogInformation("Posted week letter to channels for {ChildName}", args.ChildFirstName);
			}
			else
			{
				_logger.LogError("Failed to post week letter to channels for {ChildName}", args.ChildFirstName);
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
		var title = $"📚 Ugebrev for uge {weekNumber}/{year} - {_child.FirstName}";

		// Extract the week letter content
		var content = weekLetter.ToString();

		// Format for better readability
		return $"{title}\n\n{content}";
	}
}