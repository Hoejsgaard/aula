using Newtonsoft.Json.Linq;

namespace Aula;

public interface IAgentService
{
    Task<bool> LoginAsync();
    Task<JObject> GetWeekLetterAsync(Child child, DateOnly date, bool useCache = true);
    Task<JObject> GetWeekScheduleAsync(Child child, DateOnly date, bool useCache = true);
    
    // OpenAI-related methods
    Task<string> SummarizeWeekLetterAsync(Child child, DateOnly date);
    Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question);
    Task<JObject> ExtractKeyInformationFromWeekLetterAsync(Child child, DateOnly date);
}