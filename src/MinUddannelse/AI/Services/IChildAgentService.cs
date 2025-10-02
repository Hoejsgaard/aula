using MinUddannelse.AI.Services;
using MinUddannelse.Content.WeekLetters;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.AI.Services;

/// <summary>
/// Child-aware agent service interface that uses IChildContext to determine the current child.
/// Provides secure, isolated AI interactions for each child without requiring Child parameters.
/// </summary>
public interface IChildAgentService
{
    /// <summary>
    /// Summarizes the week letter for the current child from context.
    /// </summary>
    Task<string> SummarizeWeekLetterAsync(DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);

    /// <summary>
    /// Asks a question about the week letter for the current child from context.
    /// Input is sanitized to prevent prompt injection attacks.
    /// </summary>
    Task<string> AskQuestionAboutWeekLetterAsync(DateOnly date, string question, ChatInterface chatInterface = ChatInterface.Slack);

    /// <summary>
    /// Asks a question about the week letter with conversation context for the current child.
    /// Input is sanitized to prevent prompt injection attacks.
    /// </summary>
    Task<string> AskQuestionAboutWeekLetterAsync(DateOnly date, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    /// <summary>
    /// Extracts key information from the week letter for the current child from context.
    /// </summary>
    Task<JObject> ExtractKeyInformationAsync(DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);

    /// <summary>
    /// Processes a query with AI tools for the current child from context.
    /// Input is sanitized to prevent prompt injection attacks.
    /// </summary>
    Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    /// <summary>
    /// Clears the conversation history for the current child from context.
    /// </summary>
    Task ClearConversationHistoryAsync(string? contextKey = null);

    /// <summary>
    /// Gets the current conversation context key for the child.
    /// </summary>
    string GetConversationContextKey();

    /// <summary>
    /// Validates if a response is appropriate for the current child.
    /// </summary>
    Task<bool> ValidateResponseAppropriateness(string response);
}
