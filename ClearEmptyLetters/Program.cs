using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Aula.Services;
using Aula.Configuration;
using Newtonsoft.Json.Linq;

public class ClearEmptyLetters
{
    public static async Task Main(string[] args)
    {
        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<ClearEmptyLetters>();

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath("/mnt/d/git/aula/src/Aula")
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var config = configuration.Get<Config>() ?? throw new Exception("Configuration is null");

        // Create Supabase service
        var supabaseService = new SupabaseService(config, loggerFactory);
        await supabaseService.InitializeAsync();

        logger.LogInformation("🔍 Looking for TestChild1's week letters in database...");

        try
        {
            // Get all stored week letters for TestChild1
            var storedLetters = await supabaseService.GetStoredWeekLettersAsync(TestChild1);

            logger.LogInformation("Found {Count} week letters for TestChild1", storedLetters.Count);

            int emptyCount = 0;

            foreach (var letter in storedLetters)
            {
                logger.LogInformation("Week {Week}/{Year}: Content length = {Length} chars",
                    letter.WeekNumber, letter.Year, letter.RawContent?.Length ?? 0);

                if (string.IsNullOrEmpty(letter.RawContent))
                {
                    logger.LogWarning("  ❌ Empty content!");
                    emptyCount++;
                    continue;
                }

                try
                {
                    var json = JObject.Parse(letter.RawContent);

                    // Check if it's actually empty (no ugebreve or empty ugebreve)
                    bool isEmpty = !json.HasValues ||
                                  (json["ugebreve"] != null && !json["ugebreve"].HasValues) ||
                                  (json["errorMessage"] != null && json["ugebreve"] == null);

                    if (isEmpty)
                    {
                        logger.LogWarning("  ❌ JSON is empty or has no content!");
                        emptyCount++;

                        // Delete this empty entry from database
                        logger.LogInformation("  🗑️ Deleting week {Week}/{Year} for TestChild1",
                            letter.WeekNumber, letter.Year);

                        await supabaseService.DeleteWeekLetterAsync(TestChild1, letter.WeekNumber, letter.Year);
                        logger.LogInformation("    ✅ Deleted successfully");
                    }
                    else
                    {
                        var ugebreve = json["ugebreve"];
                        if (ugebreve != null && ugebreve.HasValues)
                        {
                            logger.LogInformation("  ✅ Has content: {Count} week letters",
                                ugebreve.Count());

                            var first = ugebreve.First();
                            if (first != null && first["indhold"] != null)
                            {
                                var content = first["indhold"].ToString();
                                logger.LogInformation("    Content preview: {Preview}",
                                    content.Substring(0, Math.Min(100, content.Length)));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("  ❌ Failed to parse JSON: {Error}", ex.Message);
                    emptyCount++;
                }
            }

            logger.LogInformation("");
            logger.LogInformation("Summary:");
            logger.LogInformation("  Total letters: {Total}", storedLetters.Count);
            logger.LogInformation("  Empty letters: {Empty}", emptyCount);
            logger.LogInformation("  Valid letters: {Valid}", storedLetters.Count - emptyCount);

            if (emptyCount > 0)
            {
                logger.LogWarning("");
                logger.LogWarning("✅ Deleted {Count} empty week letters for TestChild1!", emptyCount);
                logger.LogWarning("The system can now fetch fresh data from MinUddannelse.");
            }
            else
            {
                logger.LogInformation("✅ No empty week letters found for TestChild1");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check week letters");
        }
    }
}