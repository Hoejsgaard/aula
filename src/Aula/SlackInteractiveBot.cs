using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

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

    public Task Start()
    {
        if (string.IsNullOrEmpty(_config.Slack.ApiToken))
        {
            _logger.LogError("Cannot start Slack bot: API token is missing");
            return Task.CompletedTask;
        }

        if (string.IsNullOrEmpty(_config.Slack.ChannelId))
        {
            _logger.LogError("Cannot start Slack bot: Channel ID is missing");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting Slack polling bot");
        
        // Configure the HTTP client for Slack API
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.Slack.ApiToken);
        
        // Start polling
        _isRunning = true;
        _pollingTimer = new Timer(PollMessages, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        _logger.LogInformation("Slack polling bot started");
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _isRunning = false;
        _pollingTimer?.Dispose();
        _logger.LogInformation("Slack polling bot stopped");
    }

    private async void PollMessages(object? state)
    {
        if (!_isRunning) return;
        
        // Use a lock to prevent overlapping polls
        if (!Monitor.TryEnter(_lockObject)) return;
        
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
                _logger.LogError("Failed to fetch messages: {Error}", data["error"]?.ToString());
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
                if (message["subtype"]?.ToString() == "bot_message")
                {
                    continue;
                }
                
                // Process the message
                await ProcessMessage(message["text"]?.ToString() ?? "");
                
                // Update the timestamp to the latest message
                _lastTimestamp = message["ts"]?.ToString() ?? _lastTimestamp;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling Slack messages");
        }
        finally
        {
            Monitor.Exit(_lockObject);
        }
    }

    private async Task ProcessMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _logger.LogInformation("Processing message: {Text}", text);

        // Check if this is a question about a child's activities
        if (await TryHandleChildQuestion(text))
        {
            return;
        }

        // Check if this is a request for a week letter
        if (await TryHandleWeekLetterRequest(text))
        {
            return;
        }

        // If we get here, we didn't understand the request
        await SendMessage("I'm not sure how to help with that. You can ask me about a child's activities like 'What is Søren doing tomorrow?' or 'Does Hans have homework for Tuesday?'");
    }

    private async Task<bool> TryHandleChildQuestion(string text)
    {
        // Try to identify which child is being asked about
        string? childName = null;
        foreach (var name in _childrenByName.Keys)
        {
            if (text.ToLowerInvariant().Contains(name))
            {
                childName = name;
                break;
            }
        }

        if (childName == null)
        {
            return false;
        }

        var child = _childrenByName[childName];
        
        // Check if it's a question about activities or homework
        var isActivityQuestion = Regex.IsMatch(text, @"(what|when).*(doing|activity|activities|schedule)", RegexOptions.IgnoreCase);
        var isHomeworkQuestion = text.ToLowerInvariant().Contains("homework");
        
        if (isActivityQuestion || isHomeworkQuestion)
        {
            // Try to identify the day
            DateOnly targetDate = DateOnly.FromDateTime(DateTime.Today);
            
            if (text.ToLowerInvariant().Contains("tomorrow"))
            {
                targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            }
            else if (text.ToLowerInvariant().Contains("today"))
            {
                targetDate = DateOnly.FromDateTime(DateTime.Today);
            }
            else
            {
                // Try to find a day of the week
                var daysOfWeek = new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
                for (int i = 0; i < daysOfWeek.Length; i++)
                {
                    if (text.ToLowerInvariant().Contains(daysOfWeek[i]))
                    {
                        var today = (int)DateTime.Today.DayOfWeek;
                        var targetDay = (i + 1) % 7; // Convert to DayOfWeek enum (0 = Sunday in .NET)
                        var daysToAdd = (7 + targetDay - today) % 7;
                        if (daysToAdd == 0) daysToAdd = 7; // If it's the same day, go to next week
                        
                        targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(daysToAdd));
                        break;
                    }
                }
            }

            // Formulate the question for the OpenAI service
            string question;
            if (isHomeworkQuestion)
            {
                question = $"What homework does {child.FirstName} have for {targetDate.ToString("dddd, MMMM d")}?";
            }
            else
            {
                question = $"What is {child.FirstName} doing on {targetDate.ToString("dddd, MMMM d")}?";
            }

            // Get the answer from OpenAI
            var answer = await _agentService.AskQuestionAboutWeekLetterAsync(child, targetDate, question);
            
            // Send the response
            await SendMessage($"*{question}*\n{answer}");
            return true;
        }

        return false;
    }

    private async Task<bool> TryHandleWeekLetterRequest(string text)
    {
        // Check if it's a request for a week letter
        if (Regex.IsMatch(text, @"(show|get|display).*(week letter|ugebrev)", RegexOptions.IgnoreCase))
        {
            // Try to identify which child
            string? childName = null;
            foreach (var name in _childrenByName.Keys)
            {
                if (text.ToLowerInvariant().Contains(name))
                {
                    childName = name;
                    break;
                }
            }

            if (childName == null && _childrenByName.Count == 1)
            {
                // If there's only one child and none specified, use that one
                childName = _childrenByName.Keys.First();
            }
            else if (childName == null)
            {
                await SendMessage("Which child's week letter would you like to see?");
                return true;
            }

            var child = _childrenByName[childName];
            
            // Get the week letter
            var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today));
            
            // Create a SlackBot instance to use its formatting
            var slackBot = new SlackBot(_config);
            await slackBot.PostWeekLetter(weekLetter, child);
            
            return true;
        }

        return false;
    }

    private async Task<bool> SendMessage(string text)
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
                Encoding.UTF8,
                "application/json");
            
            // Send to Slack API
            var response = await _httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send message to Slack: HTTP {StatusCode}", response.StatusCode);
                return false;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(responseContent);
            
            if (data["ok"]?.Value<bool>() != true)
            {
                _logger.LogError("Failed to send message to Slack: {Error}", data["error"]?.ToString());
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Slack");
            return false;
        }
    }
} 