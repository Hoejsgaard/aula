using System;
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
    private readonly HashSet<string> _postedWeekLetterHashes = new HashSet<string>();
    // Track our own message IDs to avoid processing them
    private readonly HashSet<string> _sentMessageIds = new HashSet<string>();
    // Keep track of when messages were sent to allow cleanup
    private readonly Dictionary<string, DateTime> _messageTimestamps = new Dictionary<string, DateTime>();
    // Language detection arrays removed - GPT handles language detection naturally

    // Conversation context tracking
    private class ConversationContext
    {
        public string? LastChildName { get; set; }
        public bool WasAboutToday { get; set; }
        public bool WasAboutTomorrow { get; set; }
        public bool WasAboutHomework { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsStillValid => (DateTime.Now - Timestamp).TotalMinutes < 10; // Context expires after 10 minutes

        public override string ToString()
        {
            return $"Child: {LastChildName ?? "none"}, Today: {WasAboutToday}, Tomorrow: {WasAboutTomorrow}, Homework: {WasAboutHomework}, Age: {(DateTime.Now - Timestamp).TotalMinutes:F1} minutes";
        }
    }

    private ConversationContext _conversationContext = new ConversationContext();

    private void UpdateConversationContext(string? childName, bool isAboutToday, bool isAboutTomorrow, bool isAboutHomework)
    {
        _conversationContext = new ConversationContext
        {
            LastChildName = childName,
            WasAboutToday = isAboutToday,
            WasAboutTomorrow = isAboutTomorrow,
            WasAboutHomework = isAboutHomework,
            Timestamp = DateTime.Now
        };

        _logger.LogInformation("Updated conversation context: {Context}", _conversationContext);
    }

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
        string childrenList = string.Join(" og ", _childrenByName.Values.Select(c => c.FirstName.Split(' ')[0]));

        // Get the current week number
        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);

        // Send welcome message in Danish with children info
        await SendMessageInternal($"Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}");

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
                    if (!string.IsNullOrEmpty(messageId) && _sentMessageIds.Contains(messageId))
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
                    await ProcessMessage(message["text"]?.ToString() ?? "");

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

    private async Task ProcessMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _logger.LogInformation("Processing message: {Text}", text);

        // Skip system messages and announcements that don't need a response
        if (text.Contains("has joined the channel") ||
            text.Contains("added an integration") ||
            text.Contains("added to the channel") ||
            text.StartsWith("<http"))
        {
            _logger.LogInformation("Skipping system message or announcement");
            return;
        }

        // Check for help command first
        if (await TryHandleHelpCommand(text))
        {
            return;
        }

        // Use the new tool-based processing that can handle both tools and regular questions
        string contextKey = $"slack-{_config.Slack.ChannelId}";
        string response = await _agentService.ProcessQueryWithToolsAsync(text, contextKey, ChatInterface.Slack);
        await SendMessageInternal(response);
    }

    // IsFollowUpQuestion method removed - dead code

    // DetectLanguage method removed - GPT handles language detection naturally

    private string? ExtractChildName(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text = text.ToLowerInvariant();

        // For very short follow-up questions with no clear child name, use the last child from context
        if (text.Length < 15 &&
            (_conversationContext.IsStillValid && _conversationContext.LastChildName != null) &&
            (text.Contains("what about") || text.Contains("how about") ||
             text.Contains("hvad med") || text.Contains("hvordan med") ||
             text.StartsWith("og") || text.StartsWith("and")))
        {
            // Try to extract a different child name from the follow-up
            foreach (var childName in _childrenByName.Keys)
            {
                // Use word boundary to avoid partial matches
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
                {
                    _logger.LogInformation("Found child name in follow-up: {ChildName}", childName);
                    return childName;
                }
            }

            // Check for first names only in follow-up questions
            foreach (var child in _childrenByName.Values)
            {
                string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
                // Use word boundary to avoid partial matches
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
                {
                    string matchedKey = _childrenByName.Keys.FirstOrDefault(k =>
                        k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase)) ?? "";
                    _logger.LogInformation("Found first name in follow-up: {FirstName} -> {ChildName}", firstName, matchedKey);
                    return matchedKey;
                }
            }

            // If no child name found in the follow-up, use the last child from context
            _logger.LogInformation("No child name in follow-up, using context: {ChildName}", _conversationContext.LastChildName);
            return _conversationContext.LastChildName;
        }

        // Check for "og hvad med X" or "and what about X" patterns
        string[] followUpPhrases = { "hvad med", "what about", "how about", "hvordan med", "og hvad", "and what" };
        foreach (var phrase in followUpPhrases)
        {
            int index = text.IndexOf(phrase);
            if (index >= 0)
            {
                string afterPhrase = text.Substring(index + phrase.Length).Trim();
                _logger.LogInformation("Follow-up phrase detected: '{Phrase}', text after: '{AfterPhrase}'", phrase, afterPhrase);

                // First check for full names
                foreach (var childName in _childrenByName.Keys)
                {
                    if (Regex.IsMatch(afterPhrase, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
                    {
                        _logger.LogInformation("Found child name after follow-up phrase: {ChildName}", childName);
                        return childName;
                    }
                }

                // Then check for first names
                foreach (var child in _childrenByName.Values)
                {
                    string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
                    if (Regex.IsMatch(afterPhrase, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
                    {
                        string matchedKey = _childrenByName.Keys.FirstOrDefault(k =>
                            k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase)) ?? "";
                        _logger.LogInformation("Found first name after follow-up phrase: {FirstName} -> {ChildName}", firstName, matchedKey);
                        return matchedKey;
                    }
                }
            }
        }

        // Standard child name extraction - check for each child name in the text
        foreach (var childName in _childrenByName.Keys)
        {
            // Use word boundary to avoid partial matches
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
            {
                _logger.LogInformation("Found full child name in text: {ChildName}", childName);
                return childName;
            }
        }

        // Check for first names only
        foreach (var child in _childrenByName.Values)
        {
            string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
            // Use word boundary to avoid partial matches
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
            {
                string matchedKey = _childrenByName.Keys.FirstOrDefault(k =>
                    k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase)) ?? "";
                _logger.LogInformation("Found first name in text: {FirstName} -> {ChildName}", firstName, matchedKey);
                return matchedKey;
            }
        }

        _logger.LogInformation("No child name found in text");
        return null;
    }



    public async Task SendMessage(string text)
    {
        await SendMessageInternal(text);
    }

    private async Task SendMessageInternal(string text)
    {
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
                    _sentMessageIds.Add(messageId);
                    _messageTimestamps[messageId] = DateTime.UtcNow;
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
        if (_postedWeekLetterHashes.Contains(hash))
        {
            _logger.LogInformation("Week letter for {ChildName} already posted, skipping", childName);
            return;
        }

        // Add the hash to our set
        _postedWeekLetterHashes.Add(hash);

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
                    _sentMessageIds.Add(messageId);
                    _messageTimestamps[messageId] = DateTime.UtcNow;
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
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string GetDanishDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "mandag",
            DayOfWeek.Tuesday => "tirsdag",
            DayOfWeek.Wednesday => "onsdag",
            DayOfWeek.Thursday => "torsdag",
            DayOfWeek.Friday => "fredag",
            DayOfWeek.Saturday => "lørdag",
            DayOfWeek.Sunday => "søndag",
            _ => "ukendt dag"
        };
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
                _sentMessageIds.Remove(messageId);
                _messageTimestamps.Remove(messageId);
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

    // Helper method to detect if a question contains relative time references
    private bool ContainsRelativeTimeReference(string text)
    {
        string lowerText = text.ToLowerInvariant();

        // Check for common relative time words in Danish and English
        string[] relativeTimeWords = new[]
        {
            "tomorrow", "yesterday", "today", "next week", "last week", "tonight", "this morning",
            "i morgen", "i går", "i dag", "næste uge", "sidste uge", "i aften", "i morges"
        };

        return relativeTimeWords.Any(word => lowerText.Contains(word));
    }

    private async Task<bool> TryHandleHelpCommand(string text)
    {
        var normalizedText = text.Trim().ToLowerInvariant();

        // English help commands
        if (normalizedText == "help" || normalizedText == "--help" || normalizedText == "?" || normalizedText == "commands")
        {
            await SendMessageInternal(GetEnglishHelpMessage());
            return true;
        }

        // Danish help commands  
        if (normalizedText == "hjælp" || normalizedText == "kommandoer")
        {
            await SendMessageInternal(GetDanishHelpMessage());
            return true;
        }

        return false;
    }

    private string GetEnglishHelpMessage()
    {
        return """
📚 *AulaBot Commands & Usage*

*🤖 Interactive Questions:*
Ask me anything about your children's school activities in natural language:
• "What does Søren have today?"
• "Does Hans have homework tomorrow?"
• "What activities are planned this week?"

*⏰ Reminder Commands:*
• `remind me tomorrow at 8:00 that Hans has Haver til maver`
• `remind me 25/12 at 7:30 that Christmas breakfast`
• `list reminders` - Show all reminders
• `delete reminder 1` - Delete reminder with ID 1

*📅 Automatic Features:*
• Weekly letters posted every Sunday at 16:00
• Morning reminders sent when scheduled
• Retry logic for missing content

*💬 Language Support:*
Ask questions in English or Danish - I'll respond in the same language!

*ℹ️ Tips:*
• Use "today", "tomorrow", or specific dates
• Mention child names for targeted questions
• Follow-up questions maintain context for 10 minutes
""";
    }

    private string GetDanishHelpMessage()
    {
        return """
📚 *AulaBot Kommandoer & Brug*

*🤖 Interaktive Spørgsmål:*
Spørg mig om hvad som helst vedrørende dine børns skoleaktiviteter på naturligt sprog:
• "Hvad skal Søren i dag?"
• "Har Hans lektier i morgen?"
• "Hvilke aktiviteter er planlagt denne uge?"

*⏰ Påmindelseskommandoer:*
• `husk mig i morgen kl 8:00 at Hans har Haver til maver`
• `husk mig 25/12 kl 7:30 at julefrokost`
• `vis påmindelser` - Vis alle påmindelser
• `slet påmindelse 1` - Slet påmindelse med ID 1

*📅 Automatiske Funktioner:*
• Ugebreve postes hver søndag kl. 16:00
• Morgenpåmindelser sendes når planlagt
• Genforøgelseslogik for manglende indhold

*💬 Sprogunderstøttelse:*
Stil spørgsmål på engelsk eller dansk - jeg svarer på samme sprog!

*ℹ️ Tips:*
• Brug "i dag", "i morgen", eller specifikke datoer
• Nævn børnenes navne for målrettede spørgsmål
• Opfølgningsspørgsmål bevarer kontekst i 10 minutter
""";
    }

    // Reminder functionality removed - dead code eliminated



    public void Dispose()
    {
        _httpClient?.Dispose();
        _pollingTimer?.Dispose();
        _cleanupTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}