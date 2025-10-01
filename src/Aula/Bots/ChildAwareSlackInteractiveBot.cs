using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Services;

namespace Aula.Bots;

/// <summary>
/// Child-specific Slack interactive bot that only knows about ONE child.
/// This bot instance is dedicated to a single child and has no knowledge of other children.
/// </summary>
public class ChildAwareSlackInteractiveBot : IDisposable
{
	private readonly IServiceProvider _serviceProvider;
	private readonly IChildServiceCoordinator _coordinator;
	private readonly Config _config;
	private readonly ILogger _logger;
	private readonly HttpClient _httpClient;

	// This bot is dedicated to ONE specific child
	private Child? _assignedChild;
	public string? AssignedChildName => _assignedChild?.FirstName;

	// _isRunning field removed - value never read
	private Timer? _pollingTimer;
	private Timer? _cleanupTimer;
	private string _lastTimestamp = "0";
	private readonly object _lockObject = new object();
	private int _pollingInProgress;

	// Track sent messages to avoid processing our own
	private readonly ConcurrentDictionary<string, byte> _sentMessageIds = new ConcurrentDictionary<string, byte>();
	private readonly ConcurrentDictionary<string, DateTime> _messageTimestamps = new ConcurrentDictionary<string, DateTime>();

	public ChildAwareSlackInteractiveBot(
		IServiceProvider serviceProvider,
		IChildServiceCoordinator coordinator,
		Config config,
		ILoggerFactory loggerFactory,
		HttpClient? httpClient = null)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ChildAwareSlackInteractiveBot>();
		_httpClient = httpClient ?? new HttpClient();

		if (httpClient == null)
		{
			_httpClient.Timeout = TimeSpan.FromSeconds(30);
		}

