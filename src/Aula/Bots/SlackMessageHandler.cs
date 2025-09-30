using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Aula.Tools;
using Aula.Utilities;

namespace Aula.Bots;

public class SlackMessageHandler
{
	private readonly IAgentService _agentService;
	private readonly Config _config;
	private readonly ILogger _logger;
	private readonly HttpClient _httpClient;
	private readonly Dictionary<string, Child> _childrenByName;
	private readonly ConversationContext _conversationContext;
	private readonly ReminderCommandHandler _reminderHandler;

	public SlackMessageHandler(
		IAgentService agentService,
		Config config,
		ILogger logger,
		HttpClient httpClient,
		Dictionary<string, Child> childrenByName,
		ConversationContext conversationContext,
		ReminderCommandHandler reminderHandler)
	{
		ArgumentNullException.ThrowIfNull(agentService);
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(childrenByName);
		ArgumentNullException.ThrowIfNull(conversationContext);
		ArgumentNullException.ThrowIfNull(reminderHandler);

		_agentService = agentService;
		_config = config;
		_logger = logger;
		_httpClient = httpClient;
		_childrenByName = childrenByName;
		_conversationContext = conversationContext;
		_reminderHandler = reminderHandler;
	}

	public async Task<bool> HandleMessageAsync(JObject eventData)
	{
		var channel = eventData["channel"]?.ToString();
		var text = eventData["text"]?.ToString();
		var messageId = eventData["ts"]?.ToString();
		var threadTs = eventData["thread_ts"]?.ToString();

		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(channel))
		{
			_logger.LogWarning("Received message with missing text or channel");
			return false;
		}

		_logger.LogInformation("Processing Slack message in channel {Channel}: {Text}", channel, text);

