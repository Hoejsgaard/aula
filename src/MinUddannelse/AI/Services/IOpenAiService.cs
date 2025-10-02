using System.Threading.Tasks;
using MinUddannelse.Configuration;

namespace MinUddannelse.AI.Services;

/// <summary>
/// OpenAI service that accepts Child parameters for all operations.
/// </summary>
public interface IOpenAiService
{
    /// <summary>
    /// Gets an AI response for the given query in the specified child's context.
    /// </summary>
    Task<string?> GetResponseAsync(Child child, string query);

    /// <summary>
    /// Gets an AI response with conversation context for the specified child.
    /// </summary>
    Task<string?> GetResponseWithContextAsync(Child child, string query, string conversationId);

    /// <summary>
    /// Clears the conversation history for the specified child.
    /// </summary>
    Task ClearConversationHistoryAsync(Child child, string conversationId);
}
