using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Aula.Utilities;

/// <summary>
/// Utility methods for week letter operations
/// </summary>
public static class WeekLetterUtilities
{
    /// <summary>
    /// Gets the ISO 8601 week number for a given date according to Danish standards
    /// </summary>
    /// <param name="date">The date for which to calculate the week number</param>
    /// <returns>The ISO week number (1-53)</returns>
    /// <remarks>
    /// Uses System.Globalization.ISOWeek which follows ISO 8601 standard:
    /// - Week 1 is the first week with a Thursday in the new year
    /// - Weeks start on Monday
    /// - Used by Danish schools and MinUddannelse platform
    /// </remarks>
    public static int GetIsoWeekNumber(DateOnly date)
    {
        return System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
    }

    /// <summary>
    /// Computes SHA256 hash of content string for change detection
    /// </summary>
    /// <param name="content">The content to hash (cannot be null)</param>
    /// <returns>Uppercase hexadecimal string representation of SHA256 hash</returns>
    /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
    /// <remarks>
    /// Used to detect changes in week letter content to prevent duplicate posts.
    /// Returns a consistent hash for identical content, enabling efficient
    /// comparison without storing full content in memory.
    /// </remarks>
    public static string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Creates an empty week letter JSON object matching MinUddannelse API format
    /// </summary>
    /// <param name="weekNumber">The ISO week number (1-53) for the empty week letter</param>
    /// <returns>A JObject with the standard MinUddannelse week letter structure indicating no content</returns>
    /// <remarks>
    /// Used when:
    /// - No week letter is available in the database
    /// - Live fetch is not allowed
    /// - Authentication fails
    /// - API returns no content
    ///
    /// The returned JSON follows MinUddannelse API structure with a placeholder message
    /// indicating no week notes have been written.
    /// </remarks>
    public static JObject CreateEmptyWeekLetter(int weekNumber)
    {
        return new JObject
        {
            ["errorMessage"] = null,
            ["ugebreve"] = new JArray(new JObject
            {
                ["klasseNavn"] = "N/A",
                ["uge"] = weekNumber.ToString(),
                ["indhold"] = "Der er ikke skrevet nogen ugenoter til denne uge"
            }),
            ["klasser"] = new JArray()
        };
    }
}
