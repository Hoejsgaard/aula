using Microsoft.Extensions.Logging;
using Supabase;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Services;

public class WeekLetterRepository : IWeekLetterRepository
{
    private readonly Client _supabase;
    private readonly ILogger _logger;

    public WeekLetterRepository(Client supabase, ILoggerFactory loggerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _logger = loggerFactory.CreateLogger<WeekLetterRepository>();
    }

    public async Task<bool> HasWeekLetterBeenPostedAsync(string childName, int weekNumber, int year)
    {
        var result = await _supabase
            .From<PostedLetter>()
            .Select("id")
            .Where(pl => pl.ChildName == childName && pl.WeekNumber == weekNumber && pl.Year == year)
            .Get();

        return result.Models.Any();
    }

    public async Task MarkWeekLetterAsPostedAsync(string childName, int weekNumber, int year, string contentHash, bool postedToSlack = false, bool postedToTelegram = false)
    {
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
        // Check if record already exists
        var existingRecord = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Single();

        if (existingRecord != null)
        {
            // Update existing record
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
            // Insert new record
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
        var result = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.WeekNumber == weekNumber)
            .Where(p => p.Year == year)
            .Single();

        return result?.RawContent;
    }

    public async Task<List<StoredWeekLetter>> GetStoredWeekLettersAsync(string? childName = null, int? year = null)
    {
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
        var result = await _supabase
            .From<PostedLetter>()
            .Where(p => p.ChildName == childName)
            .Where(p => p.RawContent != null)
            .Order("year", Supabase.Postgrest.Constants.Ordering.Descending)
            .Order("week_number", Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Single();

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
}