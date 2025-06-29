using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula;

public static class WeekLetterContentExtractor
{
    public static string ExtractContent(JObject weekLetter, ILogger? logger = null)
    {
        try
        {
            var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(content))
            {
                logger?.LogWarning("Week letter content is empty");
            }

            return content;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error extracting week letter content");
            return "";
        }
    }

    public static string ExtractContent(dynamic weekLetter, ILogger? logger = null)
    {
        try
        {
            var ugebreve = weekLetter?["ugebreve"];
            if (ugebreve is JArray ugebreveArray && ugebreveArray.Count > 0)
            {
                return ugebreveArray[0]?["indhold"]?.ToString() ?? "";
            }
            return "";
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error extracting week letter content from dynamic object");
            return "";
        }
    }
}