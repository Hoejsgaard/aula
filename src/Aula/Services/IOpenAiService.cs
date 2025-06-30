using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula.Services;

public enum ChatInterface
{
    Slack,
    Telegram
}

public interface IOpenAiService
{
    Task<string> SummarizeWeekLetterAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack);

    Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, ChatInterface chatInterface = ChatInterface.Slack);

    Task<string> AskQuestionAboutWeekLetterAsync(JObject weekLetter, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    Task<JObject> ExtractKeyInformationAsync(JObject weekLetter, ChatInterface chatInterface = ChatInterface.Slack);

    Task<string> AskQuestionAboutChildrenAsync(Dictionary<string, JObject> childrenWeekLetters, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    void ClearConversationHistory(string? contextKey = null);
}