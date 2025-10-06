using MinUddannelse.Configuration;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public class RetryTrackingRepository : IRetryTrackingRepository
{
    private readonly Supabase.Client _supabase;
    private readonly ILogger _logger;
    private readonly Config _config;

    public RetryTrackingRepository(Supabase.Client supabase, ILoggerFactory loggerFactory, Config config)
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
            .Filter("child_name", Constants.Operator.Equals, childName)
            .Filter("week_number", Constants.Operator.Equals, weekNumber)
            .Filter("year", Constants.Operator.Equals, year)
            .Get();

        var retryAttempt = result.Models.FirstOrDefault();
        return retryAttempt?.AttemptCount ?? 0;
    }

    public async Task<bool> IncrementRetryAttemptAsync(string childName, int weekNumber, int year)
    {
        // First, try to get existing retry attempt
        var existing = await _supabase
            .From<RetryAttempt>()
            .Select("*")
            .Filter("child_name", Constants.Operator.Equals, childName)
            .Filter("week_number", Constants.Operator.Equals, weekNumber)
            .Filter("year", Constants.Operator.Equals, year)
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

            return false; // Not the first attempt
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

            return true; // This is the first attempt
        }
    }

    public async Task MarkRetryAsSuccessfulAsync(string childName, int weekNumber, int year)
    {
        // Delete the retry attempt record since it's no longer needed
        await _supabase
            .From<RetryAttempt>()
            .Filter("child_name", Constants.Operator.Equals, childName)
            .Filter("week_number", Constants.Operator.Equals, weekNumber)
            .Filter("year", Constants.Operator.Equals, year)
            .Delete();

        _logger.LogInformation("Marked retry as successful and removed tracking for {ChildName} week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task<List<RetryAttempt>> GetPendingRetriesAsync()
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var result = await _supabase
            .From<RetryAttempt>()
            .Select("*")
            .Filter("next_attempt", Constants.Operator.LessThanOrEqual, now)
            .Filter("attempt_count", Constants.Operator.LessThan, 24) // Use MaxAttempts from config
            .Get();

        _logger.LogInformation("Found {Count} pending retries", result.Models.Count);
        return result.Models;
    }
}
