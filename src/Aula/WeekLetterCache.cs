using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Aula.Configuration;

namespace Aula;

public class WeekLetterCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly Config _config;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromDays(365); // Week letters never change once published

    public WeekLetterCache(IMemoryCache cache, Config config, ILoggerFactory loggerFactory)
    {
        _cache = cache;
        _config = config;
        _logger = loggerFactory.CreateLogger(nameof(WeekLetterCache));
    }

    public virtual void CacheWeekLetter(Child child, int weekNumber, int year, JObject weekLetter)
    {
        var cacheKey = GetWeekLetterCacheKey(child, weekNumber, year);
        _cache.Set(cacheKey, weekLetter, _cacheExpiration);
        _logger.LogInformation("Cached week letter for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
    }

    public virtual JObject? GetWeekLetter(Child child, int weekNumber, int year)
    {
        var cacheKey = GetWeekLetterCacheKey(child, weekNumber, year);
        if (_cache.TryGetValue(cacheKey, out JObject? weekLetter))
        {
            _logger.LogInformation("Retrieved week letter from cache for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
            return weekLetter;
        }

        _logger.LogInformation("No cached week letter found for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
        return null;
    }

    public virtual void CacheWeekSchedule(Child child, int weekNumber, int year, JObject weekSchedule)
    {
        var cacheKey = GetWeekScheduleCacheKey(child, weekNumber, year);
        _cache.Set(cacheKey, weekSchedule, _cacheExpiration);
        _logger.LogInformation("Cached week schedule for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
    }

    public virtual JObject? GetWeekSchedule(Child child, int weekNumber, int year)
    {
        var cacheKey = GetWeekScheduleCacheKey(child, weekNumber, year);
        if (_cache.TryGetValue(cacheKey, out JObject? weekSchedule))
        {
            _logger.LogInformation("Retrieved week schedule from cache for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
            return weekSchedule;
        }

        _logger.LogInformation("No cached week schedule found for {ChildName} week {WeekNumber}/{Year}", child.FirstName, weekNumber, year);
        return null;
    }

    private string GetWeekLetterCacheKey(Child child, int weekNumber, int year)
    {
        return $"WeekLetter:{child.FirstName}:{child.LastName}:{weekNumber}:{year}";
    }

    private string GetWeekScheduleCacheKey(Child child, int weekNumber, int year)
    {
        return $"WeekSchedule:{child.FirstName}:{child.LastName}:{weekNumber}:{year}";
    }

    public IEnumerable<Child> GetChildren()
    {
        return _config.MinUddannelse.Children;
    }
}