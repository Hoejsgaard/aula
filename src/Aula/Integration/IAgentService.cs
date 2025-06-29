using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Integration;

public interface IAgentService
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetterAsync(Child child, DateOnly date, bool useCache = true);
    Task<JObject> GetWeekScheduleAsync(Child child, DateOnly date, bool useCache = true);

    // OpenAI-related methods
    Task<string> SummarizeWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);
    Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, ChatInterface chatInterface = ChatInterface.Slack);
    Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);
    Task<JObject> ExtractKeyInformationFromWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);

    // Child management methods
    Task<Child?> GetChildByNameAsync(string childName);
    Task<IEnumerable<Child>> GetAllChildrenAsync();
    Task<string> AskQuestionAboutChildrenAsync(Dictionary<string, JObject> childrenWeekLetters, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);
    Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, ChatInterface chatInterface = ChatInterface.Slack);
}