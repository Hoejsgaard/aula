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
    /// Gets the ISO week number for a given date
    /// </summary>
    public static int GetIsoWeekNumber(DateOnly date)
    {
        return System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
    }

    /// <summary>
    /// Computes SHA256 hash of content string
    /// </summary>
    public static string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Creates an empty week letter JSON object
    /// </summary>
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
