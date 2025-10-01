using Newtonsoft.Json.Linq;

namespace Aula.Authentication;

/// <summary>
/// Provides authentication and data retrieval services for the current child context.
/// This interface enables child-aware operations without requiring Child parameters.
/// </summary>
public interface IChildAuthenticationService
{
    /// <summary>
    /// Authenticates the child in the current context.
    /// Uses credentials from the child's configuration.
    /// </summary>
    /// <returns>True if authentication succeeded, false otherwise</returns>
    Task<bool> AuthenticateAsync();

    /// <summary>
    /// Checks if the current child context is authenticated.
    /// </summary>
    /// <returns>True if authenticated, false otherwise</returns>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Gets the week letter for the current child context.
    /// </summary>
    /// <param name="targetDate">The date to get the week letter for</param>
    /// <param name="allowLiveFetch">Whether to allow fetching from the live service</param>
    /// <returns>The week letter data, or null if not found</returns>
    Task<JObject?> GetWeekLetterAsync(DateOnly targetDate, bool allowLiveFetch = false);

    /// <summary>
    /// Gets the week schedule for the current child context.
    /// </summary>
    /// <param name="targetDate">The date to get the schedule for</param>
    /// <returns>The schedule data, or null if not found</returns>
    Task<JObject?> GetWeekScheduleAsync(DateOnly targetDate);

    /// <summary>
    /// Gets stored week letter for the current child context.
    /// </summary>
    /// <param name="weekNumber">The week number</param>
    /// <param name="year">The year</param>
    /// <returns>The stored week letter, or null if not found</returns>
    Task<JObject?> GetStoredWeekLetterAsync(int weekNumber, int year);

    /// <summary>
    /// Invalidates the authentication session for the current child context.
    /// </summary>
    Task InvalidateSessionAsync();

    /// <summary>
    /// Gets the session identifier for the current child context.
    /// Used for tracking and audit purposes.
    /// </summary>
    string GetSessionId();

    /// <summary>
    /// Gets the last authentication timestamp for the current child context.
    /// </summary>
    DateTimeOffset? GetLastAuthenticationTime();
}
