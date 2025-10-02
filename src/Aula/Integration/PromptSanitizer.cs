using Aula.Configuration;
using Aula.Integration.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Aula.Integration;

/// <summary>
/// Implementation of prompt sanitization to prevent injection attacks.
/// </summary>
public class PromptSanitizer : IPromptSanitizer
{
    private readonly ILogger _logger;
    private readonly List<string> _blockedPatterns;
    private readonly List<Regex> _dangerousPatterns;

    public PromptSanitizer(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<PromptSanitizer>();

        // Define patterns that indicate prompt injection attempts
        _blockedPatterns = new List<string>
        {
            "ignore previous instructions",
            "disregard above",
            "forget what i said",
            "new instructions",
            "system prompt",
            "you are now",
            "act as",
            "roleplay as",
            "pretend to be",
            "simulate being",
            "bypass safety",
            "ignore safety",
            "disable filters",
            "raw output",
            "unfiltered response",
            "developer mode",
            "dan mode",
            "jailbreak",
            "admin access",
            "system access",
            "execute command",
            "run code",
            "</system>",
            "<system>",
            "```python",
            "```bash",
            "import os",
            "subprocess",
            "eval(",
            "exec("
        };

        // Compile regex patterns for more complex injection detection
        _dangerousPatterns = new List<Regex>
        {
            new Regex(@"\bsystem\s*[:=]\s*", RegexOptions.IgnoreCase),
            new Regex(@"\bprompt\s*[:=]\s*", RegexOptions.IgnoreCase),
            new Regex(@"\binstructions?\s*[:=]\s*", RegexOptions.IgnoreCase),
            new Regex(@"\b(ignore|override|bypass|disable)\s+\w*\s*(rules?|filters?|safety)", RegexOptions.IgnoreCase),
            new Regex(@"<[^>]*script[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"javascript\s*:", RegexOptions.IgnoreCase),
            new Regex(@"data\s*:\s*text/html", RegexOptions.IgnoreCase),
            new Regex(@"\bon\w+\s*=\s*['""]", RegexOptions.IgnoreCase) // Event handlers
		};
    }

    public string SanitizeInput(string input, Child child)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        _logger.LogDebug("Sanitizing input for {ChildName}, length: {Length}",
            child.FirstName, input.Length);

        // Check for prompt injection attempts
        if (!IsInputSafe(input))
        {
            _logger.LogWarning("Prompt injection detected for {ChildName}. Input blocked.",
                child.FirstName);
            throw new PromptInjectionException(child.FirstName, input.Length);
        }

        // Remove any HTML/script tags
        var sanitized = Regex.Replace(input, @"<[^>]+>", string.Empty);

        // Remove multiple spaces and normalize whitespace
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        // Escape special characters that might be interpreted as commands
        sanitized = sanitized
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("$", "\\$");

        // Limit input length to prevent resource exhaustion
        const int maxLength = 2000;
        if (sanitized.Length > maxLength)
        {
            _logger.LogInformation("Truncating input for {ChildName} from {Original} to {Max} characters",
                child.FirstName, sanitized.Length, maxLength);
            sanitized = sanitized.Substring(0, maxLength);
        }

        _logger.LogDebug("Input sanitized for {ChildName}. Original: {Original}, Sanitized: {Sanitized}",
            child.FirstName, input.Length, sanitized.Length);

        return sanitized;
    }

    public bool IsInputSafe(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        var lowerInput = input.ToLowerInvariant();

        // Check for blocked phrases
        foreach (var pattern in _blockedPatterns)
        {
            if (lowerInput.Contains(pattern))
            {
                _logger.LogWarning("Blocked pattern detected: {Pattern}", pattern);
                return false;
            }
        }

        // Check for dangerous regex patterns
        foreach (var regex in _dangerousPatterns)
        {
            if (regex.IsMatch(input))
            {
                _logger.LogWarning("Dangerous pattern detected: {Pattern}", regex.ToString());
                return false;
            }
        }

        // Check for excessive special characters (potential code injection)
        var specialCharCount = input.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
        var specialCharRatio = (double)specialCharCount / input.Length;

        if (specialCharRatio > 0.3 && input.Length > 20)
        {
            _logger.LogWarning("High special character ratio detected: {Ratio:P}", specialCharRatio);
            return false;
        }

        // Check for repeated patterns (potential attack)
        if (HasRepeatedPatterns(input))
        {
            _logger.LogWarning("Repeated patterns detected in input");
            return false;
        }

        return true;
    }

    public string FilterResponse(string response, Child child)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        _logger.LogDebug("Filtering response for {ChildName}", child.FirstName);

        // Remove any potentially sensitive information
        var filtered = response;

        // Remove email addresses
        filtered = Regex.Replace(filtered, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[email removed]");

        // Remove phone numbers (Danish format)
        filtered = Regex.Replace(filtered, @"\b\d{8}\b|\+45\s\d{8}\b|\+45\s?\d{2}\s?\d{2}\s?\d{2}\s?\d{2}\b", "[phone removed]");

        // Remove personal identification numbers
        filtered = Regex.Replace(filtered, @"\b\d{6}-?\d{4}\b", "[CPR removed]");

        // Remove URLs that might contain sensitive data
        filtered = Regex.Replace(filtered, @"https?://[^\s]+", "[URL removed]");

        // Ensure response is child-appropriate (no profanity)
        filtered = RemoveProfanity(filtered);

        _logger.LogDebug("Response filtered for {ChildName}", child.FirstName);

        return filtered;
    }

    public IReadOnlyList<string> GetBlockedPatterns()
    {
        return _blockedPatterns.AsReadOnly();
    }

    private bool HasRepeatedPatterns(string input)
    {
        // Check for repeated substrings (potential attack pattern)
        if (input.Length < 50)
            return false;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 10)
            return false;

        // Check if the same word appears too many times
        var wordCounts = words.GroupBy(w => w.ToLowerInvariant())
            .Select(g => new { Word = g.Key, Count = g.Count() })
            .Where(x => x.Word.Length > 3) // Ignore short words
            .ToList();

        foreach (var wc in wordCounts)
        {
            var ratio = (double)wc.Count / words.Length;
            if (ratio > 0.2) // Same word is more than 20% of content
            {
                return true;
            }
        }

        return false;
    }

    private string RemoveProfanity(string text)
    {
        // Basic profanity filter - in production, use a comprehensive list
        var profanityList = new[] { "damn", "hell" }; // Minimal list for family-friendly content

        foreach (var word in profanityList)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            text = Regex.Replace(text, pattern, "[removed]", RegexOptions.IgnoreCase);
        }

        return text;
    }
}
