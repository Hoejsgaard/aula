using OpenAI.ObjectModels.RequestModels;
using System.Collections.Generic;

namespace Aula.AI.Services;

public interface IConversationManager
{
    void EnsureConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface);
    void AddUserQuestionToHistory(string contextKey, string question);
    void AddAssistantResponseToHistory(string contextKey, string response);
    void TrimConversationHistoryIfNeeded(string contextKey);
    void TrimMultiChildConversationIfNeeded(string contextKey);
    List<ChatMessage> GetConversationHistory(string contextKey);
    void ClearConversationHistory(string? contextKey = null);
    string EnsureContextKey(string? contextKey, string childName);
}
