using Aula.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Aula.Channels;

/// <summary>
/// Filters message content to prevent information leakage between children.
/// Ensures that messages only contain information appropriate for the target child.
/// </summary>
public class MessageContentFilter : IMessageContentFilter
{
    private readonly ILogger<MessageContentFilter> _logger;

    // Common patterns that might contain child-specific information
    private readonly List<string> _sensitivePatterns = new()
    {
        @"\b\d{10}\b", // CPR numbers (10 digits)
        @"\b\d{4}-\d{2}-\d{2}-\d{4}\b", // Formatted CPR
        @"[A-Za-z]+\s+[A-Za-z]+\s*-\s*\d+\.\s*klasse", // Child name with class
        @"Elev:\s*[A-Za-z]+\s+[A-Za-z]+", // Student references
    };

    public MessageContentFilter(ILogger<MessageContentFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string FilterForChild(string message, Child child)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var filteredMessage = message;

        // Remove references to other children
        filteredMessage = RemoveOtherChildReferences(filteredMessage, child);

        // Redact sensitive information
        filteredMessage = RedactSensitiveInfo(filteredMessage);

        // Ensure child's own name is properly formatted
        filteredMessage = EnsureProperChildReference(filteredMessage, child);

        if (filteredMessage != message)
        {
            _logger.LogDebug("Message filtered for {ChildName}. Original length: {Original}, Filtered: {Filtered}",
                child.FirstName, message.Length, filteredMessage.Length);
        }

        return filteredMessage;
    }

    public bool ContainsOtherChildData(string message, Child currentChild)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        // Check for any name patterns in the message
        var namePattern = @"\b[A-Z][a-zæøåÆØÅ]+\s+[A-Z][a-zæøåÆØÅ]+\b";
        var matches = Regex.Matches(message, namePattern);

        foreach (Match match in matches)
        {
            var name = match.Value;

            // Check if this looks like a person's name and isn't the current child
            if (LooksLikePersonName(name))
            {
                // Split the name to check both parts
                var nameParts = name.Split(' ');
                var isCurrentChild = false;

                // Check if either part matches the current child's first or last name
                foreach (var part in nameParts)
                {
                    if (part.Equals(currentChild.FirstName, StringComparison.OrdinalIgnoreCase) ||
                        part.Equals(currentChild.LastName, StringComparison.OrdinalIgnoreCase))
                    {
                        isCurrentChild = true;
                        break;
                    }
                }

                if (!isCurrentChild)
                {
                    _logger.LogWarning("Message contains potential data about other children: {Name}", name);
                    return true;
                }
            }
        }

        // Check for multiple distinct class references (might indicate cross-child data)
        var classReferences = Regex.Matches(message, @"\d+\.\s*klasse", RegexOptions.IgnoreCase);
        var distinctClasses = classReferences.Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .Count();

        if (distinctClasses > 1)
        {
            _logger.LogWarning("Message contains references to multiple classes");
            return true;
        }

