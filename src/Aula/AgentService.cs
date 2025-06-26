using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula;

public class AgentService : IAgentService
{
    private readonly IMinUddannelseClient _minUddannelseClient;
    private readonly IDataManager _dataManager;
    private readonly ILogger _logger;
    private bool _isLoggedIn;

    public AgentService(
        IMinUddannelseClient minUddannelseClient,
        IDataManager dataManager,
        ILoggerFactory loggerFactory)
    {
        _minUddannelseClient = minUddannelseClient;
        _dataManager = dataManager;
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
                return cachedWeekLetter;
            }
        }

        _logger.LogInformation("Getting week letter for {ChildName} for date {Date}", child.FirstName, date);
        var weekLetter = await _minUddannelseClient.GetWeekLetter(child, date);

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
}