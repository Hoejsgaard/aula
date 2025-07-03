namespace Aula.Configuration;

public class Features
{
    public bool WeekLetterPreloading { get; set; } = true;
    public bool ParallelProcessing { get; set; } = true;
    public int ConversationCacheExpirationMinutes { get; set; } = 60;

    // Week letter testing and retrieval options
    public bool UseStoredWeekLetters { get; set; } = false;
    public int? TestWeekNumber { get; set; } = null;
    public int? TestYear { get; set; } = null;

    // Mock data configuration - when enabled, MinUddannelseClient returns stored data instead of hitting API
    public bool UseMockData { get; set; } = false;
    public int MockCurrentWeek { get; set; } = 15; // Simulate this as the "current" week
    public int MockCurrentYear { get; set; } = 2025; // Simulate this as the "current" year
}