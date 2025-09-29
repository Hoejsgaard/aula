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

            var slackBot = serviceProvider.GetRequiredService<SlackBot>();
            var telegramClient = serviceProvider.GetRequiredService<TelegramClient>();

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

            // Historical data seeding (controlled by configuration)
            if (connectionTest && config.Features?.SeedHistoricalData == true)
            {
                logger.LogInformation("🗂️ Starting historical week letter population");
                var historicalDataSeeder = serviceProvider.GetRequiredService<IHistoricalDataSeeder>();
                await historicalDataSeeder.SeedHistoricalWeekLettersAsync();
            }

            // Preload week letters for all children to ensure data is available for interactive bots
            var coordinator = serviceProvider.GetRequiredService<IChildServiceCoordinator>();
            if (config.Features?.PreloadWeekLettersOnStartup == true)
            {
                logger.LogInformation("Preloading week letters for all children");
                await PreloadChildrenWeekLetters(coordinator, config, logger);
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

            // Start Slack interactive bot if enabled
            SlackInteractiveBot? slackInteractiveBot = null;
            if (config.Slack.EnableInteractiveBot)
            {
                slackInteractiveBot = serviceProvider.GetRequiredService<SlackInteractiveBot>();
                await slackInteractiveBot.Start();
            }

            // Start Telegram interactive bot if enabled
            TelegramInteractiveBot? telegramInteractiveBot = null;
            if (config.Telegram.Enabled && !string.IsNullOrEmpty(config.Telegram.Token))
            {
                telegramInteractiveBot = serviceProvider.GetRequiredService<TelegramInteractiveBot>();
                await telegramInteractiveBot.Start();
            }

            // Check if we need to post week letters on startup
            if (config.Features?.PostWeekLettersOnStartup == true)
            {
                await coordinator.PostWeekLettersForAllChildrenAsync(
                    DateOnly.FromDateTime(DateTime.Today),
                    async (child, weekLetter) =>
                    {
                        if (weekLetter != null)
                        {
                            // Post to Slack if enabled
                            if (config.Slack.Enabled)
                            {
                                await slackBot.PostWeekLetter(weekLetter, child);
                            }

                            // Post to Telegram if enabled
                            if (config.Telegram.Enabled && telegramInteractiveBot != null)
                            {
                                await telegramInteractiveBot.PostWeekLetter(child.FirstName, weekLetter);
                            }
                        }
                    });
            }

            // Scheduling service already started above - removed duplicate

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
        services.AddScoped<IChildDataService, SecureChildDataService>();
        services.AddScoped<IChildAgentService, SecureChildAgentService>();
        services.AddScoped<IChildAuthenticationService, SecureChildAuthenticationService>();
        services.AddScoped<IChildAwareOpenAiService, SecureChildAwareOpenAiService>();
        services.AddScoped<IChildAuditService, ChildAuditService>();
        services.AddScoped<IChildRateLimiter, ChildRateLimiter>();
        services.AddSingleton<IChildContextValidator, ChildContextValidator>();
        services.AddSingleton<IChildOperationExecutor, ChildOperationExecutor>();
        services.AddSingleton<IChildServiceCoordinator, ChildServiceCoordinator>();

        // Legacy services (marked as obsolete, will be removed in future version)
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IMinUddannelseClient>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var supabaseService = provider.GetRequiredService<ISupabaseService>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

            // Use the new per-child authentication client
            return new PerChildMinUddannelseClient(config, supabaseService, loggerFactory);
        });
        services.AddSingleton<IAgentService, AgentService>();

        // Other singleton services
        services.AddSingleton<SlackBot>();
        services.AddSingleton<TelegramClient>();
        services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IOpenAiService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var aiToolsManager = provider.GetRequiredService<IAiToolsManager>();
            var conversationManager = provider.GetRequiredService<IConversationManager>();
            var promptBuilder = provider.GetRequiredService<IPromptBuilder>();
            return new OpenAiService(config.OpenAi.ApiKey, loggerFactory, aiToolsManager, conversationManager, promptBuilder);
        });
        services.AddSingleton<SlackInteractiveBot>();

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
            services.AddSingleton<TelegramChannelMessenger>();
            services.AddSingleton<TelegramInteractiveBot>();
        }

        services.AddSingleton<ISupabaseService, SupabaseService>();
        services.AddSingleton<IAiToolsManager, AiToolsManager>();
        services.AddSingleton<IWeekLetterSeeder, WeekLetterSeeder>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
        services.AddSingleton<IHistoricalDataSeeder, HistoricalDataSeeder>();

        // Channel Manager and Channel Registration
        services.AddSingleton<IChannelManager>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var config = provider.GetRequiredService<Config>();
            var channelManager = new ChannelManager(loggerFactory);

            // Register Slack channel if enabled
            if (config.Slack.Enabled)
            {
                var slackBot = provider.GetService<SlackInteractiveBot>();
                var slackChannel = new SlackChannel(config, loggerFactory, slackBot);
                channelManager.RegisterChannel(slackChannel);
            }

            // Register Telegram channel if enabled
            if (config.Telegram.Enabled)
            {
                var telegramBot = provider.GetService<TelegramInteractiveBot>();
                var telegramMessenger = provider.GetService<TelegramChannelMessenger>();
                var telegramChannel = new TelegramChannel(config, loggerFactory, telegramMessenger!, telegramBot);
                channelManager.RegisterChannel(telegramChannel);
            }

            return channelManager;
        });
        services.AddSingleton<ISchedulingService>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var supabaseService = provider.GetRequiredService<ISupabaseService>();
            var coordinator = provider.GetRequiredService<IChildServiceCoordinator>();
            var channelManager = provider.GetRequiredService<IChannelManager>();
            var config = provider.GetRequiredService<Config>();

            // Updated to use IChildServiceCoordinator for proper child-aware architecture
            return new SchedulingService(loggerFactory, supabaseService, coordinator, channelManager, config);
        });

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
