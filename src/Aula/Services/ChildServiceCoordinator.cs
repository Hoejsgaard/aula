using Aula.Channels;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Repositories;
using Aula.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Services;

/// <summary>
/// Coordinates all child-specific operations across the application.
/// Ensures all operations happen within proper child context scopes.
/// </summary>
public class ChildServiceCoordinator : IChildServiceCoordinator
{
    private readonly IChildOperationExecutor _executor;
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger<ChildServiceCoordinator> _logger;

    public ChildServiceCoordinator(
        IChildOperationExecutor executor,
        IAgentService agentService,
        Config config,
        ILogger<ChildServiceCoordinator> logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PreloadWeekLettersForAllChildrenAsync()
    {
        _logger.LogInformation("Starting week letter preload for all children");

        var allChildren = await _agentService.GetAllChildrenAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var results = await _executor.ExecuteForAllChildrenAsync<object>(allChildren,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();

                // Fetch current week
                var currentWeek = await dataService.GetOrFetchWeekLetterAsync(today, true);

                // Fetch past 2 weeks
                var lastWeek = await dataService.GetOrFetchWeekLetterAsync(today.AddDays(-7), true);
                var twoWeeksAgo = await dataService.GetOrFetchWeekLetterAsync(today.AddDays(-14), true);

                return new { CurrentWeek = currentWeek, LastWeek = lastWeek, TwoWeeksAgo = twoWeeksAgo } as object;
            },
            "PreloadWeekLetters");

        _logger.LogInformation("Completed week letter preload for {Count}/{Total} children",
            results?.Count ?? 0, allChildren.Count());
    }

    public async Task PostWeekLettersToChannelsAsync()
    {
        _logger.LogInformation("Posting week letters to channels for all children");

        var allChildren = await _agentService.GetAllChildrenAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var results = await _executor.ExecuteForAllChildrenAsync(allChildren,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();

                // Calculate ISO week number
                var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                var weekNumber = calendar.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue),
                    System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                    DayOfWeek.Monday);

                var weekLetter = await dataService.GetWeekLetterAsync(weekNumber, today.Year);

                if (weekLetter == null)
                {
                    return false;
                }

                // Format the week letter content
                var message = $"📚 Ugebrev for uge {weekNumber}\n\n{weekLetter.ToString()}";