		try
		{
			// Extract child name from the message
			var childName = ExtractChildName(text);

			// Get the specific child object if a name was extracted
			Child? specificChild = null;
			if (!string.IsNullOrEmpty(childName))
			{
				specificChild = await _agentService.GetChildByNameAsync(childName);
				if (specificChild != null)
				{
					_logger.LogInformation("Processing message for specific child: {ChildName}", specificChild.FirstName);
				}
			}

			// Use the tool-based processing with specific child isolation
			string contextKey = $"slack-{channel}";
			string response = await _agentService.ProcessQueryWithToolsAsync(text, contextKey, specificChild, ChatInterface.Slack);

			// Update conversation context
			var (isAboutToday, isAboutTomorrow, isAboutHomework) = ExtractContextFlags(text);
			UpdateConversationContext(childName, isAboutToday, isAboutTomorrow, isAboutHomework);

			// Send response to Slack
			await SendMessageToSlack(channel, response, threadTs);

			_logger.LogInformation("Sent response to Slack channel {Channel}", channel);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing Slack message: {Text}", text);
			await SendMessageToSlack(channel, "Sorry, I encountered an error processing your message. Please try again.", threadTs);
			return false;
		}
	}

	private async Task SendMessageToSlack(string channel, string text, string? threadTs = null)
	{
		if (string.IsNullOrEmpty(_config.Slack.ApiToken))
		{
			_logger.LogWarning("Slack bot token not configured");
			return;
		}

		var payload = new
		{
			channel = channel,
			text = text,
			thread_ts = threadTs
		};

		var json = JsonSerializer.Serialize(payload);
		var content = new StringContent(json, Encoding.UTF8, "application/json");


		try
		{
			var response = await _httpClient.PostAsync($"{_config.Slack.ApiBaseUrl.TrimEnd('/')}/chat.postMessage", content);

			if (response.IsSuccessStatusCode)
			{
				_logger.LogInformation("Message sent successfully to Slack channel {Channel}", channel);
			}
			else
			{
				var errorContent = await response.Content.ReadAsStringAsync();
				_logger.LogError("Failed to send message to Slack. Status: {StatusCode}, Content: {Content}",
					response.StatusCode, errorContent);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending message to Slack channel {Channel}", channel);
		}
	}

	private string? ExtractChildName(string text)
	{
		if (string.IsNullOrEmpty(text))
			return null;

		text = text.ToLowerInvariant();

		// Try follow-up context first
		var childFromContext = TryExtractFromFollowUpContext(text);
		if (childFromContext != null)
			return childFromContext;

		// Try follow-up phrases
		var childFromFollowUp = TryExtractFromFollowUpPhrases(text);
		if (childFromFollowUp != null)
			return childFromFollowUp;

		// Try standard extraction
		return TryStandardExtraction(text);
	}

	private string? TryExtractFromFollowUpContext(string text)
	{
		// For very short follow-up questions with no clear child name, use the last child from context
		if (text.Length < 15 &&
			(_conversationContext.IsStillValid && _conversationContext.LastChildName != null) &&
			(text.Contains("what about") || text.Contains("how about") ||
			 text.Contains("hvad med") || text.Contains("hvordan med") ||
			 text.StartsWith("og") || text.StartsWith("and")))
		{
			// Try to extract a different child name from the follow-up
			var childFromFullName = FindChildByFullName(text);
			if (childFromFullName != null)
			{
				_logger.LogInformation("Found child name in follow-up: {ChildName}", childFromFullName);
				return childFromFullName;
			}

			var childFromFirstName = FindChildByFirstName(text);
			if (childFromFirstName != null)
			{
				_logger.LogInformation("Found first name in follow-up: {ChildName}", childFromFirstName);
				return childFromFirstName;
			}

			// If no child name found in the follow-up, use the last child from context
			_logger.LogInformation("No child name in follow-up, using context: {ChildName}", _conversationContext.LastChildName);
			return _conversationContext.LastChildName;
		}

		return null;
	}

	private string? TryExtractFromFollowUpPhrases(string text)
	{
		string[] followUpPhrases = { "hvad med", "what about", "how about", "hvordan med", "og hvad", "and what" };
		foreach (var phrase in followUpPhrases)
		{
			int index = text.IndexOf(phrase);
			if (index >= 0)
			{
				string afterPhrase = text.Substring(index + phrase.Length).Trim();
				_logger.LogInformation("Follow-up phrase detected: '{Phrase}', text after: '{AfterPhrase}'", phrase, afterPhrase);

				var childFromFullName = FindChildByFullName(afterPhrase);
				if (childFromFullName != null)
				{
					_logger.LogInformation("Found child name after follow-up phrase: {ChildName}", childFromFullName);
					return childFromFullName;
				}

				var childFromFirstName = FindChildByFirstName(afterPhrase);
				if (childFromFirstName != null)
				{
					_logger.LogInformation("Found first name after follow-up phrase: {ChildName}", childFromFirstName);
					return childFromFirstName;
				}
			}
		}

		return null;
	}

	private string? TryStandardExtraction(string text)
	{
		// Standard child name extraction - check for each child name in the text
		var childFromFullName = FindChildByFullName(text);
		if (childFromFullName != null)
		{
			_logger.LogInformation("Found full child name in text: {ChildName}", childFromFullName);
			return childFromFullName;
		}

		var childFromFirstName = FindChildByFirstName(text);
		if (childFromFirstName != null)
		{
			_logger.LogInformation("Found first name in text: {ChildName}", childFromFirstName);
			return childFromFirstName;
		}

		_logger.LogInformation("No child name found in text");
		return null;
	}

	private string? FindChildByFullName(string text)
	{
		foreach (var childName in _childrenByName.Keys)
		{
			if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
			{
				return childName;
			}
		}
		return null;
	}

	private string? FindChildByFirstName(string text)
	{
		foreach (var child in _childrenByName.Values)
		{
			string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
			if (Regex.IsMatch(text, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
			{
				return _childrenByName.Keys.FirstOrDefault(k =>
					k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase));
			}
		}
		return null;
	}

	private void UpdateConversationContext(string? childName, bool isAboutToday, bool isAboutTomorrow, bool isAboutHomework)
	{
		_conversationContext.LastChildName = childName;
		_conversationContext.WasAboutToday = isAboutToday;
		_conversationContext.WasAboutTomorrow = isAboutTomorrow;
		_conversationContext.WasAboutHomework = isAboutHomework;
		_conversationContext.Timestamp = DateTime.Now;

		_logger.LogInformation("Updated conversation context: {Context}", _conversationContext);
	}

	private (bool isAboutToday, bool isAboutTomorrow, bool isAboutHomework) ExtractContextFlags(string text)
	{
		var lowerText = text.ToLowerInvariant();
		bool isAboutToday = lowerText.Contains("today") || lowerText.Contains("i dag");
		bool isAboutTomorrow = lowerText.Contains("tomorrow") || lowerText.Contains("i morgen");
		bool isAboutHomework = lowerText.Contains("homework") || lowerText.Contains("lektier");
		return (isAboutToday, isAboutTomorrow, isAboutHomework);
	}
}
