using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using Microsoft.Extensions.Logging;
using Supabase;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinUddannelse.Repositories;

public class WeekLetterRepository : IWeekLetterRepository
{
    private readonly Supabase.Client _supabase;
    private readonly ILogger _logger;

    public WeekLetterRepository(Supabase.Client supabase, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(supabase);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _supabase = supabase;
        _logger = loggerFactory.CreateLogger<WeekLetterRepository>();
    }

    public async Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        var result = await _supabase
            .From<PostedLetter>()
            .Select("id")
            .Where(pl => pl.ChildName == childName && pl.WeekNumber == weekNumber && pl.Year == year)
            .Get();

        return result.Models.Count > 0;
    }

    public async Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var postedLetter = new PostedLetter
        {
            ChildName = childName,
            WeekNumber = weekNumber,
            Year = year,
            ContentHash = contentHash,
            PostedToSlack = postedToSlack,
            PostedToTelegram = postedToTelegram
        };

        await _supabase
            .From<PostedLetter>()
            .Insert(postedLetter);

        _logger.LogInformation("Marked week letter as posted for {ChildName}, week {WeekNumber}, year {Year}",
            childName, weekNumber, year);
    }

    public async Task StoreWeekLetterAsync(string childName, int weekNumber, int year, string contentHash, string rawContent, bool postedToSlack = false, bool postedToTelegram = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent);

        var existingRecordQuery = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Get();

        var existingRecord = existingRecordQuery.Models.FirstOrDefault();

        if (existingRecord != null)
        {
            existingRecord.ContentHash = contentHash;
            existingRecord.RawContent = rawContent;
            existingRecord.PostedToSlack = postedToSlack;
            existingRecord.PostedToTelegram = postedToTelegram;

            await _supabase
                .From<PostedLetter>()
                .Update(existingRecord);

            _logger.LogInformation("Updated existing week letter for {ChildName}, week {WeekNumber}/{Year}",
                childName, weekNumber, year);
        }
        else
        {
            var postedLetter = new PostedLetter
            {
                ChildName = childName,
                WeekNumber = weekNumber,
                Year = year,
                ContentHash = contentHash,
                RawContent = rawContent,
                PostedToSlack = postedToSlack,
                PostedToTelegram = postedToTelegram
            };

            await _supabase
                .From<PostedLetter>()
                .Insert(postedLetter);

            _logger.LogInformation("Inserted new week letter for {ChildName}, week {WeekNumber}/{Year}",
                childName, weekNumber, year);
        }
    }

    public async Task<string?> GetStoredWeekLetterAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        var resultQuery = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Get();

        var result = resultQuery.Models.FirstOrDefault();

        return result?.RawContent;
    }

    public async Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null)
    {
        if (!string.IsNullOrEmpty(childName))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        }
        if (year.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(year.Value, 2000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(year.Value, 2100);
        }

        var query = _supabase.From<PostedLetter>().Select("*");

        if (!string.IsNullOrEmpty(childName))
        {
            query = query.Where(p => p.ChildName == childName);
        }

        if (year.HasValue)
        {
            query = query.Where(p => p.Year == year.Value);
        }

        var result = await query.Get();

        return result.Models.Select(p => new StoredWeekLetter
        {
            ChildName = p.ChildName,
            WeekNumber = p.WeekNumber,
            Year = p.Year,
            RawContent = p.RawContent,
            PostedAt = p.PostedAt
        }).ToList();
    }

    public async Task<StoredWeekLetter?> GetLatestStoredWeekLetterAsync(string childName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);

        var resultQuery = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.RawContent != null)
            .Order("year", Supabase.Postgrest.Constants.Ordering.Descending)
            .Order("week_number", Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Get();

        var result = resultQuery.Models.FirstOrDefault();

        if (result == null) return null;

        return new StoredWeekLetter
        {
            ChildName = result.ChildName,
            WeekNumber = result.WeekNumber,
            Year = result.Year,
            RawContent = result.RawContent,
            PostedAt = result.PostedAt
        };
    }

    public async Task DeleteWeekLetterAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Delete();

        _logger.LogInformation("Deleted week letter for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task<PostedLetter?> GetPostedLetterByHashAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        var resultQuery = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Limit(1)
            .Get();

        return resultQuery.Models.FirstOrDefault();
    }

    public async Task MarkAutoRemindersExtractedAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        var existingRecord = await GetPostedLetterByHashAsync(childName, weekNumber, year);
        if (existingRecord == null)
        {
            _logger.LogWarning("Cannot mark auto reminders extracted - no posted letter found for {ChildName} week {WeekNumber}/{Year}",
                childName, weekNumber, year);
            return;
        }

        existingRecord.AutoRemindersExtracted = true;
        existingRecord.AutoRemindersLastUpdated = DateTime.UtcNow;

        await existingRecord.Update<PostedLetter>();

        _logger.LogInformation("Marked auto reminders extracted for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }

    public async Task ResetAutoRemindersExtractedAsync(string childName, int weekNumber, int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(childName);
        ArgumentOutOfRangeException.ThrowIfLessThan(weekNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weekNumber, 53);
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100);

        var existingRecord = await GetPostedLetterByHashAsync(childName, weekNumber, year);
        if (existingRecord == null)
        {
            _logger.LogWarning("Cannot reset auto reminders extracted - no posted letter found for {ChildName} week {WeekNumber}/{Year}",
                childName, weekNumber, year);
            return;
        }

        existingRecord.AutoRemindersExtracted = false;
        existingRecord.AutoRemindersLastUpdated = null;

        await existingRecord.Update<PostedLetter>();

        _logger.LogInformation("Reset auto reminders extracted flag for {ChildName}, week {WeekNumber}/{Year}",
            childName, weekNumber, year);
    }
}
