using Aula.Configuration;

namespace Aula.Security;

/// <summary>
/// Service for sanitizing user inputs to prevent prompt injection attacks.
/// </summary>
public interface IPromptSanitizer
{
    /// <summary>
    /// Sanitizes user input to prevent prompt injection attacks.
    /// Removes or escapes potentially malicious content while preserving legitimate queries.
    /// </summary>
    /// <param name="input">The raw user input to sanitize</param>
    /// <param name="child">The child context for additional validation</param>
    /// <returns>Sanitized input safe for AI processing</returns>
    string SanitizeInput(string input, Child child);

    /// <summary>
    /// Validates if the input contains potential prompt injection attempts.
    /// </summary>
    /// <param name="input">The input to validate</param>
    /// <returns>True if the input appears safe, false if injection detected</returns>
    bool IsInputSafe(string input);

    /// <summary>
    /// Filters AI responses to ensure they are appropriate for the child.
    /// </summary>
    /// <param name="response">The AI response to filter</param>
    /// <param name="child">The child context for filtering</param>
    /// <returns>Filtered response appropriate for the child</returns>
    string FilterResponse(string response, Child child);

    /// <summary>
    /// Gets a list of blocked patterns that indicate prompt injection.
    /// </summary>
    IReadOnlyList<string> GetBlockedPatterns();
}
