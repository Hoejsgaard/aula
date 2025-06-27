using Newtonsoft.Json.Linq;

namespace Aula;

public enum ChatInterface
{
    Slack,
    Telegram
}

public interface IOpenAiService
{
    /// <summary>
    /// Processes a week letter with the OpenAI API and returns a summary.
    /// </summary>
    /// <param name="weekLetter">The week letter JObject from MinUddannelse.</param>
    /// <param name="chatInterface">The chat interface the response will be sent to.</param>
    /// <returns>A summary of the week letter.</returns>
    Task<string> SummarizeWeekLetterAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack);
    
    /// <summary>
    /// Asks a question about a week letter and returns the answer.
    /// </summary>
    /// <param name="weekLetter">The week letter JObject from MinUddannelse.</param>
    /// <param name="question">The question to ask about the week letter.</param>
    /// <param name="chatInterface">The chat interface the response will be sent to.</param>
    /// <returns>The answer to the question.</returns>
    Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, ChatInterface chatInterface = ChatInterface.Slack);
    
    /// <summary>
    /// Asks a question about a week letter with a specific context key and returns the answer.
    /// </summary>
    /// <param name="weekLetter">The week letter JObject from MinUddannelse.</param>
    /// <param name="question">The question to ask about the week letter.</param>
    /// <param name="contextKey">Optional context key to maintain conversation history.</param>
    /// <param name="chatInterface">The chat interface the response will be sent to.</param>
    /// <returns>The answer to the question.</returns>
    Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);
    
    /// <summary>
    /// Extracts key information from a week letter.
    /// </summary>
    /// <param name="weekLetter">The week letter JObject from MinUddannelse.</param>
    /// <param name="chatInterface">The chat interface the response will be sent to.</param>
    /// <returns>A JObject containing key information from the week letter.</returns>
    Task<JObject> ExtractKeyInformationAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack);
    
    /// <summary>
    /// Clears conversation history for a specific context or all contexts.
    /// </summary>
    /// <param name="contextKey">Optional context key. If null, clears all conversation history.</param>
    void ClearConversationHistory(string? contextKey = null);
} 