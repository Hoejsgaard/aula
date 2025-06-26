using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula;

public class AgentService : IAgentService
{
    private readonly IMinUddannelseClient _minUddannelseClient;
    private readonly IDataManager _dataManager;
    private readonly IOpenAiService _openAiService;
    private readonly ILogger _logger;
    private bool _isLoggedIn;

    public AgentService(
        IMinUddannelseClient minUddannelseClient,
        IDataManager dataManager,
        IOpenAiService openAiService,
        ILoggerFactory loggerFactory)
    {
        _minUddannelseClient = minUddannelseClient;
        _dataManager = dataManager;
        _openAiService = openAiService;
        _logger = loggerFactory.CreateLogger(nameof(AgentService));
    }

    public async Task<bool> LoginAsync()
    {
        _logger.LogInformation("Logging in to MinUddannelse");
        _isLoggedIn = await _minUddannelseClient.LoginAsync();
        return _isLoggedIn;
    }

    public async Task<JObject> GetWeekLetterAsync(Child child, DateOnly date, bool useCache = true)
    {
        if (!_isLoggedIn)
        {
            _logger.LogWarning("Not logged in. Attempting to login before getting week letter");
            await LoginAsync();
        }

        if (useCache)
        {
            var cachedWeekLetter = _dataManager.GetWeekLetter(child);
            if (cachedWeekLetter != null)
            {
                _logger.LogInformation("Returning cached week letter for {ChildName}", child.FirstName);
                
                // Add child name to the week letter object if not already present
                if (cachedWeekLetter["child"] == null)
                {
                    cachedWeekLetter["child"] = child.FirstName;
                }
                
                return cachedWeekLetter;
            }
        }

        _logger.LogInformation("Getting week letter for {ChildName} for date {Date}", child.FirstName, date);
        var weekLetter = await _minUddannelseClient.GetWeekLetter(child, date);
        
        // Add child name to the week letter object
        weekLetter["child"] = child.FirstName;

        _dataManager.CacheWeekLetter(child, weekLetter);

        return weekLetter;
    }

    public async Task<JObject> GetWeekScheduleAsync(Child child, DateOnly date, bool useCache = true)
    {
        if (!_isLoggedIn)
        {
            _logger.LogWarning("Not logged in. Attempting to login before getting week schedule");
            await LoginAsync();
        }

        if (useCache)
        {
            var cachedWeekSchedule = _dataManager.GetWeekSchedule(child);
            if (cachedWeekSchedule != null)
            {
                _logger.LogInformation("Returning cached week schedule for {ChildName}", child.FirstName);
                return cachedWeekSchedule;
            }
        }

        _logger.LogInformation("Getting week schedule for {ChildName} for date {Date}", child.FirstName, date);
        var weekSchedule = await _minUddannelseClient.GetWeekSchedule(child, date);

        _dataManager.CacheWeekSchedule(child, weekSchedule);

        return weekSchedule;
    }

    public async Task<string> SummarizeWeekLetterAsync(Child child, DateOnly date)
    {
        _logger.LogInformation("Summarizing week letter for {ChildName} for date {Date}", child.FirstName, date);
        
        var weekLetter = await GetWeekLetterAsync(child, date);
        return await _openAiService.SummarizeWeekLetterAsync(weekLetter);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question)
    {
        _logger.LogInformation("Asking question about week letter for {ChildName} for date {Date}: {Question}", 
            child.FirstName, date, question);
        
        var weekLetter = await GetWeekLetterAsync(child, date);
        return await _openAiService.AskQuestionAboutWeekLetterAsync(weekLetter, question);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, string? contextKey)
    {
        _logger.LogInformation("Asking question about week letter for {ChildName} for date {Date} with context {ContextKey}: {Question}", 
            child.FirstName, date, contextKey, question);
        
        var weekLetter = await GetWeekLetterAsync(child, date);
        return await _openAiService.AskQuestionAboutWeekLetterAsync(weekLetter, question, contextKey);
    }

    public async Task<JObject> ExtractKeyInformationFromWeekLetterAsync(Child child, DateOnly date)
    {
        _logger.LogInformation("Extracting key information from week letter for {ChildName} for date {Date}", 
            child.FirstName, date);
        
        var weekLetter = await GetWeekLetterAsync(child, date);
        return await _openAiService.ExtractKeyInformationAsync(weekLetter);
    }
}