using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Content.WeekLetters;

public static class WeekLetterUtilities
{
    public static int GetIsoWeekNumber(DateOnly date)
    {
        return System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
    }

    public static string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

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
