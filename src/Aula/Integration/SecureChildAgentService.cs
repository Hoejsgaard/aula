using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Integration;

/// <summary>
/// Secure child-aware agent service with comprehensive security layers.
/// Provides isolated AI interactions for each child with protection against attacks.
/// </summary>
public class SecureChildAgentService : IChildAgentService
{
    private readonly IChildContext _context;
    private readonly IChildContextValidator _contextValidator;
    private readonly IChildAuditService _auditService;
    private readonly IChildRateLimiter _rateLimiter;
    private readonly IChildDataService _dataService;
    private readonly IOpenAiService _openAiService;
    private readonly IPromptSanitizer _promptSanitizer;
    private readonly ILogger<SecureChildAgentService> _logger;

    public SecureChildAgentService(
        IChildContext context,
        IChildContextValidator contextValidator,
        IChildAuditService auditService,
        IChildRateLimiter rateLimiter,
        IChildDataService dataService,
        IOpenAiService openAiService,
        IPromptSanitizer promptSanitizer,
        ILogger<SecureChildAgentService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _promptSanitizer = promptSanitizer ?? throw new ArgumentNullException(nameof(promptSanitizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> SummarizeWeekLetterAsync(DateOnly date, ChatInterface chatInterface = ChatInterface.Slack)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "ai:summarize"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to summarize week letter", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "ai:summarize", SecuritySeverity.Warning);
            return "You don't have permission to summarize week letters.";
        }

        // Layer 3: Rate limiting
        if (!await _rateLimiter.IsAllowedAsync(child, "SummarizeWeekLetter"))
        {
            _logger.LogWarning("Rate limit exceeded for {ChildName} summarizing week letter", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "SummarizeWeekLetter", SecuritySeverity.Warning);
            throw new RateLimitExceededException("SummarizeWeekLetter", child.FirstName, 10, TimeSpan.FromMinutes(1));
        }

        try
        {
            // Layer 4: Audit logging
            _logger.LogInformation("Summarizing week letter for {ChildName} date {Date}",
                child.FirstName, date);

            // Get the week letter
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
            var weekLetter = await _dataService.GetWeekLetterAsync(weekNumber, date.Year);
            if (weekLetter == null)
            {
                _logger.LogInformation("No week letter found for {ChildName} date {Date}",
                    child.FirstName, date);
                return $"No week letter found for {date:yyyy-MM-dd}.";
            }

            // Layer 5: AI operation
            var summary = await _openAiService.SummarizeWeekLetterAsync(weekLetter, chatInterface);

            // Layer 6: Response filtering
            var filteredSummary = _promptSanitizer.FilterResponse(summary, child);

            await _rateLimiter.RecordOperationAsync(child, "SummarizeWeekLetter");
            await _auditService.LogDataAccessAsync(child, "SummarizeWeekLetter", $"week_{date:yyyy-MM-dd}", true);

            return filteredSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize week letter for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "SummarizeWeekLetter", $"week_{date:yyyy-MM-dd}", false);
            throw;
        }
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(DateOnly date, string question, ChatInterface chatInterface = ChatInterface.Slack)
    {
        return await AskQuestionAboutWeekLetterAsync(date, question, null, chatInterface);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(DateOnly date, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "ai:query"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to query week letter", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "ai:query", SecuritySeverity.Warning);
            return "You don't have permission to ask questions about week letters.";
        }

        // Layer 3: Input sanitization (CRITICAL for prompt injection prevention)
        string sanitizedQuestion;
        try
        {
            sanitizedQuestion = _promptSanitizer.SanitizeInput(question, child);
        }
        catch (PromptInjectionException ex)
        {
            _logger.LogWarning(ex, "Prompt injection detected for {ChildName}", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PromptInjection", question, SecuritySeverity.Critical);
            return "Your question contains invalid characters and cannot be processed.";
        }

        // Layer 4: Rate limiting
        if (!await _rateLimiter.IsAllowedAsync(child, "AskQuestion"))
        {
            _logger.LogWarning("Rate limit exceeded for {ChildName} asking questions", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "AskQuestion", SecuritySeverity.Warning);
            throw new RateLimitExceededException("AskQuestion", child.FirstName, 20, TimeSpan.FromMinutes(1));
        }

        try
        {
            // Layer 5: Audit logging
            _logger.LogInformation("Processing question for {ChildName} about date {Date}",
                child.FirstName, date);

            // Get the week letter
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
            var weekLetter = await _dataService.GetWeekLetterAsync(weekNumber, date.Year);
            if (weekLetter == null)
            {
                _logger.LogInformation("No week letter found for {ChildName} date {Date}",
                    child.FirstName, date);
                return $"No week letter found for {date:yyyy-MM-dd}.";
            }

            // Use child-specific context key if not provided
            var childContextKey = contextKey ?? GetConversationContextKey();

            // Layer 6: AI operation with sanitized input
            var answer = await _openAiService.AskQuestionAboutWeekLetterAsync(
                weekLetter, sanitizedQuestion, child.FirstName, childContextKey, chatInterface);

            // Layer 7: Response filtering
            var filteredAnswer = _promptSanitizer.FilterResponse(answer, child);

            await _rateLimiter.RecordOperationAsync(child, "AskQuestion");
            await _auditService.LogDataAccessAsync(child, "AskQuestion", $"week_{date:yyyy-MM-dd}", true);

            return filteredAnswer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process question for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "AskQuestion", $"week_{date:yyyy-MM-dd}", false);
            throw;
        }
    }

    public async Task<JObject> ExtractKeyInformationAsync(DateOnly date, ChatInterface chatInterface = ChatInterface.Slack)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "ai:extract"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to extract information", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "ai:extract", SecuritySeverity.Warning);
            return JObject.Parse("{\"error\": \"Permission denied\"}");
        }

