using System;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Integration;

[Obsolete("Use IChildAgentService with IChildContext instead. This interface will be removed in the next major version. " +
          "For child management, use IChildServiceCoordinator. For child-specific operations, use child-aware services.")]
public interface IAgentService
{
    Task<bool> LoginAsync();
    Task<JObject?> GetWeekLetterAsync(Child child, DateOnly date, bool useCache = true, bool allowLiveFetch = false);
    Task<JObject?> GetWeekScheduleAsync(Child child, DateOnly date, bool useCache = true);

    // OpenAI-related methods
    Task<string> SummarizeWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);
    Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, ChatInterface chatInterface = ChatInterface.Slack);
    Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);
    Task<JObject> ExtractKeyInformationFromWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack);

    // Child management methods
    ValueTask<Child?> GetChildByNameAsync(string childName);

    [Obsolete("SECURITY VIOLATION: This method provides access to all children's data. Use IChildServiceCoordinator for administrative tasks only.")]
    ValueTask<IEnumerable<Child>> GetAllChildrenAsync();

    [Obsolete("SECURITY VIOLATION: This method processes multiple children's data simultaneously. Process each child in isolation instead.")]
    Task<string> AskQuestionAboutChildrenAsync(Dictionary<string, JObject> childrenWeekLetters, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack);

    // Process query for a specific child only - pass null to indicate no child context
    Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, Child? specificChild, ChatInterface chatInterface = ChatInterface.Slack);
}