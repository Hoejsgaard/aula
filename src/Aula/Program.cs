using System.Reflection;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aula.Bots;
using Aula.Channels;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Tools;
using Aula.Configuration;
using Aula.Services;
using Aula.Repositories;
using Aula.Utilities;
using Aula.Context;
using Aula.Authentication;
using Aula.Events;
using Newtonsoft.Json.Linq;

namespace Aula;

public class Program
{
    public static async Task Main()
    {
        var serviceProvider = ConfigureServices();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(Program));

        try
        {
            logger.LogInformation("Starting aula");

            var config = serviceProvider.GetRequiredService<Config>();

            // Validate configuration at startup
            var configValidator = serviceProvider.GetRequiredService<IConfigurationValidator>();
            var validationResult = await configValidator.ValidateConfigurationAsync(config);

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    logger.LogError("Configuration error: {Error}", error);
                }
                logger.LogError("Application startup failed due to configuration errors");
                return;
            }

            foreach (var warning in validationResult.Warnings)
            {
                logger.LogWarning("Configuration warning: {Warning}", warning);
            }

            // Child-aware architecture: Services are now accessed through child context scopes
            // Legacy SlackBot and TelegramClient removed in favor of child-aware implementations

            // Initialize Supabase
            var supabaseService = serviceProvider.GetRequiredService<ISupabaseService>();
            await supabaseService.InitializeAsync();

            // Test Supabase connection
            var connectionTest = await supabaseService.TestConnectionAsync();
            if (!connectionTest)
            {
                logger.LogWarning("Supabase connection test failed - continuing without database features");
            }
            else
            {
                logger.LogInformation("Supabase connection test successful");
            }

            // Historical data seeding removed with legacy architecture

            // Preload week letters for all children to ensure data is available for interactive bots
            // Legacy coordinator removed
            if (config.Features?.PreloadWeekLettersOnStartup == true)
            {
                logger.LogInformation("Preloading week letters for all children");
                // Legacy preload removed
            }
            else
            {
                logger.LogInformation("Week letter preloading disabled in configuration");
            }

            // Start scheduling service FIRST before bots
            logger.LogInformation("🚀 About to start SchedulingService");
            var schedulingService = serviceProvider.GetRequiredService<ISchedulingService>();
            logger.LogInformation("🚀 Got SchedulingService instance, calling StartAsync");
            await schedulingService.StartAsync();
            logger.LogInformation("🚀 SchedulingService.StartAsync completed");

            // Legacy bot implementations removed

            // Wire up child-specific event handlers for each configured child
            if (schedulingService is SchedulingService schedService)
            {
                // Subscribe to week letter events for each child
                schedService.ChildWeekLetterReady += async (sender, args) =>
                {
                    logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

                    // Find the child configuration
                    var childConfig = config.MinUddannelse?.Children?.FirstOrDefault(c =>
                        c.FirstName.Equals(args.ChildFirstName, StringComparison.OrdinalIgnoreCase));

                    if (childConfig == null)
                    {
                        logger.LogWarning("No configuration found for child: {ChildName}", args.ChildFirstName);
                        return;
                    }

                    // Process within a child context scope
                    using (var scope = new ChildContextScope(serviceProvider, childConfig))
                    {
                        try
                        {
                            // Check if this child has Slack configured
                            var slackWebhook = childConfig.Channels?.Slack?.WebhookUrl;

                            if (!string.IsNullOrEmpty(slackWebhook) && args.WeekLetter != null)
                            {
                                // Create a SlackBot instance with the child's webhook
                                var slackBot = new SlackBot(slackWebhook);

                                // Post week letter to Slack
                                var success = await slackBot.PostWeekLetter(args.WeekLetter, childConfig);

                                if (success)
                                {
                                    logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
                                }
                                else
                                {
                                    logger.LogError("Failed to post week letter to Slack for {ChildName}", args.ChildFirstName);
                                }
                            }
                            else if (args.WeekLetter != null)
                            {
                                logger.LogWarning("Slack not configured for {ChildName} - no webhook URL", args.ChildFirstName);
                            }
                            else
                            {
                                logger.LogWarning("No week letter to post for {ChildName}", args.ChildFirstName);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing week letter event for child: {ChildName}", args.ChildFirstName);
                        }
                    }
                };

                // ChildScheduleReady event subscription removed - event not currently used
            }

            // Start ChildAwareSlackInteractiveBot for EACH child with Slack enabled
            // This ensures complete isolation per child as per Task 010's child-centric architecture
            var slackBots = new List<ChildAwareSlackInteractiveBot>();

            var slackEnabledChildren = config.MinUddannelse?.Children?
                .Where(c => c.Channels?.Slack?.Enabled == true &&
                           c.Channels?.Slack?.EnableInteractiveBot == true &&
                           !string.IsNullOrEmpty(c.Channels?.Slack?.ApiToken))
                .ToList() ?? new List<Child>();

            if (slackEnabledChildren.Any())
            {
                foreach (var child in slackEnabledChildren)
                {
                    logger.LogInformation("Starting ChildAwareSlackInteractiveBot for {ChildName} on channel {ChannelId}",
                        child.FirstName, child.Channels!.Slack!.ChannelId);

                    var childBot = new ChildAwareSlackInteractiveBot(
                        serviceProvider,
                        serviceProvider.GetRequiredService<IChildServiceCoordinator>(),
                        config,
                        loggerFactory);

                    await childBot.StartForChild(child);
                    slackBots.Add(childBot);

                    logger.LogInformation("ChildAwareSlackInteractiveBot started successfully for {ChildName}", child.FirstName);
                }

                logger.LogInformation("Started {Count} child-aware Slack bots for complete isolation", slackBots.Count);
            }
            else
            {
                logger.LogInformation("No children have Slack interactive bot configured");
            }

            // Legacy Telegram bot removed

            // Post week letters on startup if configured
            if (config.Features?.PostWeekLettersOnStartup == true && schedulingService is SchedulingService schedSvc)
            {
                logger.LogInformation("📬 Posting current week letters on startup");

                // Get current week and year
                var now = DateTime.Now;
                var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(now);
                var year = now.Year;

                // Post week letters for each child
                foreach (var child in config.MinUddannelse?.Children ?? new List<Child>())
                {
                    try
                    {
                        // Get the week letter from cache/database/API using child coordinator
                        JObject? weekLetter = null;
                        using (var scope = new ChildContextScope(serviceProvider, child))
                        {
                            await scope.ExecuteAsync(async provider =>
                            {
                                var dataService = provider.GetRequiredService<IChildDataService>();
                                var date = DateOnly.FromDateTime(now.AddDays(-7 * (System.Globalization.ISOWeek.GetWeekOfYear(now) - weekNumber)));
                                weekLetter = await dataService.GetOrFetchWeekLetterAsync(date, true);
                            });
                        }

                        if (weekLetter != null)
                        {
                            logger.LogInformation("📨 Emitting week letter event for {ChildName} (week {WeekNumber}/{Year})",
                                child.FirstName, weekNumber, year);

                            // Emit the ChildWeekLetterReady event
                            var childId = child.FirstName.ToLowerInvariant().Replace(" ", "_");
                            var eventArgs = new ChildWeekLetterEventArgs(
                                childId,
                                child.FirstName,
                                weekNumber,
                                year,
                                weekLetter);

                            // Fire the event - the event handlers in Program.cs will post to Slack
                            schedSvc.TriggerChildWeekLetterReady(eventArgs);
                        }
                        else
                        {
                            logger.LogWarning("No week letter found for {ChildName} (week {WeekNumber}/{Year})",
                                child.FirstName, weekNumber, year);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error posting week letter for {ChildName} on startup", child.FirstName);
                    }
                }

                logger.LogInformation("✅ Week letter startup posting complete");
            }

            logger.LogInformation("Aula started");

            // Keep the application running with cancellation support
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                logger.LogInformation("Shutdown requested");

                // Dispose all child-aware Slack bots
                foreach (var bot in slackBots)
                {
                    bot?.Dispose();
                }
                logger.LogInformation("Disposed {Count} Slack bots", slackBots.Count);
            };

            try
            {
                // Wait indefinitely until cancellation is requested
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Application shutting down gracefully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting aula");
        }
    }

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<Config>(provider =>
        {
            var config = new Config();
            configuration.Bind(config);
            return config;
        });

        // Logging - configure to always go to console with timestamps
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "HH:mm:ss ";
                options.IncludeScopes = false;
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Memory cache
        services.AddMemoryCache();

        // Child-aware architecture services
        services.AddScoped<IChildContext, ChildContext>();
        services.AddScoped<IChildAuditService, ChildAuditService>();
        services.AddScoped<IChildRateLimiter, ChildRateLimiter>();
        services.AddScoped<IChildDataService, SecureChildDataService>();
        services.AddScoped<IChildChannelManager, SecureChildChannelManager>();
        services.AddScoped<IChildScheduler, SecureChildScheduler>();
        services.AddScoped<IChildAwareOpenAiService, SecureChildAwareOpenAiService>();
        services.AddScoped<IChildAgentService, SecureChildAgentService>();
        services.AddSingleton<IChildContextValidator, ChildContextValidator>();
        services.AddSingleton<IChildOperationExecutor, ChildOperationExecutor>();

        // Legacy interfaces still needed
        services.AddScoped<IDataService, DataService>();
        services.AddScoped<IMinUddannelseClient, MinUddannelseClient>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddSingleton<IChildServiceCoordinator, ChildServiceCoordinator>();
        services.AddSingleton<IPromptSanitizer, PromptSanitizer>();
        services.AddSingleton<IMessageContentFilter, MessageContentFilter>();

        // Other singleton services
        // Only register SlackBot if we have a root-level Slack config (which we don't anymore)
        // Or if any child has Slack configured
        var hasSlackConfig = configuration.GetSection("Slack").Exists() ||
                            configuration.GetSection("MinUddannelse:Children")
                                .GetChildren()
                                .Any(c => c.GetValue<bool>("Channels:Slack:Enabled"));

        services.AddSingleton<IChannelManager, ChannelManager>();

        services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IAiToolsManager, AiToolsManager>();
        services.AddSingleton<IOpenAiService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var aiToolsManager = provider.GetRequiredService<IAiToolsManager>();
            var conversationManager = provider.GetRequiredService<IConversationManager>();
            var promptBuilder = provider.GetRequiredService<IPromptBuilder>();
            return new OpenAiService(config.OpenAi.ApiKey, loggerFactory, aiToolsManager, conversationManager, promptBuilder);
        });

        // Only register TelegramInteractiveBot if enabled
        var telegramEnabled = configuration.GetValue<bool>("Telegram:Enabled");
        var telegramToken = configuration.GetValue<string>("Telegram:Token");
        if (telegramEnabled && !string.IsNullOrEmpty(telegramToken))
        {
            services.AddSingleton<Telegram.Bot.ITelegramBotClient>(provider =>
            {
                var config = provider.GetRequiredService<Config>();
                return new Telegram.Bot.TelegramBotClient(config.Telegram.Token);
            });
        }

        services.AddSingleton<ISupabaseService, SupabaseService>();
        services.AddSingleton<IWeekLetterSeeder, WeekLetterSeeder>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        services.AddSingleton<ISchedulingService, SchedulingService>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Preloads week letters for all children to ensure data is available for interactive bots.
    /// Fetches current week and past 2 weeks to ensure we have recent data.
    /// This improves response times and provides better user experience.
    /// Uses the new child-aware architecture for proper isolation.
    /// </summary>
    private static async Task PreloadChildrenWeekLetters(IChildServiceCoordinator coordinator, Config config, ILogger logger)
    {
        logger.LogInformation("📚 Starting week letter preload for current and recent weeks");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weeksToCheck = config.Features?.WeeksToPreload ?? 3; // Use configured value or default to 3
        var successCount = 0;
        var totalAttempts = 0;

        for (int weeksBack = 0; weeksBack < weeksToCheck; weeksBack++)
        {
            var targetDate = today.AddDays(-7 * weeksBack);
            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(targetDate.ToDateTime(TimeOnly.MinValue));
            var year = targetDate.Year;

            logger.LogInformation("📅 Checking week {WeekNumber}/{Year} (date: {Date})", weekNumber, year, targetDate);

            // Use the coordinator to fetch week letters for all children
            var results = await coordinator.FetchWeekLettersForAllChildrenAsync(targetDate);

            foreach (var (child, weekLetter) in results)
            {
                totalAttempts++;
                if (weekLetter != null && weekLetter["ugebreve"] != null)
                {
                    logger.LogInformation("✅ Preloaded week letter for {ChildName} week {WeekNumber}/{Year}",
                        child.FirstName, weekNumber, year);
                    successCount++;
                }
                else
                {
                    logger.LogInformation("📭 No week letter available for {ChildName} week {WeekNumber}/{Year}",
                        child.FirstName, weekNumber, year);
                }
            }
        }

        logger.LogInformation("📚 Week letter preload complete: {SuccessCount}/{TotalAttempts} successfully loaded",
            successCount, totalAttempts);
    }
}
