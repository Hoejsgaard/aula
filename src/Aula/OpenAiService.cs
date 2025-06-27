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
    private readonly Dictionary<string, List<ChatMessage>> _conversationHistory = new();
    private readonly Dictionary<string, string> _currentChildContext = new();

    public OpenAiService(string apiKey, ILoggerFactory loggerFactory)
    {
        _openAiClient = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = apiKey
        });
        _logger = loggerFactory.CreateLogger(nameof(OpenAiService));
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
                                  "You are responding via {GetChatInterfaceInstructions(chatInterface)}"),
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
        _logger.LogInformation("🔎 TRACE: AskQuestionAboutWeekLetterAsync called with question: {Question} for {ChatInterface}", question, chatInterface);
        
        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        _logger.LogInformation("🔎 TRACE: Week letter content in AskQuestionAboutWeekLetterAsync: {Length} characters", weekLetterContent.Length);
        
        // Extract basic metadata from the week letter
        string childName = weekLetter["child"]?.ToString() ?? "unknown";
        _logger.LogInformation("🔎 TRACE: Child name from week letter: {ChildName}", childName);
        
        // Create a unique conversation key if not provided
        if (string.IsNullOrEmpty(contextKey))
        {
            contextKey = childName.ToLowerInvariant();
            _logger.LogInformation("🔎 TRACE: Created context key from child name: {ContextKey}", contextKey);
        }
        else
        {
            _logger.LogInformation("🔎 TRACE: Using provided context key: {ContextKey}", contextKey);
        }
        
        // Store the current child name for this context
        _currentChildContext[contextKey] = childName;
        _logger.LogInformation("🔎 TRACE: Set current child context for {ContextKey} to {ChildName}", contextKey, childName);
        
        // Initialize conversation history if it doesn't exist
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            _logger.LogInformation("🔎 TRACE: Creating new conversation context for {ContextKey}", contextKey);
            _conversationHistory[contextKey] = new List<ChatMessage>
            {
                ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
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
                                      $"You are responding via {GetChatInterfaceInstructions(chatInterface)}"),
                ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}")
            };
            _logger.LogInformation("🔎 TRACE: Added week letter content to conversation context: {Length} characters", weekLetterContent.Length);
        }
        else
        {
            // Check if the child has changed for this context key
            if (_currentChildContext.TryGetValue(contextKey, out var previousChildName) && 
                !string.Equals(previousChildName, childName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("🔎 TRACE: Child changed for context {ContextKey} from {PreviousChild} to {CurrentChild}, resetting context", 
                    contextKey, previousChildName, childName);
                
                // Reset the conversation history for this context key
                _conversationHistory[contextKey] = new List<ChatMessage>
                {
                    ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
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
                                          $"You are responding via {GetChatInterfaceInstructions(chatInterface)}"),
                    ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}")
                };
                _logger.LogInformation("🔎 TRACE: Reset conversation history for {ContextKey} with new child {ChildName}", contextKey, childName);
            }
            else
            {
                // Always refresh the week letter content in the context
                // First, find the system message with the week letter content
                _logger.LogInformation("🔎 TRACE: Updating existing conversation context for {ContextKey}", contextKey);
                int contentIndex = -1;
                for (int i = 0; i < _conversationHistory[contextKey].Count; i++)
                {
                    var message = _conversationHistory[contextKey][i];
                    if (message != null && message.Role == "system" && 
                        message.Content != null && message.Content.StartsWith("Here's the weekly letter content"))
                    {
                        contentIndex = i;
                        break;
                    }
                }
                
                // If found, update it; otherwise, add it
                if (contentIndex >= 0)
                {
                    _logger.LogInformation("🔎 TRACE: Found existing week letter content at index {Index}, updating", contentIndex);
                    _conversationHistory[contextKey][contentIndex] = ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}");
                    _logger.LogInformation("🔎 TRACE: Updated existing week letter content in context: {Length} characters", weekLetterContent.Length);
                    
                    // Also update the system instructions if it's the first message
                    if (contentIndex > 0)
                    {
                        _conversationHistory[contextKey][0] = ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
                                          $"This letter is specifically about {childName}'s class and activities. " +
                                          $"Today is {DateTime.Now.DayOfWeek}, {DateTime.Now.ToString("MMMM d, yyyy")}. " +
                                          "Answer based on the content of the letter. If the specific information isn't in the letter, " +
                                          "say 'I don't have that specific information in the weekly letter' and then provide any related " +
                                          "information that might be helpful. For example, if asked about Tuesday's activities but only " +
                                          "Monday's activities are mentioned, acknowledge that Tuesday isn't mentioned but share what's " +
                                          "happening on Monday. Be concise and direct in your answers. " +
                                          "IMPORTANT: Always respond in the same language as the user's question. " +
                                          "If the question is in Danish, respond in Danish. If the question is in English, respond in English. " +
                                          $"You are responding via {GetChatInterfaceInstructions(chatInterface)}");
                        _logger.LogInformation("🔎 TRACE: Updated system instructions in context");
                    }
                }
                else
                {
                    _logger.LogInformation("🔎 TRACE: No existing week letter content found, inserting after first system message");
                    // Insert after the first system message
                    _conversationHistory[contextKey].Insert(1, ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}"));
                    _logger.LogInformation("🔎 TRACE: Added week letter content to existing context: {Length} characters", weekLetterContent.Length);
                }
            }
        }
        
        // Add the user's question to the conversation history
        _logger.LogInformation("🔎 TRACE: Adding user question to conversation history: {Question}", question);
        _conversationHistory[contextKey].Add(ChatMessage.FromUser(question));
        
        // Limit conversation history to prevent token overflow (keep last 10 messages)
        if (_conversationHistory[contextKey].Count > 12)
        {
            // Keep the system messages (first 2) and the last 10 messages
            _conversationHistory[contextKey] = _conversationHistory[contextKey].Take(2)
                .Concat(_conversationHistory[contextKey].Skip(_conversationHistory[contextKey].Count - 10))
                .ToList();
            
            _logger.LogInformation("🔎 TRACE: Trimmed conversation history to prevent token overflow");
        }

        // Log the conversation history structure
        _logger.LogInformation("🔎 TRACE: Conversation history structure:");
        for (int i = 0; i < _conversationHistory[contextKey].Count; i++)
        {
            var message = _conversationHistory[contextKey][i];
            _logger.LogInformation("🔎 TRACE: Message {Index}: Role={Role}, Content Length={Length}", 
                i, message.Role, message.Content?.Length ?? 0);
            
            // Log the first 100 characters of each message for debugging
            if (message.Content != null && message.Content.Length > 0)
            {
                var preview = message.Content.Length > 100 ? message.Content.Substring(0, 100) + "..." : message.Content;
                _logger.LogInformation("🔎 TRACE: Message {Index} Preview: {Preview}", i, preview);
            }
        }

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
            // Add the assistant's response to the conversation history
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
        _logger.LogInformation("📋 DEBUG: ExtractWeekLetterContent called with JObject with keys: {Keys}", 
            string.Join(", ", weekLetter.Properties().Select(p => p.Name)));
            
        // Check if the ugebreve array exists and has elements
        if (weekLetter["ugebreve"] == null)
        {
            _logger.LogWarning("📋 DEBUG: Week letter does not contain 'ugebreve' property");
        }
        else if (!(weekLetter["ugebreve"] is JArray ugebreve) || ugebreve.Count == 0)
        {
            _logger.LogWarning("📋 DEBUG: Week letter 'ugebreve' is not an array or is empty");
        }
        else
        {
            var ugebreveArray = (JArray)weekLetter["ugebreve"]!;
            _logger.LogInformation("📋 DEBUG: Week letter 'ugebreve' array contains {Count} items", ugebreveArray.Count);
            
            if (ugebreveArray[0] != null)
            {
                var firstItem = ugebreveArray[0];
                _logger.LogInformation("📋 DEBUG: First ugebreve item has keys: {Keys}", 
                    string.Join(", ", firstItem.Children<JProperty>().Select(p => p.Name)));
                
                if (firstItem["indhold"] == null)
                {
                    _logger.LogWarning("📋 DEBUG: First ugebreve item does not contain 'indhold' property");
                }
                else
                {
                    _logger.LogInformation("📋 DEBUG: Found 'indhold' property in first ugebreve item");
                }
            }
        }
        
        // Direct access to content as specified
        var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        
        _logger.LogInformation("📋 DEBUG: Extracted week letter content: {Length} characters", weekLetterContent.Length);
        
        if (string.IsNullOrEmpty(weekLetterContent))
        {
            _logger.LogWarning("📋 DEBUG: Week letter content is empty! Raw week letter: {WeekLetter}", weekLetter.ToString());
        }
        else
        {
            _logger.LogInformation("📋 DEBUG: Week letter content starts with: {Start}", 
                weekLetterContent.Length > 100 ? weekLetterContent.Substring(0, 100) + "..." : weekLetterContent);
        }
        
        return weekLetterContent;
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
} 