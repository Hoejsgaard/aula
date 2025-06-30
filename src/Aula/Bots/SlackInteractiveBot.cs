using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Aula.Utilities;
using Aula.Tools;

namespace Aula.Bots;

public class SlackInteractiveBot : IDisposable
{
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, Child> _childrenByName;
    private bool _isRunning;
    private Timer? _pollingTimer;
    private Timer? _cleanupTimer;
    private string _lastTimestamp = "0"; // Start from the beginning of time
    private readonly object _lockObject = new object();
    private int _pollingInProgress = 0;
    private readonly ConcurrentDictionary<string, byte> _postedWeekLetterHashes = new ConcurrentDictionary<string, byte>();
    // Track our own message IDs to avoid processing them
    private readonly ConcurrentDictionary<string, byte> _sentMessageIds = new ConcurrentDictionary<string, byte>();
    // Keep track of when messages were sent to allow cleanup
    private readonly ConcurrentDictionary<string, DateTime> _messageTimestamps = new ConcurrentDictionary<string, DateTime>();
    private readonly SlackMessageHandler _messageHandler;
    // Language detection arrays removed - GPT handles language detection naturally

    // Conversation context management moved to SlackMessageHandler

    private readonly ISupabaseService _supabaseService;

    public SlackInteractiveBot(
        IAgentService agentService,
        Config config,
        ILoggerFactory loggerFactory,
        ISupabaseService supabaseService)
    {
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<SlackInteractiveBot>();
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30); // Add 30 second timeout
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _childrenByName = _config.Children.ToDictionary(
            c => c.FirstName.ToLowerInvariant(),
            c => c);

