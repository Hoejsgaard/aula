using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Aula.Configuration;
using Aula.Integration;
using Newtonsoft.Json.Linq;

namespace Aula.Services;

public class HistoricalDataSeeder : IHistoricalDataSeeder
{
    private readonly ILogger _logger;
    private readonly IAgentService _agentService;
    private readonly ISupabaseService _supabaseService;
    private readonly Config _config;

    public HistoricalDataSeeder(
        ILoggerFactory loggerFactory,
        IAgentService agentService,
        ISupabaseService supabaseService,
        Config config)
    {
        _logger = loggerFactory.CreateLogger(nameof(HistoricalDataSeeder));
        _agentService = agentService;
        _supabaseService = supabaseService;
        _config = config;
    }

    /// <summary>
    /// ONE-OFF METHOD: Populates database with week letters from the past 8 weeks
    /// This helps with testing during summer holidays when no fresh week letters are available
    /// Remove this method once historical data has been seeded
    /// </summary>
    public async Task SeedHistoricalWeekLettersAsync()
    {
        try
        {
            _logger.LogInformation("📅 Fetching historical week letters from the past 8 weeks (weeks 19-26)");

            // Login to MinUddannelse
            var loginSuccess = await _agentService.LoginAsync();
            if (!loginSuccess)
            {
                _logger.LogWarning("Failed to login to MinUddannelse - skipping historical data population");
                return;
            }

            var allChildren = await _agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                _logger.LogWarning("No children configured - skipping historical data population");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            _logger.LogInformation("📅 Today is: {Today} (calculated from DateTime.Today: {DateTimeToday})", today, DateTime.Today);
            var successCount = 0;
            var totalAttempts = 0;

            // Go back 1-8 weeks from today to find recent school weeks
            for (int weeksBack = 1; weeksBack <= 8; weeksBack++)
            {
                var targetDate = today.AddDays(-7 * weeksBack);
                var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(targetDate.ToDateTime(TimeOnly.MinValue));
                var year = targetDate.Year;

                _logger.LogInformation("📆 Processing week {WeekNumber}/{Year} (date: {Date})", weekNumber, year, targetDate);

                foreach (var child in allChildren)
                {
                    totalAttempts++;
                    _logger.LogInformation("🔍 Processing child: '{ChildFirstName}' (Length: {Length} chars)", child.FirstName, child.FirstName.Length);

                    try
                    {
                        // Check if we already have this week letter stored
                        var childNameForStorage = child.FirstName;
                        _logger.LogInformation("💾 Checking storage for child: '{ChildName}'", childNameForStorage);
                        var existingContent = await _supabaseService.GetStoredWeekLetterAsync(childNameForStorage, weekNumber, year);
                        if (!string.IsNullOrEmpty(existingContent))
                        {
                            _logger.LogInformation("✅ Week letter for {ChildName} week {WeekNumber}/{Year} already exists - skipping",
                                child.FirstName, weekNumber, year);
                            successCount++;
                            continue;
                        }

                        // Try to fetch week letter for this historical date - DISABLE MOCK MODE temporarily
                        var originalUseMockData = _config.Features.UseMockData;
                        JObject? weekLetter;
                        try
                        {
                            _config.Features.UseMockData = false; // Force real API call
                            weekLetter = await _agentService.GetWeekLetterAsync(child, targetDate, false);
                        }
                        finally
                        {
                            _config.Features.UseMockData = originalUseMockData; // Restore original setting
                        }

                        if (weekLetter != null)
                        {
                            // Check if it has actual content (not just the "no week letter" placeholder)
                            var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(content) && !content.Contains("Der er ikke skrevet nogen ugenoter"))
                            {
                                // Store the week letter
                                var contentHash = ComputeContentHash(weekLetter.ToString());
                                await _supabaseService.StoreWeekLetterAsync(
                                    childNameForStorage,
                                    weekNumber,
                                    year,
                                    contentHash,
                                    weekLetter.ToString(),
                                    false,
                                    false);

                                successCount++;
                                _logger.LogInformation("✅ Stored week letter for {ChildName} week {WeekNumber}/{Year} ({ContentLength} chars)",
                                    child.FirstName, weekNumber, year, content.Length);
                            }
                            else
                            {
                                _logger.LogInformation("⚠️ Week letter for {ChildName} week {WeekNumber}/{Year} has no content - skipping",
                                    child.FirstName, weekNumber, year);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("⚠️ No week letter available for {ChildName} week {WeekNumber}/{Year}",
                                child.FirstName, weekNumber, year);
                        }

                        // Small delay to be respectful to the API
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "❌ Error fetching week letter for {ChildName} week {WeekNumber}/{Year}",
                            child.FirstName, weekNumber, year);
                    }
                }
            }

            _logger.LogInformation("🎉 Historical week letter population complete: {SuccessCount}/{TotalAttempts} successful",
                successCount, totalAttempts);

            if (successCount > 0)
            {
                _logger.LogInformation("📊 You can now test with stored week letters by setting Features.UseStoredWeekLetters = true");
                _logger.LogInformation("🔧 Remember to remove this PopulateHistoricalWeekLetters method once you're done seeding data");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during historical week letter population");
        }
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}