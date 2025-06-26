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

namespace Aula;

public class SlackInteractiveBot
{
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, Child> _childrenByName;
    private bool _isRunning;
    private Timer? _pollingTimer;
    private string _lastTimestamp = "0"; // Start from the beginning of time
    private readonly object _lockObject = new object();
    private int _pollingInProgress = 0;
    private bool _recentlyRespondedToGenericQuestion = false;
    private readonly HashSet<string> _postedWeekLetterHashes = new HashSet<string>();
    private readonly HashSet<string> _englishWords = new HashSet<string> { "what", "when", "how", "is", "does", "do", "can", "will", "has", "have", "had", "show", "get", "tell", "please", "thanks", "thank", "you", "hello", "hi" };
    private readonly HashSet<string> _danishWords = new HashSet<string> { "hvad", "hvornår", "hvordan", "er", "gør", "kan", "vil", "har", "havde", "vis", "få", "fortæl", "venligst", "tak", "du", "dig", "hej", "hallo", "goddag" };
    
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

    public SlackInteractiveBot(
        IAgentService agentService,
        Config config,
        ILoggerFactory loggerFactory)
    {
        _agentService = agentService;
        _config = config;
        _logger = loggerFactory.CreateLogger<SlackInteractiveBot>();
        _httpClient = new HttpClient();
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
        _lastTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        
        // Start polling
        _isRunning = true;
        _pollingTimer = new Timer(PollMessages, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        // Build a list of available children (first names only)
        string childrenList = string.Join(" og ", _childrenByName.Values.Select(c => c.FirstName.Split(' ')[0]));
        
        // Get the current week number
        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);
        
        // Send welcome message in Danish with children info
        await SendMessage($"Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}");
        
        _logger.LogInformation("Slack polling bot started");
    }

    public void Stop()
    {
        _isRunning = false;
        _pollingTimer?.Dispose();
        _logger.LogInformation("Slack polling bot stopped");
    }

