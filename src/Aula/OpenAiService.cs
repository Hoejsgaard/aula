using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.Managers;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Aula;

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIService _openAiClient;
    private readonly ILogger _logger;
    private readonly AiToolsManager _aiToolsManager;
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistory = new();
    private readonly Dictionary<string, string> _currentChildContext = new();

    public OpenAiService(string apiKey, ILoggerFactory loggerFactory, AiToolsManager aiToolsManager)
    {
        _openAiClient = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = apiKey
        });
        _logger = loggerFactory.CreateLogger(nameof(OpenAiService));
        _aiToolsManager = aiToolsManager;
    }

    public async Task<string> SummarizeWeekLetterAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Summarizing week letter for {ChatInterface}", chatInterface);

        var weekLetterContent = ExtractWeekLetterContent(weekLetter);

        // Extract metadata from the week letter
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
        if (weekLetter["class"] != null) className = weekLetter["class"]?.ToString() ?? "unknown";
        if (weekLetter["week"] != null) weekNumber = weekLetter["week"]?.ToString() ?? "unknown";

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem($"You are a helpful assistant that summarizes weekly school letters for parents. " +
                                  "Provide a brief summary of the key information in the letter, focusing on activities, " +
                                  "important dates, and things parents need to know. Be concise but thorough. " +
                                  $"You are responding via {GetChatInterfaceInstructions(chatInterface)}"),
            ChatMessage.FromUser($"Here's the week letter for {className} for week {weekNumber}:\n\n{weekLetterContent}\n\nPlease summarize this week letter.")
        };

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = Models.Gpt_4,
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
        contextKey = EnsureContextKey(contextKey, childName);

        EnsureConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        AddUserQuestionToHistory(contextKey, question);
        TrimConversationHistoryIfNeeded(contextKey);
        LogConversationHistoryStructure(contextKey);

        return await SendChatRequestAndGetResponse(contextKey);
    }

    private string EnsureContextKey(string? contextKey, string childName)
    {
        if (string.IsNullOrEmpty(contextKey))
        {
            contextKey = childName.ToLowerInvariant();
        }
        _currentChildContext[contextKey] = childName;
        return contextKey;
    }

    private void EnsureConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            InitializeNewConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
        else
        {
            UpdateExistingConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
    }

    private void InitializeNewConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        _conversationHistory[contextKey] = new List<ChatMessage>
        {
            CreateSystemInstructionsMessage(childName, chatInterface),
            CreateWeekLetterContentMessage(childName, weekLetterContent)
        };
    }

    private void UpdateExistingConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        if (ShouldResetHistoryForNewChild(contextKey, childName))
        {
            InitializeNewConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
        else
        {
            RefreshWeekLetterContentInHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
    }

    private bool ShouldResetHistoryForNewChild(string contextKey, string childName)
    {
        return _currentChildContext.TryGetValue(contextKey, out var previousChildName) &&
               !string.Equals(previousChildName, childName, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshWeekLetterContentInHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        _logger.LogInformation("🔎 TRACE: Updating existing conversation context for {ContextKey}", contextKey);
        
        int contentIndex = FindWeekLetterContentIndex(contextKey);
        
        if (contentIndex >= 0)
        {
            UpdateExistingWeekLetterContent(contextKey, childName, weekLetterContent, contentIndex, chatInterface);
        }
        else
        {
            InsertWeekLetterContent(contextKey, childName, weekLetterContent);
        }
    }

    private int FindWeekLetterContentIndex(string contextKey)
    {
        for (int i = 0; i < _conversationHistory[contextKey].Count; i++)
        {
            var message = _conversationHistory[contextKey][i];
            if (message?.Role == "system" &&
                message.Content?.StartsWith("Here's the weekly letter content") == true)
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateExistingWeekLetterContent(string contextKey, string childName, string weekLetterContent, int contentIndex, ChatInterface chatInterface)
    {
        _logger.LogInformation("🔎 TRACE: Found existing week letter content at index {Index}, updating", contentIndex);
        _conversationHistory[contextKey][contentIndex] = CreateWeekLetterContentMessage(childName, weekLetterContent);
        _logger.LogInformation("🔎 TRACE: Updated existing week letter content in context: {Length} characters", weekLetterContent.Length);

        if (contentIndex > 0)
        {
            _conversationHistory[contextKey][0] = CreateSystemInstructionsMessage(childName, chatInterface);
            _logger.LogInformation("🔎 TRACE: Updated system instructions in context");
        }
    }

    private void InsertWeekLetterContent(string contextKey, string childName, string weekLetterContent)
    {
        _logger.LogInformation("🔎 TRACE: No existing week letter content found, inserting after first system message");
        _conversationHistory[contextKey].Insert(1, CreateWeekLetterContentMessage(childName, weekLetterContent));
        _logger.LogInformation("🔎 TRACE: Added week letter content to existing context: {Length} characters", weekLetterContent.Length);
    }

    private ChatMessage CreateSystemInstructionsMessage(string childName, ChatInterface chatInterface)
    {
        return ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
                                    $"This letter is specifically about {childName}'s class and activities. " +
                                    $"Today is {DateTime.Now.DayOfWeek}, {DateTime.Now.ToString("MMMM d, yyyy")}. " +
                                    "Answer based on the content of the letter. If the specific information isn't in the letter, " +
                                    "say 'I don't have that specific information in the weekly letter' and then provide any related " +
                                    "information that might be helpful. For example, if asked about Tuesday's activities but only " +
                                    "Monday's activities are mentioned, acknowledge that Tuesday isn't mentioned but share what's " +
                                    "happening on Monday. Be concise and direct in your answers. " +
                                    "CRITICAL: You can ONLY answer questions about the weekly letter content. You CANNOT create reminders, " +
                                    "set alarms, or take any actions. Never promise to remind users of anything. If asked to create " +
                                    "a reminder, explain they need to use specific reminder commands instead. " +
                                    "IMPORTANT: Always respond in the same language as the user's question. " +
                                    "If the question is in Danish, respond in Danish. If the question is in English, respond in English. " +
                                    $"You are responding via {GetChatInterfaceInstructions(chatInterface)}");
    }

    private ChatMessage CreateWeekLetterContentMessage(string childName, string weekLetterContent)
    {
        return ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}");
    }

    private void AddUserQuestionToHistory(string contextKey, string question)
    {
        _logger.LogInformation("🔎 TRACE: Adding user question to conversation history: {Question}", question);
        _conversationHistory[contextKey].Add(ChatMessage.FromUser(question));
    }

    private void TrimConversationHistoryIfNeeded(string contextKey)
    {
        if (_conversationHistory[contextKey].Count > 12)
        {
            _conversationHistory[contextKey] = _conversationHistory[contextKey].Take(2)
                .Concat(_conversationHistory[contextKey].Skip(_conversationHistory[contextKey].Count - 10))
                .ToList();

            _logger.LogInformation("🔎 TRACE: Trimmed conversation history to prevent token overflow");
        }
    }

    private void LogConversationHistoryStructure(string contextKey)
    {
        _logger.LogInformation("🔎 TRACE: Conversation history structure:");
        for (int i = 0; i < _conversationHistory[contextKey].Count; i++)
        {
            var message = _conversationHistory[contextKey][i];
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
        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = _conversationHistory[contextKey],
            Model = Models.Gpt_4,
            Temperature = 0.7f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            var reply = response.Choices.First().Message.Content ?? "No response content received.";
            _conversationHistory[contextKey].Add(ChatMessage.FromAssistant(reply));
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

        // Extract metadata from the week letter
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
        if (weekLetter["class"] != null) className = weekLetter["class"]?.ToString() ?? "unknown";
        if (weekLetter["week"] != null) weekNumber = weekLetter["week"]?.ToString() ?? "unknown";

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem("You are a helpful assistant that extracts structured information from weekly school letters. " +
                                  "Extract information about activities for each day of the week, homework, and important dates. " +
                                  "Respond in valid JSON format with the following structure: " +
                                  "{ \"monday\": \"activities\", \"tuesday\": \"activities\", \"wednesday\": \"activities\", " +
                                  "\"thursday\": \"activities\", \"friday\": \"activities\", \"homework\": \"homework details\", " +
                                  "\"important_dates\": [\"date1\", \"date2\"] }"),
            ChatMessage.FromUser($"Here's the week letter for {childName} in class {className} for week {weekNumber}:\n\n{weekLetterContent}\n\nPlease extract the key information in JSON format.")
        };

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = Models.Gpt_4,
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

        var combinedContent = new StringBuilder();
        combinedContent.AppendLine("Week letters for children:");
        combinedContent.AppendLine();

        foreach (var (childName, weekLetter) in childrenWeekLetters)
        {
            var weekLetterContent = ExtractWeekLetterContent(weekLetter);
            combinedContent.AppendLine($"=== {childName} ===");
            combinedContent.AppendLine(weekLetterContent);
            combinedContent.AppendLine();
        }

        var contextKeyToUse = contextKey ?? "combined-children";

        if (!_conversationHistory.ContainsKey(contextKeyToUse))
        {
            _conversationHistory[contextKeyToUse] = new List<ChatMessage>();
        }

        var systemPrompt = $@"You are an AI assistant helping parents understand their children's school activities. 
You have been provided with week letters from school for multiple children. 
Answer questions about the children's activities clearly and helpfully.

CRITICAL: You can ONLY answer questions about the weekly letter content. You CANNOT create reminders, 
set alarms, or take any actions. Never promise to remind users of anything. If asked to create 
a reminder, explain they need to use specific reminder commands instead.

{GetChatInterfaceInstructions(chatInterface)}

When answering about multiple children, clearly indicate which child each piece of information relates to.
If a question is about a specific child, focus on that child but you can mention relevant information about other children if helpful.
If a question could apply to multiple children, provide information for all relevant children.

Current day context: Today is {DateTime.Now.ToString("dddd, MMMM dd, yyyy")}";

        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem(systemPrompt),
            ChatMessage.FromUser($"Here are the week letters:\n\n{combinedContent}")
        };

        messages.AddRange(_conversationHistory[contextKeyToUse]);
        messages.Add(ChatMessage.FromUser(question));

        var chatRequest = new ChatCompletionCreateRequest
        {
            Messages = messages,
            Model = Models.Gpt_4,
            Temperature = 0.3f
        };

        var response = await _openAiClient.ChatCompletion.CreateCompletion(chatRequest);

        if (response.Successful)
        {
            var answer = response.Choices.First().Message.Content ?? "I couldn't generate a response.";

            _conversationHistory[contextKeyToUse].Add(ChatMessage.FromUser(question));
            _conversationHistory[contextKeyToUse].Add(ChatMessage.FromAssistant(answer));

            if (_conversationHistory[contextKeyToUse].Count > 20)
            {
                _conversationHistory[contextKeyToUse] = _conversationHistory[contextKeyToUse].Skip(4).ToList();
            }

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
        if (string.IsNullOrEmpty(contextKey))
        {
            _conversationHistory.Clear();
            _currentChildContext.Clear();
            _logger.LogInformation("Cleared all conversation history and child contexts");
        }
        else if (_conversationHistory.ContainsKey(contextKey))
        {
            _conversationHistory.Remove(contextKey);
            _currentChildContext.Remove(contextKey);
            _logger.LogInformation("Cleared conversation history and child context for {ContextKey}", contextKey);
        }
    }

    // Helper method to get chat interface-specific instructions
    private string GetChatInterfaceInstructions(ChatInterface chatInterface)
    {
        return chatInterface switch
        {
            ChatInterface.Slack => "Slack. Format your responses using Slack markdown (e.g., *bold*, _italic_, `code`). Don't use HTML tags.",
            ChatInterface.Telegram => "Telegram. Format your responses using HTML tags (e.g., <b>bold</b>, <i>italic</i>, <code>code</code>). Telegram supports these HTML tags: <b>, <i>, <u>, <s>, <a>, <code>, <pre>. Use <br/> for line breaks.",
            _ => "a chat interface. Use plain text formatting."
        };
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

            var analysisPrompt = $@"Analyze this user query and determine what they want to do:

Query: ""{query}""

Determine if this query requires any of these ACTIONS:
- CREATE_REMINDER: User wants to set up a reminder/notification
- LIST_REMINDERS: User wants to see their current reminders
- DELETE_REMINDER: User wants to remove a reminder
- GET_CURRENT_TIME: User wants to know the current date/time
- HELP: User wants help or available commands

If the query requires an ACTION, respond with: TOOL_CALL: [ACTION_NAME]
If it's a question about children's school activities, week letters, or schedules, respond with: INFORMATION_QUERY

Examples:
- ""Remind me tomorrow at 8 AM about TestChild1's soccer"" → TOOL_CALL: CREATE_REMINDER
- ""Kan du minde mig om at hente øl om 2 timer?"" → TOOL_CALL: CREATE_REMINDER
- ""What are my reminders?"" → TOOL_CALL: LIST_REMINDERS  
- ""Vis mine påmindelser"" → TOOL_CALL: LIST_REMINDERS
- ""Delete reminder 2"" → TOOL_CALL: DELETE_REMINDER
- ""What time is it?"" → TOOL_CALL: GET_CURRENT_TIME
- ""Hvad er klokken?"" → TOOL_CALL: GET_CURRENT_TIME
- ""What does Emma have today?"" → INFORMATION_QUERY
- ""Hvad skal TestChild2 i dag?"" → INFORMATION_QUERY
- ""Show me this week's letter"" → INFORMATION_QUERY

Analyze the query and respond accordingly:";

            var chatRequest = new ChatCompletionCreateRequest
            {
                Model = Models.Gpt_4,
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
        var extractionPrompt = $@"Extract reminder details from this natural language request:

Query: ""{query}""

Extract:
1. Description: What to remind about
2. DateTime: When to remind (convert to yyyy-MM-dd HH:mm format)
3. ChildName: If mentioned, the child's name (optional)

For relative dates (current time is {DateTime.Now:yyyy-MM-dd HH:mm}):
- ""tomorrow"" = {DateTime.Today.AddDays(1):yyyy-MM-dd}
- ""today"" = {DateTime.Today:yyyy-MM-dd}
- ""next Monday"" = calculate the next Monday
- ""in 2 hours"" = {DateTime.Now.AddHours(2):yyyy-MM-dd HH:mm}
- ""om 2 minutter"" = {DateTime.Now.AddMinutes(2):yyyy-MM-dd HH:mm}
- ""om 30 minutter"" = {DateTime.Now.AddMinutes(30):yyyy-MM-dd HH:mm}

Respond in this exact format:
DESCRIPTION: [extracted description]
DATETIME: [yyyy-MM-dd HH:mm]
CHILD: [child name or NONE]";

        var chatRequest = new ChatCompletionCreateRequest
        {
            Model = Models.Gpt_4,
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

                // Parse the structured response
                var lines = content.Split('\n');
                var description = lines.FirstOrDefault(l => l.StartsWith("DESCRIPTION:"))?.Replace("DESCRIPTION:", "").Trim() ?? "Reminder";
                var dateTime = lines.FirstOrDefault(l => l.StartsWith("DATETIME:"))?.Replace("DATETIME:", "").Trim() ?? DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm");
                var childName = lines.FirstOrDefault(l => l.StartsWith("CHILD:"))?.Replace("CHILD:", "").Trim();

                if (childName == "NONE") childName = null;

                return await _aiToolsManager.CreateReminderAsync(description, dateTime, childName);
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
        // For information queries, we need to get week letters and use the existing AskQuestionAboutChildrenAsync
        try
        {
            // We need to get the children and their week letters to answer the question
            // Since we don't have direct access to AgentService here, we'll need to pass this through
            // For now, return a message indicating this should be handled by the existing system

            // Get all children from DataManager (we have access through AiToolsManager)
            var allChildren = _aiToolsManager.GetWeekLetters(); // This gets basic info

            // The proper way is to fall back to the existing AskQuestionAboutChildrenAsync
            // but we need the week letters in the right format. Let me implement a bridge method.

            // For now, let's indicate that this should use the fallback
            return Task.FromResult("FALLBACK_TO_EXISTING_SYSTEM");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling regular Aula query");
            return Task.FromResult("❌ I couldn't process your question about school activities right now.");
        }
    }
}