                // Send to all configured channels for this child
                return await channelManager.SendMessageAsync(message, MessageFormat.Markdown);
            },
            "PostWeekLetters");

        var successCount = results.Values.Count(r => r);
        _logger.LogInformation("Posted week letters for {Success}/{Total} children",
            successCount, allChildren.Count());
    }

    public async Task<bool> FetchWeekLetterForChildAsync(Child child, DateOnly date)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                var letter = await dataService.GetOrFetchWeekLetterAsync(date, true);
                return letter != null;
            },
            "FetchWeekLetter");
    }

    public async Task<JObject?> GetWeekLetterForChildAsync(Child child, DateOnly date)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                return await dataService.GetOrFetchWeekLetterAsync(date, true);
            },
            $"GetWeekLetter_{child.FirstName}");
    }

    public async Task<IEnumerable<(Child child, JObject? weekLetter)>> FetchWeekLettersForAllChildrenAsync(DateOnly date)
    {
        var allChildren = await _agentService.GetAllChildrenAsync();
        var results = new List<(Child child, JObject? weekLetter)>();

        foreach (var child in allChildren)
        {
            var weekLetter = await _executor.ExecuteInChildContextAsync(child,
                async (serviceProvider) =>
                {
                    var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                    return await dataService.GetOrFetchWeekLetterAsync(date, true);
                },
                $"FetchWeekLetter_{child.FirstName}");

            results.Add((child, weekLetter));
        }

        return results;
    }

    public async Task ProcessScheduledTasksForChildAsync(Child child)
    {
        await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
                await scheduler.ProcessDueTasksAsync();
            },
            "ProcessScheduledTasks");
    }

    public async Task ProcessScheduledTasksForAllChildrenAsync()
    {
        var allChildren = await _agentService.GetAllChildrenAsync();

        await _executor.ExecuteForAllChildrenAsync(allChildren,
            async (serviceProvider) =>
            {
                var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
                await scheduler.ProcessDueTasksAsync();
                return true;
            },
            "ProcessScheduledTasksForAll");
    }

    public async Task<bool> SendReminderToChildAsync(Child child, string reminderMessage)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();
                return await channelManager.SendReminderAsync(reminderMessage);
            },
            "SendReminder");
    }

    public async Task<string> ProcessAiQueryForChildAsync(Child child, string query)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var aiService = serviceProvider.GetRequiredService<IChildAwareOpenAiService>();
                var response = await aiService.GetResponseAsync(query);
                return response ?? "Jeg kunne ikke behandle din forespørgsel.";
            },
            "ProcessAiQuery");
    }

    public async Task SeedHistoricalDataForChildAsync(Child child, int weeksBack = 12)
    {
        await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                var today = DateOnly.FromDateTime(DateTime.Today);

                for (int i = 0; i < weeksBack; i++)
                {
                    var date = today.AddDays(-7 * i);
                    await dataService.GetOrFetchWeekLetterAsync(date, false);
                }
            },
            "SeedHistoricalData");
    }

    public async Task SeedHistoricalDataForAllChildrenAsync(int weeksBack = 12)
    {
        var allChildren = await _agentService.GetAllChildrenAsync();

        await _executor.ExecuteForAllChildrenAsync(allChildren,
            async (serviceProvider) =>
            {
                var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                var today = DateOnly.FromDateTime(DateTime.Today);

                for (int i = 0; i < weeksBack; i++)
                {
                    var date = today.AddDays(-7 * i);
                    await dataService.GetOrFetchWeekLetterAsync(date, false);
                }

                return true;
            },
            $"SeedHistoricalData_{weeksBack}Weeks");
    }

    public async Task<DateTime?> GetNextScheduledTaskTimeForChildAsync(Child child)
    {
        return await _executor.ExecuteInChildContextAsync(child,
            async (serviceProvider) =>
            {
                var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
                var tasks = await scheduler.GetScheduledTasksAsync();

                var nextTask = tasks
                    .Where(t => t.Enabled && t.NextRun.HasValue)
                    .OrderBy(t => t.NextRun)
                    .Select(t => t.NextRun!.Value)
                    .Cast<DateTime?>()
                    .FirstOrDefault();

                return nextTask;
            },
            "GetNextScheduledTaskTime");
    }

    public async Task<bool> ValidateChildServicesAsync()
    {
        try
        {
            _logger.LogInformation("Validating child-aware services");

            // Check if we can get all children
            var allChildren = await _agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                _logger.LogWarning("No children configured");
                return false;
            }

            // Test creating a scope for the first child
            var testChild = allChildren.First();
            var testResult = await _executor.ExecuteInChildContextAsync(testChild,
                (serviceProvider) =>
                {
                    // Verify all required services can be resolved
                    var requiredServices = new[]
                    {
                        typeof(IChildContext),
                        typeof(IChildDataService),
                        typeof(IChildChannelManager),
                        typeof(IChildScheduler),
                        typeof(IChildAwareOpenAiService)
                    };

                    foreach (var serviceType in requiredServices)
                    {
                        var service = serviceProvider.GetService(serviceType);
                        if (service == null)
                        {
                            _logger.LogError("Failed to resolve service {ServiceType}", serviceType.Name);
                            return Task.FromResult(false);
                        }
                    }

                    // Verify context is properly set
                    var context = serviceProvider.GetRequiredService<IChildContext>();
                    if (context.CurrentChild == null ||
                        context.CurrentChild.FirstName != testChild.FirstName)
                    {
                        _logger.LogError("Child context not properly set");
                        return Task.FromResult(false);
                    }

                    return Task.FromResult(true);
                },
                "ValidateServices");

            return testResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate child services");
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> GetChildServicesHealthAsync()
    {
        var health = new Dictionary<string, bool>();

        try
        {
            // Check AgentService
            var allChildren = await _agentService.GetAllChildrenAsync();
            health["AgentService"] = allChildren.Any();

            if (allChildren.Any())
            {
                var testChild = allChildren.First();

                // Check each service
                var serviceChecks = await _executor.ExecuteInChildContextAsync(testChild,
                    async (serviceProvider) =>
                    {
                        var results = new Dictionary<string, bool>();

                        // Check IChildContext
                        try
                        {
                            var context = serviceProvider.GetRequiredService<IChildContext>();
                            results["ChildContext"] = context.CurrentChild != null;
                        }
                        catch { results["ChildContext"] = false; }

                        // Check IChildDataService
                        try
                        {
                            var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                            results["ChildDataService"] = dataService != null;
                        }
                        catch { results["ChildDataService"] = false; }

                        // Check IChildChannelManager
                        try
                        {
                            var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();
                            var hasChannels = await channelManager.HasConfiguredChannelsAsync();
                            results["ChildChannelManager"] = true;
                            results["ConfiguredChannels"] = hasChannels;
                        }
                        catch { results["ChildChannelManager"] = false; }

                        // Check IChildScheduler
                        try
                        {
                            var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
                            results["ChildScheduler"] = scheduler != null;
                        }
                        catch { results["ChildScheduler"] = false; }

                        // Check IChildAwareOpenAiService
                        try
                        {
                            var aiService = serviceProvider.GetRequiredService<IChildAwareOpenAiService>();
                            results["ChildAwareOpenAiService"] = aiService != null;
                        }
                        catch { results["ChildAwareOpenAiService"] = false; }

                        return results;
                    },
                    "HealthCheck");

                foreach (var kvp in serviceChecks)
                {
                    health[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                health["NoChildrenConfigured"] = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service health");
            health["Error"] = false;
        }

        return health;
    }

    public async Task<IEnumerable<Child>> GetAllChildrenAsync()
    {
        return await _agentService.GetAllChildrenAsync();
    }

    public async Task PostWeekLettersForAllChildrenAsync(DateOnly date, Func<Child, JObject?, Task> postAction)
    {
        if (postAction == null) throw new ArgumentNullException(nameof(postAction));

        var allChildren = await _agentService.GetAllChildrenAsync();

        foreach (var child in allChildren)
        {
            var weekLetter = await _executor.ExecuteInChildContextAsync(child,
                async (serviceProvider) =>
                {
                    var dataService = serviceProvider.GetRequiredService<IChildDataService>();
                    return await dataService.GetOrFetchWeekLetterAsync(date, true);
                },
                $"GetWeekLetter_{child.FirstName}");

            await postAction(child, weekLetter);
        }
    }
}