using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Integration;

public class AgentService : IAgentService
{
    private readonly IMinUddannelseClient _minUddannelseClient;
    private readonly DataService _dataService;
    private readonly Config _config;
    private readonly IWeekLetterAiService _openAiService;
    private readonly ILogger _logger;

    public AgentService(
        IMinUddannelseClient minUddannelseClient,
        DataService dataService,
        Config config,
        IWeekLetterAiService openAiService,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(minUddannelseClient);
        ArgumentNullException.ThrowIfNull(dataService);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(openAiService);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _minUddannelseClient = minUddannelseClient;
        _dataService = dataService;
        _config = config;
        _openAiService = openAiService;
        _logger = loggerFactory.CreateLogger(nameof(AgentService));
    }

    public async Task<bool> LoginAsync()
    {
        _logger.LogInformation("LoginAsync called - authentication now happens per-request");
        return await _minUddannelseClient.LoginAsync();
    }

    public async Task<JObject?> GetWeekLetterAsync(Child child, DateOnly date, bool useCache = true, bool allowLiveFetch = false)
    {
        _logger.LogInformation("MONITOR: GetWeekLetterAsync for {ChildName} for date {Date}, useCache: {UseCache}, allowLiveFetch: {AllowLiveFetch}",
            child.FirstName, date, useCache, allowLiveFetch);

        // Calculate week number and year for caching
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        var year = date.Year;

        // Authentication now happens per-request in MinUddannelseClient, no need to check here

        if (useCache)
        {
            var cachedWeekLetter = _dataService.GetWeekLetter(child, weekNumber, year);
            if (cachedWeekLetter != null)
            {
                _logger.LogInformation("MONITOR: Returning cached week letter for {ChildName}", child.FirstName);

                // Log the content of the cached week letter
                var cachedContent = cachedWeekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
                _logger.LogInformation("MONITOR: Cached week letter content length for {ChildName}: {Length} characters",
                    child.FirstName, cachedContent.Length);

                if (string.IsNullOrEmpty(cachedContent))
                {
                    _logger.LogWarning("MONITOR: Cached week letter content is empty for {ChildName}", child.FirstName);
                }

                // Add child name to the week letter object if not already present
                if (cachedWeekLetter["child"] == null)
                {
                    cachedWeekLetter["child"] = child.FirstName;
                    _logger.LogInformation("MONITOR: Added missing child name to cached week letter");
                }

                return cachedWeekLetter;
            }
        }

        _logger.LogInformation("MONITOR: Fetching fresh week letter for {ChildName} for date {Date}, allowLiveFetch: {AllowLiveFetch}", child.FirstName, date, allowLiveFetch);
        var weekLetter = await _minUddannelseClient.GetWeekLetter(child, date, allowLiveFetch);

        // Log the raw week letter structure
        _logger.LogInformation("MONITOR: Raw week letter structure for {ChildName}: {Keys}",
            child.FirstName, string.Join(", ", weekLetter.Properties().Select(p => p.Name)));

        if (weekLetter["ugebreve"] != null && weekLetter["ugebreve"] is JArray ugebreve && ugebreve.Count > 0)
        {
            _logger.LogInformation("MONITOR: Week letter for {ChildName} contains {Count} ugebreve items, first item has keys: {Keys}",
                child.FirstName, ugebreve.Count,
                string.Join(", ", ugebreve[0].Children<JProperty>().Select(p => p.Name)));

            var content = ugebreve[0]?["indhold"]?.ToString() ?? "";
            _logger.LogInformation("MONITOR: Week letter content length for {ChildName}: {Length} characters",
                child.FirstName, content.Length);

            if (!string.IsNullOrEmpty(content))
            {
                _logger.LogInformation("MONITOR: Week letter content starts with: {Start}",
                    content.Length > 100 ? content.Substring(0, 100) + "..." : content);
            }
        }
        else
        {
            _logger.LogWarning("MONITOR: Week letter for {ChildName} does not contain ugebreve array or it's empty", child.FirstName);
        }

        // Add child name to the week letter object
        weekLetter["child"] = child.FirstName;
        _logger.LogInformation("MONITOR: Added child name to week letter: {ChildName}", child.FirstName);

        _dataService.CacheWeekLetter(child, weekNumber, year, weekLetter);
        _logger.LogInformation("MONITOR: Cached week letter for {ChildName}", child.FirstName);

        return weekLetter;
    }

