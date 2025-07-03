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
}