    private async void PollMessages(object? state)
    {
        // Don't use locks with async/await as it can lead to deadlocks
        // Instead, use a simple flag to prevent concurrent executions
        if (!_isRunning || Interlocked.Exchange(ref _pollingInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            // Build the API URL for conversations.history
            var url = $"https://slack.com/api/conversations.history?channel={_config.Slack.ChannelId}&oldest={_lastTimestamp}";
            
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
            
            _logger.LogInformation("Found {Count} new messages", messages.Count);
            
            // Process messages in chronological order (oldest first)
            foreach (var message in messages.OrderBy(m => m["ts"]?.ToString()))
            {
                // Skip bot messages
                if (message["subtype"]?.ToString() == "bot_message" || 
                    message["bot_id"] != null)
                {
                    continue;
                }
                
                // Process only messages from real users
                string user = message["user"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(user))
                {
                    // Process the message
                    await ProcessMessage(message["text"]?.ToString() ?? "");
                    
                    // Update the timestamp to the latest message
                    _lastTimestamp = message["ts"]?.ToString() ?? _lastTimestamp;
                }
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
    }

    private async Task ProcessMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _logger.LogInformation("Processing message: {Text}", text);
        
        // Log current conversation context if it's still valid
        if (_conversationContext.IsStillValid)
        {
            _logger.LogInformation("Current conversation context: {Context}", _conversationContext);
        }

        // Skip our own messages to prevent loops
        if (text.Contains("Jeg er online") || 
            text.Contains("I'm online") ||
            text.Contains("Jeg er ikke sikker") ||
            text.Contains("I'm not sure"))
        {
            _logger.LogInformation("Skipping our own message to prevent loops");
            return;
        }

        // Skip system messages and announcements that don't need a response
        if (text.Contains("has joined the channel") || 
            text.Contains("added an integration") ||
            text.Contains("added to the channel") ||
            text.StartsWith("<http"))
        {
            return;
        }

        // Detect language (Danish or English)
        bool isEnglish = DetectLanguage(text) == "en";
        
        // Handle questions about what day it is today
        if (text.ToLowerInvariant().Contains("hvilken dag er det i dag") || 
            text.ToLowerInvariant().Contains("what day is it today") ||
            text.ToLowerInvariant().Contains("hvilken dag er det") ||
            text.ToLowerInvariant().Contains("what day is it"))
        {
            await HandleDayQuestion(isEnglish);
            return;
        }
        
        // Check for follow-up questions like "what about X?" or "how about X?"
        bool isFollowUp = IsFollowUpQuestion(text);
        string? followUpChildName = null;
        
        if (isFollowUp)
        {
            followUpChildName = ExtractChildName(text);
            _logger.LogInformation("Detected follow-up question about child: {ChildName}", followUpChildName ?? "unknown");
            
            // If this is a follow-up with no child name and previous context was about a specific child,
            // assume it's about the same child
            if (followUpChildName == null && _conversationContext.IsStillValid && _conversationContext.LastChildName != null)
            {
                followUpChildName = _conversationContext.LastChildName;
                _logger.LogInformation("Using previous child from context: {ChildName}", followUpChildName);
            }
        }
        
        // First check if this is a question about a child - this should take priority
        if (await TryHandleChildQuestion(text, isEnglish, isFollowUp, followUpChildName))
        {
            _logger.LogInformation("Handled as child question");
            return;
        }

        // Then check if this is a request for a week letter
        if (await TryHandleWeekLetterRequest(text, isEnglish))
        {
            _logger.LogInformation("Handled as week letter request");
            return;
        }

        // Only send the default response for messages that look like questions or commands
        // and only if we haven't already sent a similar response recently
        if (text.Contains("?") || 
            text.StartsWith("vis") || text.StartsWith("få") || text.StartsWith("hvad") || 
            text.StartsWith("hvornår") || text.StartsWith("hvordan") ||
            text.StartsWith("show") || text.StartsWith("get") || text.StartsWith("what") || 
            text.StartsWith("when") || text.StartsWith("how"))
        {
            // Don't respond to our own help message
            if (!_recentlyRespondedToGenericQuestion)
            {
                _recentlyRespondedToGenericQuestion = true;
                
                if (isEnglish)
                {
                    await SendMessage("I'm not sure how to help with that. You can ask me about a child's activities like 'What is TestChild2 doing tomorrow?' or 'Does TestChild1 have homework for Tuesday?'");
                }
                else
                {
                    await SendMessage("Jeg er ikke sikker på, hvordan jeg kan hjælpe med det. Du kan spørge mig om et barns aktiviteter som 'Hvad skal TestChild2 lave i morgen?' eller 'Har TestChild1 lektier for til tirsdag?'");
                }
                
                // Reset the flag after a delay to allow responding again later
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => 
                {
                    _recentlyRespondedToGenericQuestion = false;
                    _logger.LogInformation("Reset generic question response flag");
                });
            }
            else
            {
                _logger.LogInformation("Suppressing duplicate generic response");
            }
        }
    }

    private bool IsFollowUpQuestion(string text)
    {
        if (!_conversationContext.IsStillValid)
        {
            return false;
        }
        
        text = text.ToLowerInvariant();
        
        // Check for phrases that indicate follow-up questions
        bool hasFollowUpPhrase = text.Contains("what about") || 
                               text.Contains("how about") || 
                               text.Contains("hvad med") || 
                               text.Contains("hvordan med") ||
                               text.Contains("og hvad med") ||
                               text.Contains("and what about") ||
                               text.Contains("tak") ||
                               text.Contains("thanks") ||
                               text.StartsWith("og") ||
                               text.StartsWith("and");
                               
        // If it's a very short message, it's likely a follow-up
        bool isShortMessage = text.Split(' ').Length <= 5;
        
        // If it contains a child name but doesn't have time references, it might be a follow-up
        bool hasChildName = ExtractChildName(text) != null;
        bool hasTimeReference = text.Contains("today") || text.Contains("tomorrow") || 
                              text.Contains("i dag") || text.Contains("i morgen");
                              
        // Special case for very short messages that are likely follow-ups
        if (isShortMessage && (text.Contains("?") || text == "ok" || text == "okay"))
        {
            _logger.LogInformation("Detected likely follow-up based on short message: {Text}", text);
            return true;
        }
        
        bool result = hasFollowUpPhrase || (isShortMessage && hasChildName && !hasTimeReference);
        
        if (result)
        {
            _logger.LogInformation("Detected follow-up question: {Text}", text);
        }
        
        return result;
    }

    private string DetectLanguage(string text)
    {
        // Simple language detection based on word frequency
        text = text.ToLowerInvariant();
        
        // Count English and Danish words
        int englishCount = 0;
        int danishCount = 0;
        
        // Split text into words and count matches
        var words = Regex.Split(text, @"\W+").Where(w => !string.IsNullOrEmpty(w));
        foreach (var word in words)
        {
            if (_englishWords.Contains(word))
                englishCount++;
            if (_danishWords.Contains(word))
                danishCount++;
        }
        
        // Default to Danish unless clearly English
        return englishCount > danishCount ? "en" : "da";
    }

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
                // Use word boundary regex to avoid partial matches
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
                {
                    return childName;
                }
            }
            