        var conversationContext = new ConversationContext();
        var reminderHandler = new ReminderCommandHandler(_logger, _supabaseService, _childrenByName);
        _messageHandler = new SlackMessageHandler(_agentService, _config, _logger, _httpClient, _childrenByName, conversationContext, reminderHandler);
    }

    public async Task Start()
    {
        if (string.IsNullOrEmpty(_config.Slack.ApiToken))
        {
            _logger.LogError("Cannot start Slack bot: API token is missing");
            return;
        }

        if (string.IsNullOrEmpty(_config.Slack.ChannelId))
        {
            _logger.LogError("Cannot start Slack bot: Channel ID is missing");
            return;
        }

        _logger.LogInformation("Starting Slack polling bot");

        // Configure the HTTP client for Slack API
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Slack.ApiToken);

        // Set the timestamp to now so we don't process old messages
        // Slack uses a timestamp format like "1234567890.123456"
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _lastTimestamp = $"{unixTimestamp}.000000";
        _logger.LogInformation("Initial timestamp set to: {Timestamp}", _lastTimestamp);

        // Start polling
        _isRunning = true;
        var pollingInterval = TimeSpan.FromSeconds(_config.Timers.SlackPollingIntervalSeconds);
        _pollingTimer = new Timer(PollMessages, null, TimeSpan.Zero, pollingInterval);
        _logger.LogInformation("Slack polling started - checking every {IntervalSeconds} seconds", _config.Timers.SlackPollingIntervalSeconds);

        // Start message ID cleanup timer
        var cleanupInterval = TimeSpan.FromHours(_config.Timers.CleanupIntervalHours);
        _cleanupTimer = new Timer(CleanupOldMessageIds, null, cleanupInterval, cleanupInterval);
        _logger.LogInformation("Slack cleanup timer started - running every {IntervalHours} hours", _config.Timers.CleanupIntervalHours);

        // Build a list of available children (first names only)
        string childrenList = string.Join(" og ", _childrenByName.Values.Select(c =>
            c.FirstName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? c.FirstName));

        // Get the current week number
        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);

        // Send welcome message in Danish with children info and usage hints
        await SendMessageInternal($"🤖 Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}\n\n" +
                                 "Du kan spørge mig om:\n" +
                                 "• Aktiviteter for en bestemt dag: 'Hvad skal Emma i dag?'\n" +
                                 "• Oprette påmindelser: 'Mind mig om at hente Hans kl 15'\n" +
                                 "• Se ugeplaner: 'Vis ugeplanen for denne uge'\n" +
                                 "• Hjælp: 'hjælp' eller 'help'");

        _logger.LogInformation("Slack polling bot started");
    }

    public void Stop()
    {
        _isRunning = false;
        _pollingTimer?.Dispose();
        _logger.LogInformation("Slack polling bot stopped");
    }

    private void PollMessages(object? state)
    {
        // Don't use locks with async/await as it can lead to deadlocks
        // Instead, use a simple flag to prevent concurrent executions
        if (!_isRunning || Interlocked.Exchange(ref _pollingInProgress, 1) == 1)
        {
            return;
        }

        // Use Fire and Forget pattern instead of async void
        _ = Task.Run(async () =>
        {
            try
            {
                // Build the API URL for conversations.history
                // Add a small buffer to the timestamp to avoid duplicate messages
                var adjustedTimestamp = _lastTimestamp;
                if (!string.IsNullOrEmpty(_lastTimestamp) && _lastTimestamp != "0")
                {
                    // Slack timestamps are in the format "1234567890.123456"
                    // We need to ensure we're handling them correctly
                    if (_lastTimestamp.Contains("."))
                    {
                        // Already in correct format, add a tiny fraction to avoid duplicates
                        adjustedTimestamp = _lastTimestamp;
                    }
                    else if (double.TryParse(_lastTimestamp, out double ts))
                    {
                        // Convert to proper Slack timestamp format if needed
                        adjustedTimestamp = ts.ToString("0.000000");
                    }
                }

                // Removed noisy polling log - only log when there are actual messages
                var url = $"https://slack.com/api/conversations.history?channel={_config.Slack.ChannelId}&oldest={adjustedTimestamp}&limit=10";

                // Make the API call
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch messages: HTTP {StatusCode}", response.StatusCode);
                    return;
                }

                // Parse the response
                var content = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(content);

                if (data["ok"]?.Value<bool>() != true)
                {
                    string error = data["error"]?.ToString() ?? "unknown error";

                    // Handle the not_in_channel error
                    if (error == "not_in_channel")
                    {
                        _logger.LogWarning("Bot is not in the channel. Attempting to join...");
                        await JoinChannel();
                        return;
                    }

                    _logger.LogError("Failed to fetch messages: {Error}", error);
                    return;
                }

                // Get the messages
                var messages = data["messages"] as JArray;
                if (messages == null || !messages.Any())
                {
                    return;
                }

                // Only log if there are actual new user messages (removed spam)

                // Get actual new messages (not from the bot)
                var userMessages = messages.Where(m =>
                {
                    // Skip messages we sent ourselves (by checking the ID)
                    string messageId = m["ts"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(messageId) && _sentMessageIds.ContainsKey(messageId))
                    {
                        // Skip our own message (removed log spam)
                        return false;
                    }

                    // Skip bot messages
                    if (m["subtype"]?.ToString() == "bot_message" || m["bot_id"] != null)
                    {
                        return false;
                    }

                    // Only include messages with a user ID
                    return !string.IsNullOrEmpty(m["user"]?.ToString());
                }).ToList();

                if (!userMessages.Any())
                {
                    return;
                }

                _logger.LogInformation("Found {Count} new user messages", userMessages.Count);

                // Keep track of the latest timestamp
                string latestTimestamp = _lastTimestamp;

                // Process messages in chronological order (oldest first)
                foreach (var message in userMessages.OrderBy(m => m["ts"]?.ToString()))
                {
                    // Process the message immediately with higher priority
                    await _messageHandler.HandleMessageAsync((JObject)message);

                    // Update the latest timestamp if this message is newer
                    string messageTs = message["ts"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(messageTs) &&
                        (string.IsNullOrEmpty(latestTimestamp) ||
                         string.Compare(messageTs, latestTimestamp) > 0))
                    {
                        latestTimestamp = messageTs;
                    }
                }

                // Update the timestamp to the latest message
                if (!string.IsNullOrEmpty(latestTimestamp) && latestTimestamp != _lastTimestamp)
                {
                    _lastTimestamp = latestTimestamp;
                    _logger.LogInformation("Updated last timestamp to {Timestamp}", _lastTimestamp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Slack messages");
            }
            finally
            {
                // Reset the flag to allow the next polling operation
                Interlocked.Exchange(ref _pollingInProgress, 0);
            }
        });
    }

    // Message processing logic moved to SlackMessageHandler



    public async Task SendMessage(string text)
    {
        await SendMessageInternal(text);
    }

    private async Task SendMessageInternal(string text)
    {
        // Slack has a 4000 character limit for messages
        if (text.Length > 4000)
        {
            _logger.LogWarning("Message truncated due to length: {Length} characters", text.Length);
            text = text[..3900] + "... (truncated)";
        }

        try
        {
            _logger.LogInformation("Sending message to Slack");

            var payload = new
            {
                channel = _config.Slack.ChannelId,
                text = text
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send message: HTTP {StatusCode}", response.StatusCode);
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(responseContent);

            if (data["ok"]?.Value<bool>() != true)
            {
                _logger.LogError("Failed to send message: {Error}", data["error"]);
            }
            else
            {
                // Store the message ID to avoid processing it later
                string messageId = data["ts"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(messageId))
                {
                    _sentMessageIds.TryAdd(messageId, 0);
                    _messageTimestamps.TryAdd(messageId, DateTime.UtcNow);
                    _logger.LogInformation("Stored sent message ID: {MessageId}", messageId);
                }
                _logger.LogInformation("Message sent successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Slack");
        }
    }

    private async Task JoinChannel()
    {
        try
        {
            // Create the payload to join the channel
            var payload = new
            {
                channel = _config.Slack.ChannelId
            };

            // Serialize to JSON
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            // Send to Slack API
            var response = await _httpClient.PostAsync("https://slack.com/api/conversations.join", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to join channel: HTTP {StatusCode}", response.StatusCode);
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(responseContent);

            if (data["ok"]?.Value<bool>() != true)
            {
                _logger.LogError("Failed to join channel: {Error}", data["error"]?.ToString());

                // If we can't join, send a message to the user about it
                await SendMessageInternal("I need to be invited to this channel. Please use `/invite @YourBotName` in the channel.");
            }
            else
            {
                _logger.LogInformation("Successfully joined channel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining channel");
        }
    }

    public async Task PostWeekLetter(string childName, string weekLetter, string weekLetterTitle)
    {
        if (string.IsNullOrEmpty(weekLetter))
        {
            _logger.LogWarning("Cannot post empty week letter for {ChildName}", childName);
            return;
        }

        // Compute a hash of the week letter to avoid posting duplicates
        string hash = ComputeHash(weekLetter);

        // Check if we've already posted this week letter
        if (_postedWeekLetterHashes.ContainsKey(hash))
        {
            _logger.LogInformation("Week letter for {ChildName} already posted, skipping", childName);
            return;
        }

        // Add the hash to our set
        _postedWeekLetterHashes.TryAdd(hash, 0);

        // Format the message with a title
        string message = $"*Ugeplan for {childName}: {weekLetterTitle}*\n\n{weekLetter}";

        try
        {
            _logger.LogInformation("Posting week letter for {ChildName}", childName);

            var payload = new
            {
                channel = _config.Slack.ChannelId,
                text = message,
                mrkdwn = true
            };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to post week letter: HTTP {StatusCode}", response.StatusCode);
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(responseContent);

            if (data["ok"]?.Value<bool>() != true)
            {
                _logger.LogError("Failed to post week letter: {Error}", data["error"]?.ToString());
            }
            else
            {
                // Store the message ID to avoid processing it later
                string messageId = data["ts"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(messageId))
                {
                    _sentMessageIds.TryAdd(messageId, 0);
                    _messageTimestamps.TryAdd(messageId, DateTime.UtcNow);
                    _logger.LogInformation("Stored week letter message ID: {MessageId}", messageId);
                }
                _logger.LogInformation("Week letter for {ChildName} posted successfully", childName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting week letter for {ChildName}", childName);
        }
    }

    private string ComputeHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }




    private void CleanupOldMessageIds(object? state)
    {
        try
        {
            // Keep message IDs for 24 hours to be safe
            var cutoff = DateTime.UtcNow.AddHours(-24);
            int removedCount = 0;

            // Find message IDs older than the cutoff
            var oldMessageIds = _messageTimestamps
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            // Remove them from both collections
            foreach (var messageId in oldMessageIds)
            {
                _sentMessageIds.TryRemove(messageId, out _);
                _messageTimestamps.TryRemove(messageId, out _);
                removedCount++;
            }

            // Also clean up the week letter hashes if there are too many
            if (_postedWeekLetterHashes.Count > 100)
            {
                // Since we can't easily determine which are oldest in a HashSet,
                // we'll just clear it if it gets too large
                _postedWeekLetterHashes.Clear();
                _logger.LogInformation("Cleared week letter hash cache");
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} old message IDs. Remaining: {Remaining}",
                    removedCount, _sentMessageIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old message IDs");
        }
    }



    public void Dispose()
    {
        _httpClient?.Dispose();
        _pollingTimer?.Dispose();
        _cleanupTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}