        return false;
    }

    public string RemoveOtherChildReferences(string message, Child currentChild)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var filteredMessage = message;

        // Remove lines that mention other children by name
        var lines = filteredMessage.Split('\n');
        var filteredLines = new List<string>();

        foreach (var line in lines)
        {
            // Skip lines that appear to reference other children
            if (IsLineAboutOtherChild(line, currentChild))
            {
                _logger.LogDebug("Removing line that references other child: {Line}",
                    line.Length > 50 ? line.Substring(0, 50) + "..." : line);
                continue;
            }

            filteredLines.Add(line);
        }

        return string.Join('\n', filteredLines);
    }

    public bool ValidateMessageSafety(string message, Child targetChild)
    {
        // Check if message contains other child data
        if (ContainsOtherChildData(message, targetChild))
        {
            _logger.LogWarning("Message failed safety validation - contains other child data");
            return false;
        }

        // Check for potential security risks
        if (ContainsSecurityRisks(message))
        {
            _logger.LogWarning("Message failed safety validation - security risks detected");
            return false;
        }

        // Check message length (prevent spam/abuse)
        if (message.Length > 10000)
        {
            _logger.LogWarning("Message failed safety validation - exceeds maximum length");
            return false;
        }

        return true;
    }

    public string RedactSensitiveInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var redactedMessage = message;

        // Redact potential CPR numbers
        redactedMessage = Regex.Replace(redactedMessage, @"\b\d{10}\b", "[CPR REDACTED]");
        redactedMessage = Regex.Replace(redactedMessage, @"\b\d{4}-\d{2}-\d{2}-\d{4}\b", "[CPR REDACTED]");

        // Redact email addresses that might belong to other families
        redactedMessage = Regex.Replace(redactedMessage,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            match =>
            {
                // Keep school domain emails visible
                if (match.Value.EndsWith("@aula.dk") || match.Value.EndsWith("@skolekom.dk"))
                    return match.Value;
                return "[EMAIL REDACTED]";
            });

        // Redact phone numbers
        redactedMessage = Regex.Replace(redactedMessage,
            @"\b(\+45\s?)?\d{2}\s?\d{2}\s?\d{2}\s?\d{2}\b",
            "[PHONE REDACTED]");

        // Redact addresses that look like home addresses
        redactedMessage = Regex.Replace(redactedMessage,
            @"[A-Za-zæøåÆØÅ\s]+vej\s+\d+.*?,\s*\d{4}\s+[A-Za-zæøåÆØÅ\s]+",
            "[ADDRESS REDACTED]",
            RegexOptions.IgnoreCase);

        return redactedMessage;
    }

    // Helper methods
    private bool IsLineAboutOtherChild(string line, Child currentChild)
    {
        // Skip empty lines
        if (string.IsNullOrWhiteSpace(line))
            return false;

        // Check if line mentions a child's name that isn't the current child
        var namePattern = @"\b[A-Z][a-zæøåÆØÅ]+\s+[A-Z][a-zæøåÆØÅ]+\b";
        var matches = Regex.Matches(line, namePattern);

        foreach (Match match in matches)
        {
            var name = match.Value;

            // Check if this looks like a person's name and isn't the current child
            if (LooksLikePersonName(name))
            {
                // Split the name to check both parts
                var nameParts = name.Split(' ');
                var isCurrentChild = false;

                // Check if either part matches the current child's first or last name
                foreach (var part in nameParts)
                {
                    if (part.Equals(currentChild.FirstName, StringComparison.OrdinalIgnoreCase) ||
                        part.Equals(currentChild.LastName, StringComparison.OrdinalIgnoreCase))
                    {
                        isCurrentChild = true;
                        break;
                    }
                }

                if (!isCurrentChild)
                {
                    // Additional context check - is this in a context that suggests it's about a child?
                    var context = GetSurroundingContext(line, match.Index);
                    // For safety, assume any name that isn't the current child might be another child
                    return true;
                }
            }
        }

        return false;
    }

    private bool LooksLikePersonName(string text)
    {
        // Basic heuristic: two capitalized words that could be first and last name
        var parts = text.Split(' ');
        if (parts.Length != 2)
            return false;

        // Check if both parts start with capital letter
        return char.IsUpper(parts[0][0]) && char.IsUpper(parts[1][0]);
    }

    private string GetSurroundingContext(string line, int position)
    {
        var contextStart = Math.Max(0, position - 20);
        var contextEnd = Math.Min(line.Length, position + 50);
        return line.Substring(contextStart, contextEnd - contextStart);
    }

    private bool IsChildContext(string context)
    {
        var childIndicators = new[]
        {
            "elev", "barn", "klasse", "skole", "forældre",
            "student", "child", "pupil", "class"
        };

        return childIndicators.Any(indicator =>
            context.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsSecurityRisks(string message)
    {
        // Check for potential script injection attempts
        var scriptPatterns = new[]
        {
            @"<script[^>]*>.*?</script>",
            @"javascript:",
            @"on\w+\s*=",
            @"<iframe[^>]*>",
        };

        foreach (var pattern in scriptPatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Potential script injection detected in message");
                return true;
            }
        }

        // Check for SQL injection patterns
        var sqlPatterns = new[]
        {
            @";\s*(DROP|DELETE|INSERT|UPDATE|ALTER)\s+",
            @"--\s*$",
            @"\/\*.*\*\/",
            @"'\s*OR\s*'?\d*'\s*=\s*'?\d*",
        };

        foreach (var pattern in sqlPatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Potential SQL injection detected in message");
                return true;
            }
        }

        return false;
    }

    private string EnsureProperChildReference(string message, Child child)
    {
        // Ensure the child's name is consistently formatted when mentioned
        var childFullName = $"{child.FirstName} {child.LastName}";

        // Replace variations of the child's name with consistent format
        message = Regex.Replace(message,
            $@"\b{Regex.Escape(child.FirstName)}\s+{Regex.Escape(child.LastName[0].ToString())}\.?\b",
            childFullName,
            RegexOptions.IgnoreCase);

        return message;
    }
}