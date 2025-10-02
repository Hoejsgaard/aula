using System;
using MinUddannelse.Content.WeekLetters;
using System.Linq;
using System.Threading.Tasks;
using MinUddannelse.Configuration;
using Microsoft.Extensions.Logging;

namespace MinUddannelse.AI.Services;

public class OpenAiService : IOpenAiService
{
    private readonly IWeekLetterAiService _openAiService;
    private readonly IWeekLetterService _weekLetterService;
    private readonly ILogger _logger;

    public OpenAiService(
        IWeekLetterAiService openAiService,
        IWeekLetterService weekLetterService,
        ILoggerFactory loggerFactory)
    {
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _weekLetterService = weekLetterService ?? throw new ArgumentNullException(nameof(weekLetterService));
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<OpenAiService>();
    }

    public async Task<string?> GetResponseAsync(Child child, string query)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        _logger.LogInformation("Getting AI response for child {ChildName}: {Query}", child.FirstName, query);

        // Check if this looks like a week letter question about days of the week
        if (IsWeekLetterQuery(query))
        {
            _logger.LogInformation("Detected week letter query for {ChildName}, processing with week letter data", child.FirstName);
            return await ProcessWeekLetterQuery(child, query);
        }

        // For non-week letter queries, use the standard OpenAI service
        var contextualQuery = $"[Context: Child {child.FirstName}] {query}";
        return await _openAiService.ProcessQueryWithToolsAsync(contextualQuery,
            $"child_{child.FirstName}",
            ChatInterface.Slack);
    }

    private bool IsWeekLetterQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) return false;

        var lowerQuery = query.ToLowerInvariant();

        // Check for Danish day names and common week letter question patterns
        var weekLetterIndicators = new[]
        {
            "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag", "søndag",
            "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday",
            "hvad skal", "what should", "hvilken dag", "what day", "på", "on",
            "denne uge", "this week", "ugebrev", "week letter"
        };

        return weekLetterIndicators.Any(indicator => lowerQuery.Contains(indicator));
    }

    private async Task<string?> ProcessWeekLetterQuery(Child child, string query)
    {
        try
        {
            // Get current week letter (default to current week)
            var currentDate = DateOnly.FromDateTime(DateTime.Now);
            var weekLetter = await _weekLetterService.GetOrFetchWeekLetterAsync(child, currentDate);

            if (weekLetter == null)
            {
                _logger.LogWarning("No week letter available for {ChildName} for current week", child.FirstName);
                return "Jeg kan ikke finde ugekrevset for denne uge.";
            }

            // Extract week letter content
            var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Week letter content is empty for {ChildName}", child.FirstName);
                return "Ugebrevet er tomt denne uge.";
            }

            _logger.LogInformation("Processing week letter query for {ChildName}, content length: {Length} characters",
                child.FirstName, content.Length);

            // Create a direct query with week letter context to avoid conversation management duplicates
            var contextualQuery = $@"Du er en hjælpsom assistent for {child.FirstName}.

Her er ugebrevet for denne uge:
{content}

Spørgsmål: {query}

Svar venligst kort og præcist på spørgsmålet baseret på information fra ugebrevet. Svar på dansk.";

            var response = await _openAiService.ProcessDirectQueryAsync(contextualQuery, ChatInterface.Slack);

            if (string.IsNullOrEmpty(response) || response == "FALLBACK_TO_EXISTING_SYSTEM")
            {
                _logger.LogError("Failed to get valid response from OpenAI service for week letter query for {ChildName}", child.FirstName);
                return "Jeg kunne ikke finde svaret i ugebrevet.";
            }

            _logger.LogInformation("Successfully processed week letter query for {ChildName}", child.FirstName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing week letter query for {ChildName}", child.FirstName);
            return "Der opstod en fejl ved behandling af dit spørgsmål om ugebrevet.";
        }
    }

    public async Task<string?> GetResponseWithContextAsync(Child child, string query, string conversationId)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        _logger.LogInformation("Getting AI response with context for child {ChildName}, conversation {ConversationId}",
            child.FirstName, conversationId);

        // Create child-specific conversation ID
        var childConversationId = $"{child.FirstName}_{conversationId}";

        return await _openAiService.ProcessQueryWithToolsAsync(query,
            childConversationId,
            ChatInterface.Slack);
    }

    public async Task ClearConversationHistoryAsync(Child child, string conversationId)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        _logger.LogInformation("Clearing conversation history for child {ChildName}, conversation {ConversationId}",
            child.FirstName, conversationId);

        // Create child-specific conversation ID
        var childConversationId = $"{child.FirstName}_{conversationId}";

        // Use the ClearConversationHistory method from IOpenAiService
        _openAiService.ClearConversationHistory(childConversationId);
        await Task.CompletedTask;
    }
}
