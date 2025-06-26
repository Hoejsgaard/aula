using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Chat;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Aula;

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIAPI _openAiClient;
    private readonly ILogger _logger;

    public OpenAiService(string apiKey, ILoggerFactory loggerFactory)
    {
        _openAiClient = new OpenAIAPI(apiKey);
        _logger = loggerFactory.CreateLogger(nameof(OpenAiService));
    }

    public async Task<string> SummarizeWeekLetterAsync(JObject weekLetter)
    {
        _logger.LogInformation("Summarizing week letter");
        
        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        
        var chat = _openAiClient.Chat.CreateConversation();
        
        // Use GPT-4 if available, otherwise fall back to GPT-3.5
        try
        {
            chat.Model = OpenAI_API.Models.Model.GPT4;
        }
        catch
        {
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
        }
        
        chat.AppendSystemMessage("You are a helpful assistant that summarizes weekly school letters for parents. " +
                               "Extract the most important information and present it in a clear, concise format. " +
                               "Focus on key events, deadlines, and important notices.");
        
        chat.AppendUserInput($"Please summarize this week letter from school:\n\n{weekLetterContent}");
        
        try
        {
            var response = await chat.GetResponseFromChatbotAsync();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error from OpenAI API");
            return "Error processing the week letter. Please try again later.";
        }
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question)
    {
        _logger.LogInformation("Asking question about week letter: {Question}", question);
        
        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        
        var chat = _openAiClient.Chat.CreateConversation();
        
        // Use GPT-4 if available, otherwise fall back to GPT-3.5
        try
        {
            chat.Model = OpenAI_API.Models.Model.GPT4;
        }
        catch
        {
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
        }
        
        chat.AppendSystemMessage("You are a helpful assistant that answers questions about weekly school letters. " +
                               "Provide accurate, concise answers based only on the information in the week letter.");
        
        chat.AppendUserInput($"Week letter content:\n\n{weekLetterContent}\n\nQuestion: {question}");
        
        try
        {
            var response = await chat.GetResponseFromChatbotAsync();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error from OpenAI API: {Error}", ex.Message);
            return "Error processing your question. Please try again later.";
        }
    }

    public async Task<JObject> ExtractKeyInformationAsync(JObject weekLetter)
    {
        _logger.LogInformation("Extracting key information from week letter");
        
        var weekLetterContent = ExtractWeekLetterContent(weekLetter);
        
        var chat = _openAiClient.Chat.CreateConversation();
        
        // Use GPT-4 if available, otherwise fall back to GPT-3.5
        try
        {
            chat.Model = OpenAI_API.Models.Model.GPT4;
        }
        catch
        {
            chat.Model = OpenAI_API.Models.Model.ChatGPTTurbo;
        }
        
        chat.AppendSystemMessage("You are a helpful assistant that extracts key information from weekly school letters. " +
                               "Extract events, deadlines, required materials, and other important information. " +
                               "Return the information as a JSON object with appropriate categories.");
        
        chat.AppendUserInput($"Extract key information from this week letter as JSON:\n\n{weekLetterContent}");
        
        try
        {
            var response = await chat.GetResponseFromChatbotAsync();
            return JObject.Parse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error from OpenAI API or parsing JSON response");
            return new JObject { ["error"] = "Error processing the week letter or parsing the response" };
        }
    }
    
    private string ExtractWeekLetterContent(JObject weekLetter)
    {
        try
        {
            var content = new StringBuilder();
            
            if (weekLetter["ugebreve"] is JArray ugebreve && ugebreve.Count > 0)
            {
                foreach (var ugeBrev in ugebreve)
                {
                    var klasseNavn = ugeBrev["klasseNavn"]?.ToString();
                    var uge = ugeBrev["uge"]?.ToString();
                    var indhold = ugeBrev["indhold"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(klasseNavn) && !string.IsNullOrEmpty(indhold))
                    {
                        content.AppendLine($"Class: {klasseNavn}");
                        content.AppendLine($"Week: {uge}");
                        content.AppendLine("Content:");
                        content.AppendLine(indhold);
                        content.AppendLine();
                    }
                }
            }
            
            return content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content from week letter");
            return "Error extracting content from week letter";
        }
    }
} 