    public async Task<JObject?> GetWeekScheduleAsync(Child child, DateOnly date, bool useCache = true)
    {
        // Calculate week number and year for caching
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        var year = date.Year;

        // Authentication now happens per-request in MinUddannelseClient, no need to check here

        if (useCache)
        {
            var cachedWeekSchedule = _dataService.GetWeekSchedule(child, weekNumber, year);
            if (cachedWeekSchedule != null)
            {
                _logger.LogInformation("Returning cached week schedule for {ChildName}", child.FirstName);
                return cachedWeekSchedule;
            }
        }

        _logger.LogInformation("Getting week schedule for {ChildName} for date {Date}", child.FirstName, date);
        var weekSchedule = await _minUddannelseClient.GetWeekSchedule(child, date);

        _dataService.CacheWeekSchedule(child, weekNumber, year, weekSchedule);

        return weekSchedule;
    }

    public async Task<string> SummarizeWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Summarizing week letter for {ChildName} for date {Date} using {ChatInterface}",
            child.FirstName, date, chatInterface);

        var weekLetter = await GetWeekLetterAsync(child, date);
        if (weekLetter == null)
        {
            return "No week letter available for the specified date.";
        }
        return await _openAiService.SummarizeWeekLetterAsync(weekLetter, chatInterface);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Asking question about week letter for {ChildName} for date {Date} using {ChatInterface}: {Question}",
            child.FirstName, date, chatInterface, question);

        var weekLetter = await GetWeekLetterAsync(child, date);
        if (weekLetter == null)
        {
            return "No week letter available for the specified date.";
        }
        return await _openAiService.AskQuestionAboutWeekLetterAsync(weekLetter, question, child.FirstName, chatInterface);
    }

    public async Task<string> AskQuestionAboutWeekLetterAsync(Child child, DateOnly date, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("MONITOR: AskQuestionAboutWeekLetterAsync for {ChildName} with context {ContextKey} using {ChatInterface}: {Question}",
            child.FirstName, contextKey, chatInterface, question);

        var weekLetter = await GetWeekLetterAsync(child, date);
        if (weekLetter == null)
        {
            return "No week letter available for the specified date.";
        }

        // Extract and log the content to ensure it's being passed correctly
        var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
        _logger.LogInformation("MONITOR: Week letter content being passed to OpenAiService: {Length} characters", content.Length);

        if (!string.IsNullOrEmpty(content))
        {
            var contentPreview = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
            _logger.LogInformation("MONITOR: Week letter content preview: {Preview}", contentPreview);
        }

        return await _openAiService.AskQuestionAboutWeekLetterAsync(weekLetter, question, child.FirstName, contextKey, chatInterface);
    }

    public async Task<JObject> ExtractKeyInformationFromWeekLetterAsync(Child child, DateOnly date, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Extracting key information from week letter for {ChildName} for date {Date} using {ChatInterface}",
            child.FirstName, date, chatInterface);

        var weekLetter = await GetWeekLetterAsync(child, date);
        if (weekLetter == null)
        {
            return new JObject { ["error"] = "No week letter available for the specified date." };
        }
        return await _openAiService.ExtractKeyInformationAsync(weekLetter, chatInterface);
    }

    public ValueTask<Child?> GetChildByNameAsync(string childName)
    {
        _logger.LogInformation("Getting child by name: {ChildName}", childName);
        var child = _config.MinUddannelse.Children
            .FirstOrDefault(c => c.FirstName.Equals(childName, StringComparison.OrdinalIgnoreCase));

        if (child == null)
        {
            _logger.LogWarning("Child not found: {ChildName}", childName);
        }
        else
        {
            _logger.LogInformation("Found child: {ChildName}", child.FirstName);
        }

        return ValueTask.FromResult(child);
    }

    public ValueTask<IEnumerable<Child>> GetAllChildrenAsync()
    {
        _logger.LogInformation("Getting all children");
        var children = _config.MinUddannelse.Children.AsEnumerable();
        _logger.LogInformation("Found {Count} children", children.Count());
        return ValueTask.FromResult(children);
    }

    public async Task<string> AskQuestionAboutChildrenAsync(Dictionary<string, JObject> childrenWeekLetters, string question, string? contextKey, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Asking question about multiple children: {Question}", question);
        return await _openAiService.AskQuestionAboutChildrenAsync(childrenWeekLetters, question, contextKey, chatInterface);
    }


    public async Task<string> ProcessQueryWithToolsAsync(string query, string contextKey, Child? specificChild, ChatInterface chatInterface = ChatInterface.Slack)
    {
        _logger.LogInformation("Processing query with tools: {Query}, SpecificChild: {ChildName}", query, specificChild?.FirstName ?? "None");

        // Let the OpenAI service analyze intent first
        var response = await _openAiService.ProcessQueryWithToolsAsync(query, contextKey, chatInterface);

        // If it's a fallback to existing system, handle it here
        if (response == "FALLBACK_TO_EXISTING_SYSTEM")
        {
            _logger.LogInformation("Falling back to existing week letter system for query: {Query}", query);

            // SECURITY: Require a specific child to be provided
            if (specificChild == null)
            {
                _logger.LogWarning("No specific child provided for query processing - this is a security violation");
                return "Please specify which child you're asking about.";
            }

            _logger.LogInformation("Using specific child data for: {ChildName}", specificChild.FirstName);
            var childrenToProcess = new[] { specificChild };

            // Collect week letters for the selected children
            var childrenWeekLetters = new Dictionary<string, JObject>();
            foreach (var child in childrenToProcess)
            {
                var weekLetter = await GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                if (weekLetter != null)
                {
                    childrenWeekLetters[child.FirstName] = weekLetter;
                }
            }

            if (childrenWeekLetters.Count == 0)
            {
                return specificChild != null
                    ? $"I don't have any week letters available for {specificChild.FirstName} at the moment."
                    : "I don't have any week letters available at the moment.";
            }

            // Add day context if needed
            string enhancedQuery = query;
            if (query.ToLowerInvariant().Contains("i dag") || query.ToLowerInvariant().Contains("today"))
            {
                string dayOfWeek = DateTime.Now.DayOfWeek.ToString();
                enhancedQuery = $"{query} (Today is {dayOfWeek})";
            }
            else if (query.ToLowerInvariant().Contains("i morgen") || query.ToLowerInvariant().Contains("tomorrow"))
            {
                string dayOfWeek = DateTime.Now.AddDays(1).DayOfWeek.ToString();
                enhancedQuery = $"{query} (Tomorrow is {dayOfWeek})";
            }

            // Add language instruction to ensure response is in same language as query
            bool isDanish = query.ToLowerInvariant().Contains("hvad") ||
                           query.ToLowerInvariant().Contains("skal") ||
                           query.ToLowerInvariant().Contains("dag") ||
                           query.ToLowerInvariant().Contains("morgen") ||
                           query.ToLowerInvariant().Contains("børn");

            if (isDanish)
            {
                enhancedQuery = $"{enhancedQuery} (CRITICAL: Respond in Danish - the user asked in Danish)";
            }

            // Use the existing method for week letter questions
            return await _openAiService.AskQuestionAboutChildrenAsync(childrenWeekLetters, enhancedQuery, contextKey, chatInterface);
        }

        return response;
    }
}