        // Layer 3: Rate limiting
        if (!await _rateLimiter.IsAllowedAsync(child, "ExtractInformation"))
        {
            _logger.LogWarning("Rate limit exceeded for {ChildName} extracting information", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "ExtractInformation", SecuritySeverity.Warning);
            throw new RateLimitExceededException("ExtractInformation", child.FirstName, 5, TimeSpan.FromMinutes(1));
        }

        try
        {
            // Layer 4: Audit logging
            _logger.LogInformation("Extracting key information for {ChildName} date {Date}",
                child.FirstName, date);

            // Get the week letter
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
            var weekLetter = await _dataService.GetWeekLetterAsync(weekNumber, date.Year);
            if (weekLetter == null)
            {
                _logger.LogInformation("No week letter found for {ChildName} date {Date}",
                    child.FirstName, date);
                return JObject.Parse($"{{\"error\": \"No week letter found for {date:yyyy-MM-dd}\"}}");
            }

            // Layer 5: AI operation
            var extracted = await _openAiService.ExtractKeyInformationAsync(weekLetter, chatInterface);

            // Layer 6: Filter extracted data to remove sensitive information
            FilterExtractedData(extracted, child);

            await _rateLimiter.RecordOperationAsync(child, "ExtractInformation");
            await _auditService.LogDataAccessAsync(child, "ExtractInformation", $"week_{date:yyyy-MM-dd}", true);

            return extracted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract information for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "ExtractInformation", $"week_{date:yyyy-MM-dd}", false);
            throw;
        }
    }

    public async Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "ai:tools"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to use AI tools", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "ai:tools", SecuritySeverity.Warning);
            return "You don't have permission to use AI tools.";
        }

        // Layer 3: Input sanitization (CRITICAL for prompt injection prevention)
        string sanitizedQuery;
        try
        {
            sanitizedQuery = _promptSanitizer.SanitizeInput(query, child);
        }
        catch (PromptInjectionException ex)
        {
            _logger.LogWarning(ex, "Prompt injection detected for {ChildName} in tool query", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PromptInjection", query, SecuritySeverity.Critical);
            return "Your query contains invalid characters and cannot be processed.";
        }

        // Layer 4: Rate limiting (stricter for tools)
        if (!await _rateLimiter.IsAllowedAsync(child, "ProcessWithTools"))
        {
            _logger.LogWarning("Rate limit exceeded for {ChildName} using tools", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "RateLimitExceeded", "ProcessWithTools", SecuritySeverity.Warning);
            throw new RateLimitExceededException("ProcessWithTools", child.FirstName, 5, TimeSpan.FromMinutes(5));
        }

        try
        {
            // Layer 5: Audit logging
            _logger.LogInformation("Processing tool query for {ChildName}", child.FirstName);

            // Use child-specific context key
            var childContextKey = $"{contextKey}_{child.FirstName}_{child.LastName}";

            // Layer 6: AI operation with sanitized input
            var result = await _openAiService.ProcessQueryWithToolsAsync(
                sanitizedQuery, childContextKey, chatInterface);

            // Check for fallback signal and handle properly
            if (result == "FALLBACK_TO_EXISTING_SYSTEM")
            {
                _logger.LogInformation("Falling back to week letter system for {ChildName}", child.FirstName);

                // Try to answer using week letter data
                var today = DateOnly.FromDateTime(DateTime.Today);
                var weekLetter = await _dataService.GetOrFetchWeekLetterAsync(today, true);

                if (weekLetter != null)
                {
                    // Ask the question about the week letter - this calls our own method which has child context
                    result = await AskQuestionAboutWeekLetterAsync(today, sanitizedQuery, contextKey, chatInterface);
                }
                else
                {
                    result = chatInterface == ChatInterface.Telegram
                        ? "Beklager, jeg kunne ikke finde ugebrevet for denne uge."
                        : "Beklager, jeg kunne ikke finde ugebrevet for denne uge. Prøv igen senere.";
                }
            }

            // Layer 7: Response filtering
            var filteredResult = _promptSanitizer.FilterResponse(result, child);

            await _rateLimiter.RecordOperationAsync(child, "ProcessWithTools");
            await _auditService.LogDataAccessAsync(child, "ProcessWithTools", contextKey, true);

            return filteredResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process tool query for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "ProcessWithTools", contextKey, false);

            // Return a user-friendly error message instead of throwing
            var errorMessage = chatInterface == ChatInterface.Telegram
                ? "Beklager, der opstod en fejl. Prøv venligst igen."
                : "Beklager, jeg kan ikke behandle din forespørgsel lige nu. Prøv venligst igen om et øjeblik.";

            return errorMessage;
        }
    }

    public async Task ClearConversationHistoryAsync(string? contextKey = null)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Audit logging
        _logger.LogInformation("Clearing conversation history for {ChildName} context {ContextKey}",
            child.FirstName, contextKey ?? "default");

        // Use child-specific context key
        var childContextKey = contextKey != null
            ? $"{contextKey}_{child.FirstName}_{child.LastName}"
            : GetConversationContextKey();

        _openAiService.ClearConversationHistory(childContextKey);

        await _auditService.LogDataAccessAsync(child, "ClearConversation", childContextKey, true);
    }

    public string GetConversationContextKey()
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;
        return $"conversation_{child.FirstName}_{child.LastName}_{_context.ContextId}";
    }

    public Task<bool> ValidateResponseAppropriateness(string response)
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Check if response passes safety filters
        try
        {
            var filtered = _promptSanitizer.FilterResponse(response, child);
            // If significant content was removed, response is inappropriate
            var isAppropriate = filtered.Length >= response.Length * 0.9; // Allow 10% filtering
            return Task.FromResult(isAppropriate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Response validation failed for {ChildName}", child.FirstName);
            return Task.FromResult(false);
        }
    }

    private void FilterExtractedData(JObject data, Child child)
    {
        // Remove any fields that might contain sensitive information
        var sensitiveFields = new[] { "cpr", "ssn", "phone", "email", "address", "password" };

        foreach (var field in sensitiveFields)
        {
            if (data.Remove(field))
            {
                _logger.LogInformation("Removed sensitive field {Field} from extracted data for {ChildName}",
                    field, child.FirstName);
            }
        }

        // Recursively check nested objects
        foreach (var property in data.Properties().ToList())
        {
            if (property.Value is JObject nestedObject)
            {
                FilterExtractedData(nestedObject, child);
            }
            else if (property.Value is JArray array)
            {
                foreach (var item in array)
                {
                    if (item is JObject arrayObject)
                    {
                        FilterExtractedData(arrayObject, child);
                    }
                }
            }
        }
    }
}