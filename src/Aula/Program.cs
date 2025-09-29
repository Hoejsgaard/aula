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
                            // Legacy bot architecture removed - week letter posting disabled
                            logger.LogInformation("Week letter posting disabled in current build for child: {ChildName}", args.ChildFirstName);

                            if (false) // Disabled in current build
                            {
                                // Extract week letter content
                                var ugebreve = args.WeekLetter["ugebreve"];
                                if (ugebreve is JArray ugebreveArray && ugebreveArray.Count > 0)
                                {
                                    var content = ugebreveArray[0]?["indhold"]?.ToString() ?? "";
                                    var uge = ugebreveArray[0]?["uge"]?.ToString() ?? "";
                                    var klasseNavn = ugebreveArray[0]?["klasseNavn"]?.ToString() ?? "";

                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        // Convert HTML to Slack markdown
                                        var html2MarkdownConverter = new Html2SlackMarkdownConverter();
                                        var markdownContent = html2MarkdownConverter.Convert(content).Replace("**", "*");

                                        var message = $"📅 *Ugbrev for uge {uge} - {klasseNavn}*\n\n{markdownContent}";
                                        // childBot.SendMessageToSlack(message); // Disabled in current build

                                        logger.LogInformation("Posted week letter for {ChildName}", args.ChildFirstName);
                                    }
                                }
                            }
                            else
                            {
                                logger.LogWarning("No bot found for child {ChildName} or week letter is null", args.ChildFirstName);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing week letter event for child: {ChildName}", args.ChildFirstName);
                        }
                    }
                };

                // Subscribe to schedule events for each child
                schedService.ChildScheduleReady += async (sender, args) =>
                {
                    logger.LogInformation("Received schedule event for child: {ChildName}", args.ChildFirstName);

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
                            // Process the schedule for this specific child
                            logger.LogInformation("Processing schedule for {ChildName} in isolated context", args.ChildFirstName);

                            // TODO: Add specific schedule processing logic here if needed
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing schedule event for child: {ChildName}", args.ChildFirstName);
                        }
                    }
                };
            }

            // Legacy child-specific Slack bots removed

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
                        // Get the week letter from cache using child coordinator
                        using (var scope = new ChildContextScope(serviceProvider, child))
                        {
                            JObject? weekLetter = null; // GetWeekLetter method disabled in current build

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
        services.AddScoped<IChildAwareOpenAiService, SecureChildAwareOpenAiService>();
        services.AddScoped<IChildAgentService, SecureChildAgentService>();
        services.AddSingleton<IChildContextValidator, ChildContextValidator>();
        services.AddSingleton<IChildOperationExecutor, ChildOperationExecutor>();


        // Other singleton services
        // Only register SlackBot if we have a root-level Slack config (which we don't anymore)
        // Or if any child has Slack configured
        var hasSlackConfig = configuration.GetSection("Slack").Exists() ||
                            configuration.GetSection("MinUddannelse:Children")
                                .GetChildren()
                                .Any(c => c.GetValue<bool>("Channels:Slack:Enabled"));


        services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IOpenAiService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var conversationManager = provider.GetRequiredService<IConversationManager>();
            var promptBuilder = provider.GetRequiredService<IPromptBuilder>();
            return new OpenAiService(config.OpenAi.ApiKey, loggerFactory, conversationManager, promptBuilder);
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
