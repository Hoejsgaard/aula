using System.Threading.Tasks;

namespace Aula.Services;

/// <summary>
/// OpenAI service that is aware of the current child context.
/// This is a simplified version for Chapter 7 migration.
/// </summary>
public interface IChildAwareOpenAiService
{
	/// <summary>
	/// Gets an AI response for the given query in the current child's context.
	/// </summary>
	Task<string?> GetResponseAsync(string query);

	/// <summary>
	/// Gets an AI response with conversation context.
	/// </summary>
	Task<string?> GetResponseWithContextAsync(string query, string conversationId);

	/// <summary>
	/// Clears the conversation history for the current child.
	/// </summary>
	Task ClearConversationHistoryAsync(string conversationId);
}
