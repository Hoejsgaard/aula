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

    public OpenAiService(string apiKey, ILoggerFactory loggerFactory)
    {
        _openAiClient = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = apiKey
        });
        _logger = loggerFactory.CreateLogger(nameof(OpenAiService));
    }

    public async Task<string> SummarizeWeekLetterAsync(JObject weekLetter)
    {
        _logger.LogInformation("Summarizing week letter");
        
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
            ChatMessage.FromSystem("You are a helpful assistant that summarizes weekly school letters for parents. " +
                                  "Provide a brief summary of the key information in the letter, focusing on activities, " +
                                  "important dates, and things parents need to know. Be concise but thorough."),
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

    public async Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question)
    {
        return await AskQuestionAboutWeekLetterAsync(weekLetter, question, null);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, string? contextKey)
    {
        _logger.LogInformation("Asking question about week letter: {Question}", question);
        
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
        
        // Create a unique conversation key if not provided
        if (string.IsNullOrEmpty(contextKey))
        {
            contextKey = $"{childName}-{weekNumber}";
        }
        
        // Initialize conversation history if it doesn't exist
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            _conversationHistory[contextKey] = new List<ChatMessage>
            {
                ChatMessage.FromSystem($"You are a helpful assistant that answers questions about a child's weekly school letter. " +
                                      $"The child's name is {childName} and they're in class {className}. " +
                                      $"This is for week {weekNumber}. Today is {DateTime.Now.DayOfWeek}. " +
                                      $"Answer questions based on the content of the letter. If the information isn't in the letter, say so politely.")
            };
            
            // Add the week letter content as context
            _conversationHistory[contextKey].Add(ChatMessage.FromSystem($"Here's the week letter content:\n\n{weekLetterContent}"));
        }
        
        // Add the user's question to the conversation history
        _conversationHistory[contextKey].Add(ChatMessage.FromUser(question));
        
        // Limit conversation history to prevent token overflow (keep last 10 messages)
        if (_conversationHistory[contextKey].Count > 12)
        {
            // Keep the system messages (first 2) and the last 10 messages
            _conversationHistory[contextKey] = _conversationHistory[contextKey].Take(2)
                .Concat(_conversationHistory[contextKey].Skip(_conversationHistory[contextKey].Count - 10))
                .ToList();
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

    public async Task<JObject> ExtractKeyInformationAsync(JObject weekLetter)
    {
        _logger.LogInformation("Extracting key information from week letter");
        
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
        var sb = new StringBuilder();
        
        // Check for content in the old format
        if (weekLetter["content"] != null)
        {
            sb.AppendLine(weekLetter["content"]?.ToString() ?? string.Empty);
            return sb.ToString();
        }
        
        // Extract content from the ugebreve array
        if (weekLetter["ugebreve"] != null && weekLetter["ugebreve"] is JArray ugebreve && ugebreve.Count > 0)
        {
            var indhold = ugebreve[0]?["indhold"]?.ToString();
            if (!string.IsNullOrEmpty(indhold))
            {
                sb.AppendLine(indhold);
            }
        }
        
        return sb.ToString();
    }
    
    // Method to clear conversation history for a specific context or all contexts
    public void ClearConversationHistory(string? contextKey = null)
    {
        if (string.IsNullOrEmpty(contextKey))
        {
            _conversationHistory.Clear();
        }
        else if (_conversationHistory.ContainsKey(contextKey))
        {
            _conversationHistory.Remove(contextKey);
        }
    }
} 