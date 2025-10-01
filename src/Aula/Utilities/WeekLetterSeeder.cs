using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Aula.Services;

namespace Aula.Utilities;

public class WeekLetterSeeder : IWeekLetterSeeder
{
    private readonly ISupabaseService _supabaseService;
    private readonly ILogger _logger;

    public WeekLetterSeeder(ISupabaseService supabaseService, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(supabaseService);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _supabaseService = supabaseService;
        _logger = loggerFactory.CreateLogger<WeekLetterSeeder>();
    }

    public async Task SeedTestDataAsync()
    {
        _logger.LogInformation("Seeding test week letter data");

        var testData = new[]
        {
            new
            {
                ChildName = "Emma",
                WeekNumber = 20,
                Year = 2024,
                ClassName = "1.A",
                Content = "Kære forældre,\n\nDenne uge har vi arbejdet med matematik og dansk. Husk at tage madpakke med i morgen til udflugten.\n\nVenlig hilsen\nLæreren"
            },
            new
            {
                ChildName = "Lucas",
                WeekNumber = 21,
                Year = 2024,
                ClassName = "3.B",
                Content = "Hej forældre,\n\nI denne uge har klassen arbejdet med naturfag og historie. Fredag har vi sports dag - husk sportstøj!\n\nMvh\nKlasselæreren"
            },
            new
            {
                ChildName = "Emma",
                WeekNumber = 22,
                Year = 2024,
                ClassName = "1.A",
                Content = "Kære alle,\n\nEn spændende uge med bogstart og matematik. Husk aflevering af tegninger senest onsdag.\n\nKh\nLæreren"
            }
        };

        foreach (var data in testData)
        {
            await SeedWeekLetterAsync(data.ChildName, data.WeekNumber, data.Year, data.Content, data.ClassName);
        }

        _logger.LogInformation("Completed seeding {Count} test week letters", testData.Length);
    }

    public async Task SeedWeekLetterAsync(string childName, int weekNumber, int year, string content, string? className = null)
    {
        try
        {
            // Create a mock week letter JSON structure that matches MinUddannelse format
            var weekLetterJson = new JObject
            {
                ["ugebreve"] = new JArray
                {
                    new JObject
                    {
                        ["klasseNavn"] = className ?? "Test Class",
                        ["uge"] = weekNumber.ToString(),
                        ["indhold"] = content
                    }
                },
                ["child"] = childName
            };

            var contentHash = ComputeContentHash(content);
            var rawContent = weekLetterJson.ToString();

            // Check if already exists
            var existingContent = await _supabaseService.GetStoredWeekLetterAsync(childName, weekNumber, year);
            if (!string.IsNullOrEmpty(existingContent))
            {
                _logger.LogInformation("Week letter for {ChildName}, week {WeekNumber}/{Year} already exists - skipping",
                    childName, weekNumber, year);
                return;
            }

            await _supabaseService.StoreWeekLetterAsync(childName, weekNumber, year, contentHash, rawContent, false, false);

            _logger.LogInformation("Seeded week letter for {ChildName}, week {WeekNumber}/{Year}",
                childName, weekNumber, year);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding week letter for {ChildName}, week {WeekNumber}/{Year}",
                childName, weekNumber, year);
        }
    }

    private static string ComputeContentHash(string content)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
