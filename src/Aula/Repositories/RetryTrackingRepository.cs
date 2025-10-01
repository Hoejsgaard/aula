using Aula.Configuration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Repositories;

public class RetryTrackingRepository : IRetryTrackingRepository
{
    private readonly Client _supabase;
    private readonly ILogger _logger;
    private readonly Config _config;

    public RetryTrackingRepository(Client supabase, ILoggerFactory loggerFactory, Config config)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _logger = loggerFactory.CreateLogger<RetryTrackingRepository>();
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<int> GetRetryAttemptsAsync(string childName, int weekNumber, int year)
    {
        var result = await _supabase
            .From<RetryAttempt>()
            .Select("attempt_count")
            .Where(ra => ra.ChildName == childName && ra.WeekNumber == weekNumber && ra.Year == year)
            .Get();

        var retryAttempt = result.Models.FirstOrDefault();
        return retryAttempt?.AttemptCount ?? 0;
    }

    public async Task IncrementRetryAttemptAsync(string childName, int weekNumber, int year)
    {
        // First, try to get existing retry attempt
        var existing = await _supabase
            .From<RetryAttempt>()
            .Select("*")
            .Where(ra => ra.ChildName == childName && ra.WeekNumber == weekNumber && ra.Year == year)
            .Get();

        if (existing.Models.Count > 0)
        {
            // Increment existing attempt count
            var retryAttempt = existing.Models.First();
            retryAttempt.AttemptCount += 1;
            retryAttempt.LastAttempt = DateTime.UtcNow;

            // Use configured retry hours
            var retryHours = _config.WeekLetter.RetryIntervalHours;
            retryAttempt.NextAttempt = DateTime.UtcNow.AddHours(retryHours);

            await _supabase
                .From<RetryAttempt>()
                .Update(retryAttempt);

            _logger.LogInformation("Incremented retry attempt for {ChildName} week {WeekNumber}/{Year} to {Count}",
                childName, weekNumber, year, retryAttempt.AttemptCount);
        }
        else
        {
            // Use configured retry settings
            var retryHours = _config.WeekLetter.RetryIntervalHours;
            var maxRetryHours = _config.WeekLetter.MaxRetryDurationHours;
            var maxAttempts = maxRetryHours / retryHours; // Calculate max attempts based on retry duration

            // Create new retry attempt record
            var newRetryAttempt = new RetryAttempt
            {
                ChildName = childName,
                WeekNumber = weekNumber,
                Year = year,
                AttemptCount = 1,
                LastAttempt = DateTime.UtcNow,
                NextAttempt = DateTime.UtcNow.AddHours(retryHours),
                MaxAttempts = maxAttempts
            };

            await _supabase
                .From<RetryAttempt>()
                .Insert(newRetryAttempt);

            _logger.LogInformation("Created first retry attempt for {ChildName} week {WeekNumber}/{Year}",
                childName, weekNumber, year);
        }
    }

    public async Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year)
    {
        // Delete the retry attempt record since it's no longer needed
        await _supabase
            .From<RetryAttempt>()
            .Where(ra => ra.ChildName == childName && ra.WeekNumber == weekNumber && ra.Year == year)
            .Delete();

        _logger.LogInformation("Marked retry as successful and removed tracking for {ChildName} week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }
}