		// Bot will be assigned to a specific child via StartForChild method
	}

	public async Task StartForChild(Child child)
	{
		_assignedChild = child ?? throw new ArgumentNullException(nameof(child));

		if (string.IsNullOrEmpty(_assignedChild.Channels?.Slack?.ApiToken))
		{
			_logger.LogError("Cannot start Slack bot for {ChildName}: API token is missing", _assignedChild.FirstName);
			return;
		}

		if (string.IsNullOrEmpty(_assignedChild.Channels?.Slack?.ChannelId))
		{
			_logger.LogError("Cannot start Slack bot for {ChildName}: Channel ID is missing", _assignedChild.FirstName);
			return;
		}

		_logger.LogInformation("Starting Slack bot for child: {ChildName}", _assignedChild.FirstName);

		// Configure HTTP client for this child's Slack API token
		_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _assignedChild.Channels.Slack.ApiToken);

		// Get current timestamp
		_lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

		_logger.LogInformation("Initial timestamp set to: {Timestamp}", _lastTimestamp + ".000000");

		// Start polling timer
		int pollingInterval = _config.Timers.SlackPollingIntervalSeconds * 1000;
		_pollingTimer = new Timer(async _ => await PollForMessages(), null, pollingInterval, pollingInterval);

		_logger.LogInformation("Slack polling started - checking every {Seconds} seconds", _config.Timers.SlackPollingIntervalSeconds);

		// Start cleanup timer (every hour)
		_cleanupTimer = new Timer(_ => CleanupOldMessages(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

		_logger.LogInformation("Slack cleanup timer started - running every 1 hour");

		// Send startup message
		await SendMessageToSlack($"👋 Bot for {_assignedChild.FirstName} is now online and ready to help!");
	}

	private async Task PollForMessages()
	{
		if (Interlocked.Exchange(ref _pollingInProgress, 1) == 1)
		{
			return; // Already polling
		}

		try
		{
			var url = $"https://slack.com/api/conversations.history?channel={_assignedChild?.Channels?.Slack?.ChannelId}&oldest={_lastTimestamp}";
			var response = await _httpClient.GetAsync(url);

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to poll Slack messages. Status: {StatusCode}", response.StatusCode);
				return;
			}

			var content = await response.Content.ReadAsStringAsync();
			var json = JObject.Parse(content);

			if (json["ok"]?.Value<bool>() != true)
			{
				_logger.LogError("Slack API returned error: {Error}", json["error"]?.ToString());
				return;
			}

			var messages = json["messages"] as JArray;
			if (messages == null || messages.Count == 0)
			{
				return;
			}

			// Process messages in chronological order
			foreach (var message in messages.OrderBy(m => m["ts"]?.ToString()))
			{
				await ProcessMessage(message as JObject);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error polling for Slack messages");
		}
		finally
		{
			Interlocked.Exchange(ref _pollingInProgress, 0);
		}
	}

	private async Task ProcessMessage(JObject? message)
	{
		if (message == null) return;

		var messageId = message["ts"]?.ToString();
		var userId = message["user"]?.ToString();
		var text = message["text"]?.ToString();
		var subtype = message["subtype"]?.ToString();

		if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(text))
		{
			return;
		}

		// Update timestamp for next poll
		_lastTimestamp = messageId;

		// Skip our own messages
		if (_sentMessageIds.ContainsKey(messageId))
		{
			return;
		}

		// Skip bot messages
		if (subtype == "bot_message" || message["bot_id"] != null)
		{
			return;
		}

		_logger.LogInformation("Processing message for {ChildName} from user {UserId}: {Text}",
			_assignedChild?.FirstName, userId, text);

		// This bot only knows about its assigned child
		if (_assignedChild == null)
		{
			_logger.LogError("Bot has no assigned child");
			return;
		}

		var child = _assignedChild;

		// Process the message using direct service calls with child parameters
		try
		{
			// This needs to be refactored to use a proper service that accepts Child parameters
			// For now, we'll get a temporary basic response
			var response = await _coordinator.ProcessAiQueryForChildAsync(child, text);

			await SendMessageToSlack(response);

			_logger.LogInformation("Processed message for child {ChildName} successfully", child.FirstName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing message for child {ChildName}", child.FirstName);
			await SendMessageToSlack($"Sorry, I encountered an error processing your request about {child.FirstName}.");
		}
	}

	// This bot doesn't need to extract child names - it only knows about one child

	public async Task SendMessageToSlack(string text, string? threadTs = null)
	{
		var payload = new
		{
			channel = _assignedChild?.Channels?.Slack?.ChannelId,
			text = text,
			thread_ts = threadTs
		};

		var json = JsonSerializer.Serialize(payload);
		var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

		try
		{
			var response = await _httpClient.PostAsync(
				"https://slack.com/api/chat.postMessage",
				content);

			if (response.IsSuccessStatusCode)
			{
				var responseContent = await response.Content.ReadAsStringAsync();
				var responseJson = JObject.Parse(responseContent);
				var messageId = responseJson["ts"]?.ToString();

				if (!string.IsNullOrEmpty(messageId))
				{
					_sentMessageIds.TryAdd(messageId, 0);
					_messageTimestamps.TryAdd(messageId, DateTime.UtcNow);
				}

				_logger.LogInformation("Message sent successfully to Slack");
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
			_logger.LogError(ex, "Error sending message to Slack");
		}
	}

	private void CleanupOldMessages()
	{
		var cutoff = DateTime.UtcNow.AddHours(-2);
		var toRemove = _messageTimestamps
			.Where(kvp => kvp.Value < cutoff)
			.Select(kvp => kvp.Key)
			.ToList();

		foreach (var messageId in toRemove)
		{
			_sentMessageIds.TryRemove(messageId, out _);
			_messageTimestamps.TryRemove(messageId, out _);
		}

		if (toRemove.Count > 0)
		{
			_logger.LogDebug("Cleaned up {Count} old message IDs", toRemove.Count);
		}
	}

	public void Stop()
	{
		_pollingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		_cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		_logger.LogInformation("Slack bot stopped");
	}

	public void Dispose()
	{
		Stop();
		_pollingTimer?.Dispose();
		_cleanupTimer?.Dispose();
		_httpClient?.Dispose();
	}
}
