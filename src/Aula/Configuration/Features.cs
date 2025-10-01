namespace Aula.Configuration;

public class Features
{
    public bool WeekLetterPreloading { get; set; } = true;
    public bool ParallelProcessing { get; set; } = true;
    public int ConversationCacheExpirationMinutes { get; set; } = 60;

    // Week letter testing and retrieval options
    public bool UseStoredWeekLetters { get; set; }
    public int? TestWeekNumber { get; set; }
    public int? TestYear { get; set; }
    public bool SeedHistoricalData { get; set; }

    // Mock data configuration - when enabled, MinUddannelseClient returns stored data instead of hitting API
    public bool UseMockData { get; set; }
    public int MockCurrentWeek { get; set; } = 15; // Simulate this as the "current" week
    public int MockCurrentYear { get; set; } = 2025; // Simulate this as the "current" year

    // Startup behavior
    public bool PostWeekLettersOnStartup { get; set; } // Whether to post week letters when the service starts
    public bool PreloadWeekLettersOnStartup { get; set; } = true; // Whether to preload recent week letters on startup
    public int WeeksToPreload { get; set; } = 3; // Number of weeks to preload (current + past weeks)
}
