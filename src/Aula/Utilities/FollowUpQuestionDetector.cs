using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Aula.Configuration;

namespace Aula.Utilities;

public static class FollowUpQuestionDetector
{
    private static readonly FrozenSet<string> EnglishFollowUpPhrases = new[]
    {
        "what about", "how about", "and what", "also", "and?", "likewise"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> DanishFollowUpPhrases = new[]
    {
        "hvad med", "hvordan med", "og hvad", "også", "og?"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> StartingFollowUpWords = new[]
    {
        "og ", "and "
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ShortFollowUps = new[]
    {
        "ok", "okay"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> TimeReferences = new[]
    {
        "today", "tomorrow", "i dag", "i morgen"
    }.ToFrozenSet();

    public static bool IsFollowUpQuestion(string text, List<Child> children, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalizedText = text.ToLowerInvariant();

        if (HasFollowUpPhrase(normalizedText))
        {
            logger.LogInformation("Detected follow-up question with phrase: {Text}", text);
            return true;
        }

        if (IsShortFollowUp(normalizedText))
        {
            logger.LogInformation("Detected likely follow-up based on short message: {Text}", text);
            return true;
        }

        var isShortMessage = normalizedText.Length < 15;
        var hasChildName = ContainsChildName(normalizedText, children);
        var hasTimeReference = ContainsTimeReference(normalizedText);

        var isFollowUp = isShortMessage && hasChildName && !hasTimeReference;

        if (isFollowUp)
        {
            logger.LogInformation("Detected follow-up question: {Text}", text);
        }

        return isFollowUp;
    }

    private static bool HasFollowUpPhrase(string normalizedText)
    {
        return EnglishFollowUpPhrases.Any(phrase => normalizedText.Contains(phrase)) ||
               DanishFollowUpPhrases.Any(phrase => normalizedText.Contains(phrase)) ||
               StartingFollowUpWords.Any(word => normalizedText.StartsWith(word));
    }

    private static bool IsShortFollowUp(string normalizedText)
    {
        return normalizedText.Length < 15 &&
               (normalizedText.Contains('?') || ShortFollowUps.Contains(normalizedText));
    }

    private static bool ContainsChildName(string normalizedText, List<Child> children)
    {
        foreach (var child in children)
        {
            // Check full name
            if (normalizedText.Contains(child.FirstName.ToLowerInvariant()) ||
                normalizedText.Contains(child.LastName.ToLowerInvariant()))
            {
                return true;
            }

            // Check first name parts
            var firstNameParts = child.FirstName.Split(' ');
            if (firstNameParts.Any(part => normalizedText.Contains(part.ToLowerInvariant())))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTimeReference(string normalizedText)
    {
        return TimeReferences.Any(timeRef => normalizedText.Contains(timeRef));
    }
}
