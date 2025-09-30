using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.Managers;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Aula.Tools;
using Aula.Configuration;
using Aula.Utilities;

namespace Aula.Services;

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIService _openAiClient;
    private readonly ILogger _logger;
    private readonly IAiToolsManager _aiToolsManager;
    private readonly IConversationManager _conversationManager;
    private readonly IPromptBuilder _promptBuilder;
    private readonly string _aiModel;

    // Constants
    private const int MaxConversationHistoryWeekLetter = 20;
    private const int ConversationTrimAmount = 4;
    private const string FallbackToExistingSystem = "FALLBACK_TO_EXISTING_SYSTEM";

    public OpenAiService(string apiKey, ILoggerFactory loggerFactory, IAiToolsManager aiToolsManager, IConversationManager conversationManager, IPromptBuilder promptBuilder, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(aiToolsManager);
        ArgumentNullException.ThrowIfNull(conversationManager);
        ArgumentNullException.ThrowIfNull(promptBuilder);

        _aiModel = model ?? Models.Gpt_4;
        _openAiClient = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = apiKey
        });
        _logger = loggerFactory.CreateLogger(nameof(OpenAiService));
        _aiToolsManager = aiToolsManager;
        _conversationManager = conversationManager;
        _promptBuilder = promptBuilder;
    }

    // Legacy internal constructor removed

    private static (string childName, string className, string weekNumber) ExtractWeekLetterMetadata(JObject weekLetter)
    {
        string childName = "unknown";
        string className = "unknown";
        string weekNumber = "unknown";

        // Try to get metadata from the ugebreve array
        if (weekLetter["ugebreve"] != null && weekLetter["ugebreve"] is JArray ugebreve && ugebreve.Count > 0)
        {
            className = ugebreve[0]?["klasseNavn"]?.ToString() ?? "unknown";
            weekNumber = ugebreve[0]?["uge"]?.ToString() ?? "unknown";
        }

        // Fallback to old format if needed
        if (weekLetter["child"] != null) childName = weekLetter["child"]?.ToString() ?? "unknown";

        return (childName, className, weekNumber);
    }

    public async Task<string> SummarizeWeekLetterAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Summarizing week letter for {ChatInterface}", chatInterface);

        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        var (childName, className, weekNumber) = ExtractWeekLetterMetadata(weekLetter);

        var messages = _promptBuilder.CreateSummarizationMessages(childName, className, weekNumber, weekLetterContent, chatInterface);

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = _aiModel,
            Temperature = 0.7f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            return response.Choices.First().Message.Content ?? "No response content received.";
        }
        else
        {
            _logger.LogError("Error calling OpenAI API: {Error}", response.Error?.Message);
            return "Sorry, I couldn't summarize the week letter at this time.";
        }
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, ChatInterface chatInterface = ChatInterface.Slack)
    {
        return await AskQuestionAboutWeekLetterAsync(weekLetter, question, null, chatInterface);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        string childName = weekLetter["child"]?.ToString() ?? "unknown";
        contextKey = _conversationManager.EnsureContextKey(contextKey, childName);

        _conversationManager.EnsureConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        _conversationManager.AddUserQuestionToHistory(contextKey, question);
        _conversationManager.TrimConversationHistoryIfNeeded(contextKey);
        LogConversationHistoryStructure(contextKey);

        return await SendChatRequestAndGetResponse(contextKey);
    }


    private void LogConversationHistoryStructure(string contextKey)
    {
        var messages = _conversationManager.GetConversationHistory(contextKey);
        _logger.LogInformation("🔎 TRACE: Conversation history structure:");
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            _logger.LogInformation("🔎 TRACE: Message {Index}: Role={Role}, Content Length={Length}",
                i, message.Role, message.Content?.Length ?? 0);

            if (message.Content != null && message.Content.Length > 0)
            {
                var preview = message.Content.Length > 100 ? message.Content.Substring(0, 100) + "..." : message.Content;
                _logger.LogInformation("🔎 TRACE: Message {Index} Preview: {Preview}", i, preview);
            }
        }
    }

    private async Task<string> SendChatRequestAndGetResponse(string contextKey)
    {
        var messages = _conversationManager.GetConversationHistory(contextKey);
        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = _aiModel,
            Temperature = 0.7f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            var reply = response.Choices.First().Message.Content ?? "No response content received.";
            _conversationManager.AddAssistantResponseToHistory(contextKey, reply);
            return reply;
        }
        else
        {
            _logger.LogError("Error calling OpenAI API: {Error}", response.Error?.Message);
            return "Sorry, I couldn't answer your question at this time.";
        }
    }

    public async Task<JObject> ExtractKeyInformationAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Extracting key information from week letter for {ChatInterface}", chatInterface);

        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        var (childName, className, weekNumber) = ExtractWeekLetterMetadata(weekLetter);

        var messages = _promptBuilder.CreateKeyInformationExtractionMessages(childName, className, weekNumber, weekLetterContent);

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = _aiModel,
            Temperature = 0.3f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            var jsonResponse = response.Choices.First().Message.Content;
            try
            {
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    return new JObject();
                }
                return JObject.Parse(jsonResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON response from OpenAI");
                return new JObject();
            }
        }
        else
        {
            _logger.LogError("Error calling OpenAI API: {Error}", response.Error?.Message);
            return new JObject();
        }
    }

    private string ExtractWeekLetterContent(JObject weekLetter)
    {
        return WeekLetterContentExtractor.ExtractContent(weekLetter, _logger);
    }

    public async Task<string> AskQuestionAboutChildrenAsync(Dictionary<string, JObject> childrenWeekLetters, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Asking question about multiple children: {Question} for {ChatInterface}", question, chatInterface);

        // Extract content for each child
        var childrenContent = new Dictionary<string, string>();
        foreach (var (childName, weekLetter) in childrenWeekLetters)
        {
            childrenContent[childName] = ExtractWeekLetterContent(weekLetter);
        }

        var contextKeyToUse = contextKey ?? "combined-children";

        var baseMessages = _promptBuilder.CreateMultiChildMessages(childrenContent, chatInterface);
        var existingHistory = _conversationManager.GetConversationHistory(contextKeyToUse);

        var messages = new List<ChatMessage>(baseMessages);
        messages.AddRange(existingHistory);
        messages.Add(ChatMessage.FromUser(question));

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = _aiModel,
            Temperature = 0.3f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            var answer = response.Choices.First().Message.Content ?? "I couldn't generate a response.";

            _conversationManager.AddUserQuestionToHistory(contextKeyToUse, question);
            _conversationManager.AddAssistantResponseToHistory(contextKeyToUse, answer);
            _conversationManager.TrimMultiChildConversationIfNeeded(contextKeyToUse);

            return answer;
        }
        else
        {
            _logger.LogError("Error calling OpenAI API: {Error}", response.Error?.Message);
            return "I'm sorry, I couldn't process your question at the moment.";
        }
    }

    public void ClearConversationHistory(string? contextKey = null)
    {
        _conversationManager.ClearConversationHistory(contextKey);
    }

    public async Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        try
        {
            _logger.LogInformation("Processing query with intelligent tool selection: {Query}", query);

            // First, let the LLM analyze the intent and decide what to do
            var analysisResponse = await AnalyzeUserIntentAsync(query, chatInterface);

            _logger.LogInformation("Intent analysis result: {Analysis}", analysisResponse);

            // Check if this needs a tool call
            if (analysisResponse.Contains("TOOL_CALL:"))
            {
                return await HandleToolBasedQuery(query, analysisResponse, contextKey, chatInterface);
            }
            else
            {
                // Handle as normal Aula question about week letters
                return await HandleRegularAulaQuery(query, contextKey, chatInterface);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query with tools");
            return "❌ I encountered an error processing your request. Please try again.";
        }
    }

    private async Task<string> AnalyzeUserIntentAsync(string query, ChatInterface chatInterface)
    {
        try
        {
            _logger.LogInformation("Starting intent analysis for query: {Query}", query);

            var analysisPrompt = IntentAnalysisPrompts.GetFormattedPrompt(query);

            var chatRequest = new ChatCompletionCreateRequest
            {
                Model = _aiModel,
                Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(analysisPrompt)
            },
                Temperature = 0.1f
            };

            _logger.LogInformation("Sending intent analysis request to OpenAI");
            var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

            if (response.Successful)
            {
                var intentAnalysis = response.Choices.First().Message.Content ?? "INFORMATION_QUERY";
                _logger.LogInformation("Intent analysis completed successfully: {Result}", intentAnalysis);
                return intentAnalysis;
            }
            else
            {
                _logger.LogError("Error analyzing user intent: {Error}", response.Error?.Message);
                return "INFORMATION_QUERY"; // Fallback to regular query
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during intent analysis");
            return "INFORMATION_QUERY"; // Fallback to regular query
        }
    }

    private async Task<string> HandleToolBasedQuery(string query, string analysis, string contextKey, ChatInterface chatInterface)
    {
        try
        {
            // Extract the tool type
            var toolType = analysis.Replace("TOOL_CALL:", "").Trim();

            _logger.LogInformation("Handling tool-based query with tool: {ToolType}", toolType);

            return toolType switch
            {
                "CREATE_REMINDER" => await HandleCreateReminderQuery(query),
                "LIST_REMINDERS" => await _aiToolsManager.ListRemindersAsync(),
                "DELETE_REMINDER" => await HandleDeleteReminderQuery(query),
                "GET_CURRENT_TIME" => _aiToolsManager.GetCurrentDateTime(),
                "HELP" => _aiToolsManager.GetHelp(),
                _ => await HandleRegularAulaQuery(query, contextKey, chatInterface)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool-based query");
            return "❌ I couldn't complete that action. Please try again.";
        }
    }

    private async Task<string> HandleCreateReminderQuery(string query)
    {
        // Use LLM to extract reminder details from natural language
        var extractionPrompt = ReminderExtractionPrompts.GetExtractionPrompt(query, DateTime.Now);

        var chatRequest = new ChatCompletionCreateRequest
        {
            Model = _aiModel,
            Messages = new List<ChatMessage>
            {
                ChatMessage.FromSystem(extractionPrompt)
            },
            Temperature = 0.1f
        };

        try
        {
            _logger.LogInformation("Sending reminder extraction request to OpenAI");
            var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

            if (response.Successful)
            {
                var content = response.Choices.First().Message.Content ?? "";
                _logger.LogInformation("Reminder extraction completed successfully");

                // Parse the structured response with validation
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var description = ExtractValue(lines, "DESCRIPTION") ?? "Reminder";
                var dateTimeStr = ExtractValue(lines, "DATETIME") ?? DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");
                var childName = ExtractValue(lines, "CHILD");

                // Validate datetime format
                if (!DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out _))
                {
                    _logger.LogWarning("Invalid datetime format from AI: {DateTime}", dateTimeStr);
                    dateTimeStr = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");
                }

                if (childName == "NONE") childName = null;

                return await _aiToolsManager.CreateReminderAsync(description, dateTimeStr, childName);
            }
            else
            {
                _logger.LogError("Error extracting reminder details: {Error}", response.Error?.Message);
                return "❌ I couldn't understand the reminder details. Please try again with a clearer format.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during reminder extraction");
            return "❌ I couldn't understand the reminder details. Please try again with a clearer format.";
        }
    }

    private async Task<string> HandleDeleteReminderQuery(string query)
    {
        // Extract reminder number from query
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (int.TryParse(word, out var reminderNumber))
            {
                return await _aiToolsManager.DeleteReminderAsync(reminderNumber);
            }
        }

        return "❌ Please specify which reminder number to delete (e.g., 'delete reminder 2').";
    }

    private Task<string> HandleRegularAulaQuery(string query, string contextKey, ChatInterface chatInterface)
    {
        // Analyze the query to provide more specific guidance
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("activity") || lowerQuery.Contains("aktivitet"))
        {
            return Task.FromResult("To ask about activities, try: 'What activities does [child name] have today?' or 'Hvad skal [child name] i dag?'");
        }

        if (lowerQuery.Contains("remind") || lowerQuery.Contains("mind"))
        {
            return Task.FromResult("To create reminders, try: 'Remind me to pick up [child] at 3pm tomorrow' or 'Mind mig om at hente [child] kl 15 i morgen'");
        }

        // For general information queries, delegate to the existing week letter system
        // CRITICAL: Must return "FALLBACK_TO_EXISTING_SYSTEM" to trigger AgentService fallback logic
        // DO NOT return generic help text here - it causes language mismatch (Danish->English) 
        // and prevents proper week letter processing
        _logger.LogInformation("Delegating Aula query to existing system: {Query}", query);
        return Task.FromResult(FallbackToExistingSystem);
    }

    private static string? ExtractValue(string[] lines, string prefix)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith($"{prefix}:"));
        return line?.Substring(prefix.Length + 1).Trim();
    }
}