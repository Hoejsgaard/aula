using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula;

public class DataManager : IDataManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly Config _config;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(1);

    public DataManager(IMemoryCache cache, Config config, ILoggerFactory loggerFactory)
    {
        _cache = cache;
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(DataManager));
    }

    public void CacheWeekLetter(Child child, JObject weekLetter)
    {
        var cacheKey = GetWeekLetterCacheKey(child);
        _cache.Set(cacheKey, weekLetter, _cacheExpiration);
        _logger.LogInformation("Cached week letter for {ChildName}", child.FirstName);
    }

    public JObject? GetWeekLetter(Child child)
    {
        var cacheKey = GetWeekLetterCacheKey(child);
        if (_cache.TryGetValue(cacheKey, out JObject? weekLetter))
        {
            _logger.LogInformation("Retrieved week letter from cache for {ChildName}", child.FirstName);
            return weekLetter;
        }

        _logger.LogInformation("No cached week letter found for {ChildName}", child.FirstName);
        return null;
    }

    public void CacheWeekSchedule(Child child, JObject weekSchedule)
    {
        var cacheKey = GetWeekScheduleCacheKey(child);
        _cache.Set(cacheKey, weekSchedule, _cacheExpiration);
        _logger.LogInformation("Cached week schedule for {ChildName}", child.FirstName);
    }

    public JObject? GetWeekSchedule(Child child)
    {
        var cacheKey = GetWeekScheduleCacheKey(child);
        if (_cache.TryGetValue(cacheKey, out JObject? weekSchedule))
        {
            _logger.LogInformation("Retrieved week schedule from cache for {ChildName}", child.FirstName);
            return weekSchedule;
        }

        _logger.LogInformation("No cached week schedule found for {ChildName}", child.FirstName);
        return null;
    }

    private string GetWeekLetterCacheKey(Child child)
    {
        return $"WeekLetter:{child.FirstName}:{child.LastName}";
    }

    private string GetWeekScheduleCacheKey(Child child)
    {
        return $"WeekSchedule:{child.FirstName}:{child.LastName}";
    }

    public IEnumerable<Child> GetChildren()
    {
        return _config.Children;
    }
}