            // If no new child name is found, this might be a generic follow-up about all children
            return null;
        }
        
        // Check for each child name with word boundary matching
        foreach (var childName in _childrenByName.Keys)
        {
            // Use word boundary regex to avoid partial matches
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
            {
                return childName;
            }
        }
        
        // If no child name is found in the text but we have a valid context,
        // this might be a follow-up question about the same child
        if (_conversationContext.IsStillValid && _conversationContext.LastChildName != null)
        {
            // Check if the question is a clear follow-up about the same child
            if (text.Contains("again") || text.Contains("also") || 
                text.Contains("more") || text.Contains("else") ||
                text.Contains("igen") || text.Contains("også") || 
                text.Contains("mere") || text.Contains("andet"))
            {
                return _conversationContext.LastChildName;
            }
        }
        
        return null;
    }

    private async Task<bool> TryHandleChildQuestion(string text, bool isEnglish = false, bool isFollowUp = false, string? followUpChildName = null)
    {
        // Extract child name from the question
        string? childName = followUpChildName ?? ExtractChildName(text);
        
        // For follow-up questions, use context from the previous question
        bool isAboutToday = text.ToLowerInvariant().Contains("i dag") || 
                          text.ToLowerInvariant().Contains("today") ||
                          (isFollowUp && _conversationContext.WasAboutToday);
                          
        bool isAboutTomorrow = text.ToLowerInvariant().Contains("i morgen") || 
                             text.ToLowerInvariant().Contains("tomorrow") ||
                             (isFollowUp && _conversationContext.WasAboutTomorrow);
                             
        bool isAboutHomework = text.ToLowerInvariant().Contains("homework") || 
                             text.ToLowerInvariant().Contains("lektier") ||
                             (isFollowUp && _conversationContext.WasAboutHomework);
        
        // If no child name was found but there's only one child, use that one
        if (string.IsNullOrEmpty(childName) && _childrenByName.Count == 1)
        {
            childName = _childrenByName.Keys.First();
            _logger.LogInformation("No child name in question, but only one child available: {ChildName}", childName);
        }
        // If no child name was found and there are multiple children, try to infer from context
        else if (string.IsNullOrEmpty(childName) && _childrenByName.Count > 1)
        {
            // For follow-up questions without a child name, try to answer for all children
            if (isFollowUp || isAboutToday || isAboutTomorrow)
            {
                // Try to answer for all children
                await HandleMultiChildDayQuestion(text, isEnglish, isAboutToday || !isAboutTomorrow);
                return true;
            }
            
            _logger.LogInformation("No child name found in message and multiple children available: {Text}", text);
            return false;
        }

        // If we still don't have a child name, we can't proceed
        if (string.IsNullOrEmpty(childName))
        {
            _logger.LogInformation("No valid child name found in the message");
            return false;
        }

        _logger.LogInformation("Processing question about child: {ChildName}", childName);

        // Find the child by name
        if (!_childrenByName.TryGetValue(childName, out var child))
        {
            string notFoundMessage = isEnglish
                ? $"I don't know a child named {childName}. Available children are: {string.Join(", ", _childrenByName.Keys)}"
                : $"Jeg kender ikke et barn ved navn {childName}. Tilgængelige børn er: {string.Join(", ", _childrenByName.Keys)}";
            
            await SendMessage(notFoundMessage);
            return true;
        }

        try
        {
            // Get the week letter for the child
            var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
            if (weekLetter == null || 
                !weekLetter.ContainsKey("ugebreve") || 
                weekLetter["ugebreve"] == null || 
                !(weekLetter["ugebreve"] is JArray ugebreve) || 
                ugebreve.Count == 0)
            {
                string noLetterMessage = isEnglish
                    ? $"I don't have a week letter for {childName} yet."
                    : $"Jeg har ikke et ugebrev for {childName} endnu.";
                
                await SendMessage(noLetterMessage);
                return true;
            }

            // Extract metadata from the week letter
            var className = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
            var weekNumber = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
            var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
            
            _logger.LogInformation("Found week letter for {ChildName}, class {ClassName}, week {WeekNumber}", 
                child.FirstName, className, weekNumber);

            // Formulate the question for the OpenAI service with more context and day information
            string question;
            
            // Get the current day name in Danish and English
            string currentDayDanish = GetDanishDayName(DateTime.Now.DayOfWeek);
            string currentDayEnglish = DateTime.Now.DayOfWeek.ToString();
            
            // Get tomorrow's day name
            string tomorrowDayDanish = GetDanishDayName(DateTime.Now.AddDays(1).DayOfWeek);
            string tomorrowDayEnglish = DateTime.Now.AddDays(1).DayOfWeek.ToString();
            
            if (isAboutHomework)
            {
                question = isEnglish
                    ? $"What homework does {child.FirstName} (class {className}) have for week {weekNumber}? Today is {currentDayEnglish}."
                    : $"Hvilke lektier har {child.FirstName} (klasse {className}) for uge {weekNumber}? I dag er det {currentDayDanish}.";
            }
            else if (isAboutTomorrow)
            {
                question = isEnglish
                    ? $"What is {child.FirstName} (class {className}) doing tomorrow ({tomorrowDayEnglish}) according to the week letter for week {weekNumber}? Today is {currentDayEnglish}."
                    : $"Hvad skal {child.FirstName} (klasse {className}) lave i morgen ({tomorrowDayDanish}) ifølge ugebrevet for uge {weekNumber}? I dag er det {currentDayDanish}.";
            }
            else if (isAboutToday)
            {
                question = isEnglish
                    ? $"What is {child.FirstName} (class {className}) doing today ({currentDayEnglish}) according to the week letter for week {weekNumber}?"
                    : $"Hvad skal {child.FirstName} (klasse {className}) lave i dag ({currentDayDanish}) ifølge ugebrevet for uge {weekNumber}?";
            }
            else
            {
                question = isEnglish
                    ? $"What is {child.FirstName} (class {className}) doing this week according to the week letter for week {weekNumber}? Today is {currentDayEnglish}."
                    : $"Hvad skal {child.FirstName} (klasse {className}) lave denne uge ifølge ugebrevet for uge {weekNumber}? I dag er det {currentDayDanish}.";
            }

            _logger.LogInformation("Asking OpenAI: {Question}", question);

            // Ask OpenAI about the child's activities
            string answer = await _agentService.AskQuestionAboutWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), question);
            
            _logger.LogInformation("Got answer from OpenAI: {Answer}", answer);
            await SendMessage(answer);
            
            // Update conversation context
            UpdateConversationContext(childName, isAboutToday, isAboutTomorrow, isAboutHomework);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question about child {ChildName}", childName);
            string errorMessage = isEnglish
                ? $"Sorry, I couldn't process information about {childName} at the moment."
                : $"Beklager, jeg kunne ikke behandle information om {childName} i øjeblikket.";
            
            await SendMessage(errorMessage);
            return true;
        }
    }

    private async Task<bool> TryHandleWeekLetterRequest(string text, bool isEnglish = false)
    {
        // Check if this is a request for a week letter
        var match = Regex.Match(text, @"(vis|show|få|get) (ugebrev|week letter) (?:for|til) (\w+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        string childName = match.Groups[3].Value;
        
        // Find the child by name
        if (!_childrenByName.TryGetValue(childName.ToLowerInvariant(), out var child))
        {
            string notFoundMessage = isEnglish
                ? $"I don't know a child named {childName}. Available children are: {string.Join(", ", _childrenByName.Keys)}"
                : $"Jeg kender ikke et barn ved navn {childName}. Tilgængelige børn er: {string.Join(", ", _childrenByName.Keys)}";
            
            await SendMessage(notFoundMessage);
            return true;
        }

        try
        {
            // Get the week letter for the child
            var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
            
            // Extract the content and post it
            var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
            var weekLetterTitle = $"Uge {weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? ""} - {weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? ""}";
            
            // Convert HTML to markdown
            var html2MarkdownConverter = new Html2SlackMarkdownConverter();
            var markdownContent = html2MarkdownConverter.Convert(weekLetterContent).Replace("**", "*");
            
            await PostWeekLetter(child.FirstName, markdownContent, weekLetterTitle);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting week letter for child {ChildName}", childName);
            string errorMessage = isEnglish
                ? $"Sorry, I couldn't retrieve the week letter for {childName} at the moment."
                : $"Beklager, jeg kunne ikke hente ugebrevet for {childName} i øjeblikket.";
            
            await SendMessage(errorMessage);
            return true;
        }
    }

    private async Task<JObject> GetConversationHistory()
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/conversations.history");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Slack.ApiToken);
        
        var parameters = new Dictionary<string, string>
        {
            { "channel", _config.Slack.ChannelId },
            { "limit", "50" }
        };
        
        if (!string.IsNullOrEmpty(_lastTimestamp) && _lastTimestamp != "0")
        {
            parameters.Add("oldest", _lastTimestamp);
        }
        
        var content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content = content;
        
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var responseContent = await response.Content.ReadAsStringAsync();
        return JObject.Parse(responseContent);
    }

    private async Task SendMessage(string text)
    {
        try
        {
            // Create the message payload
            var payload = new
            {
                channel = _config.Slack.ChannelId,
                text = text,
                mrkdwn = true
            };
            
            // Serialize to JSON
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            // Send to Slack API
            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send message to Slack: HTTP {StatusCode}", response.StatusCode);
                return;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(responseContent);
            
            if (data["ok"]?.Value<bool>() != true)
            {
                _logger.LogError("Failed to send message to Slack: {Error}", data["error"]?.ToString());
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
                await SendMessage("I need to be invited to this channel. Please use `/invite @YourBotName` in the channel.");
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
            _logger.LogInformation("Empty week letter for {ChildName}, not posting", childName);
            return;
        }

        // Create a hash of the week letter content to check for duplicates
        string contentHash = ComputeHash(childName + weekLetterTitle + weekLetter);
        
        // Check if we've already posted this exact week letter
        if (_postedWeekLetterHashes.Contains(contentHash))
        {
            _logger.LogInformation("Week letter for {ChildName} with title {Title} already posted, skipping", childName, weekLetterTitle);
            return;
        }

        _logger.LogInformation("Posting week letter for {ChildName} with title {Title}", childName, weekLetterTitle);
        string message = $"*Week letter for {childName}*\n*{weekLetterTitle}*\n\n{weekLetter}";
        
        await SendMessage(message);
        
        // Remember that we've posted this week letter
        _postedWeekLetterHashes.Add(contentHash);
        _logger.LogInformation("Added hash for week letter: {ChildName} - {Title}", childName, weekLetterTitle);
        
        // Limit the size of the hash set to avoid memory issues over time
        if (_postedWeekLetterHashes.Count > 100)
        {
            // Remove oldest entries (approximation since HashSet doesn't maintain order)
            int toRemove = _postedWeekLetterHashes.Count - 100;
            _postedWeekLetterHashes.Take(toRemove).ToList().ForEach(hash => _postedWeekLetterHashes.Remove(hash));
        }
    }
    
    private string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private async Task HandleDayQuestion(bool isEnglish)
    {
        // Get the current day of the week in Danish or English
        string dayName = DateTime.Now.DayOfWeek.ToString();
        string danishDayName = GetDanishDayName(DateTime.Now.DayOfWeek);
        
        if (isEnglish)
        {
            await SendMessage($"Today is {dayName}, {DateTime.Now:MMMM d, yyyy}.");
        }
        else
        {
            await SendMessage($"I dag er det {danishDayName}, {DateTime.Now:d. MMMM yyyy}.");
        }
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

    private async Task HandleMultiChildDayQuestion(string text, bool isEnglish, bool isAboutToday)
    {
        try
        {
            // Get the current day name
            string currentDayDanish = GetDanishDayName(DateTime.Now.DayOfWeek);
            string currentDayEnglish = DateTime.Now.DayOfWeek.ToString();
            
            // Get tomorrow's day name
            string tomorrowDayDanish = GetDanishDayName(DateTime.Now.AddDays(1).DayOfWeek);
            string tomorrowDayEnglish = DateTime.Now.AddDays(1).DayOfWeek.ToString();
            
            // Determine if this is a follow-up question
            bool isFollowUp = IsFollowUpQuestion(text);
            bool isAboutTomorrow = !isAboutToday;
            
            // If it's a follow-up question with no explicit time reference, use context
            if (isFollowUp && !text.ToLowerInvariant().Contains("i dag") && 
                !text.ToLowerInvariant().Contains("today") &&
                !text.ToLowerInvariant().Contains("i morgen") && 
                !text.ToLowerInvariant().Contains("tomorrow"))
            {
                isAboutToday = _conversationContext.WasAboutToday;
                isAboutTomorrow = _conversationContext.WasAboutTomorrow;
            }
            
            StringBuilder responseBuilder = new StringBuilder();
            
            if (isEnglish)
            {
                responseBuilder.AppendLine(isAboutToday 
                    ? $"Here's what all children are doing today ({currentDayEnglish}):"
                    : $"Here's what all children are doing tomorrow ({tomorrowDayEnglish}):");
            }
            else
            {
                responseBuilder.AppendLine(isAboutToday 
                    ? $"Her er hvad alle børn laver i dag ({currentDayDanish}):"
                    : $"Her er hvad alle børn laver i morgen ({tomorrowDayDanish}):");
            }
            
            foreach (var childEntry in _childrenByName)
            {
                string childName = childEntry.Key;
                Child child = childEntry.Value;
                
                // Get the week letter for the child
                var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                if (weekLetter == null || 
                    !weekLetter.ContainsKey("ugebreve") || 
                    weekLetter["ugebreve"] == null || 
                    !(weekLetter["ugebreve"] is JArray ugebreve) || 
                    ugebreve.Count == 0)
                {
                    string noLetterMessage = isEnglish
                        ? $"- {child.FirstName}: No week letter available."
                        : $"- {child.FirstName}: Intet ugebrev tilgængeligt.";
                    
                    responseBuilder.AppendLine(noLetterMessage);
                    continue;
                }

                // Extract metadata from the week letter
                var className = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
                var weekNumber = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
                
                // Formulate the question for the OpenAI service
                string question;
                
                if (isAboutToday)
                {
                    question = isEnglish
                        ? $"What is {child.FirstName} (class {className}) doing today ({currentDayEnglish}) according to the week letter for week {weekNumber}? Give a brief answer."
                        : $"Hvad skal {child.FirstName} (klasse {className}) lave i dag ({currentDayDanish}) ifølge ugebrevet for uge {weekNumber}? Giv et kort svar.";
                }
                else
                {
                    question = isEnglish
                        ? $"What is {child.FirstName} (class {className}) doing tomorrow ({tomorrowDayEnglish}) according to the week letter for week {weekNumber}? Today is {currentDayEnglish}. Give a brief answer."
                        : $"Hvad skal {child.FirstName} (klasse {className}) lave i morgen ({tomorrowDayDanish}) ifølge ugebrevet for uge {weekNumber}? I dag er det {currentDayDanish}. Giv et kort svar.";
                }

                // Ask OpenAI about the child's activities
                string answer = await _agentService.AskQuestionAboutWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), question);
                
                // Format the answer
                responseBuilder.AppendLine($"- {child.FirstName}: {answer.Trim()}");
            }
            
            // Update conversation context
            UpdateConversationContext(null, isAboutToday, isAboutTomorrow, false);
            
            await SendMessage(responseBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling multi-child day question");
            string errorMessage = isEnglish
                ? "Sorry, I couldn't retrieve information about the children's activities at the moment."
                : "Beklager, jeg kunne ikke hente information om børnenes aktiviteter i øjeblikket.";
            
            await SendMessage(errorMessage);
        }
    }
} 