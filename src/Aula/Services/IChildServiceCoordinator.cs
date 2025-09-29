using Aula.Configuration;

namespace Aula.Services;

/// <summary>
/// Coordinates all child-specific operations across the application.
/// Acts as the main entry point for child-scoped operations.
/// </summary>
public interface IChildServiceCoordinator
{
    /// <summary>
    /// Preloads week letters for all children.
    /// </summary>
    Task PreloadWeekLettersForAllChildrenAsync();

    /// <summary>
    /// Posts week letters to configured channels for all children.
    /// </summary>
    Task PostWeekLettersToChannelsAsync();

    /// <summary>
    /// Fetches and stores the current week letter for a specific child.
    /// </summary>
    Task<bool> FetchWeekLetterForChildAsync(Child child, DateOnly date);

    /// <summary>
    /// Fetches and stores week letters for all children.
    /// </summary>
    Task FetchWeekLettersForAllChildrenAsync(DateOnly date);

    /// <summary>
    /// Processes scheduled tasks for a specific child.
    /// </summary>
    Task ProcessScheduledTasksForChildAsync(Child child);

    /// <summary>
    /// Processes scheduled tasks for all children.
    /// </summary>
    Task ProcessScheduledTasksForAllChildrenAsync();

    /// <summary>
    /// Sends a reminder to a specific child.
    /// </summary>
    Task<bool> SendReminderToChildAsync(Child child, string reminderMessage);

    /// <summary>
    /// Processes an AI query for a specific child.
    /// </summary>
    Task<string> ProcessAiQueryForChildAsync(Child child, string query);

    /// <summary>
    /// Seeds historical data for a specific child.
    /// </summary>
    Task SeedHistoricalDataForChildAsync(Child child, int weeksBack = 12);

    /// <summary>
    /// Seeds historical data for all children.
    /// </summary>
    Task SeedHistoricalDataForAllChildrenAsync(int weeksBack = 12);

    /// <summary>
    /// Gets the next scheduled task time for a child.
    /// </summary>
    Task<DateTime?> GetNextScheduledTaskTimeForChildAsync(Child child);

    /// <summary>
    /// Validates that all child-aware services are properly configured.
    /// </summary>
    Task<bool> ValidateChildServicesAsync();

    /// <summary>
    /// Gets health status for all child-aware services.
    /// </summary>
    Task<Dictionary<string, bool>> GetChildServicesHealthAsync();
}