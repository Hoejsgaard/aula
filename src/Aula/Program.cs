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
using Aula.Utilities;

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

            // Week letter storage testing - disabled after confirming fix works
            // await TestWeekLetterStorage(serviceProvider, logger);

            // Preload week letters for all children to ensure data is available for interactive bots
            logger.LogInformation("Preloading week letters for all children");
            var agentService = serviceProvider.GetRequiredService<IAgentService>();
            await agentService.LoginAsync();

            var allChildren = await agentService.GetAllChildrenAsync();
            foreach (var child in allChildren)
            {
                try
                {
                    var weekLetter = await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), false);
                    logger.LogInformation("Preloaded week letter for {ChildName}", child.FirstName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to preload week letter for {ChildName}", child.FirstName);
                }
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

            // Check if we need to post week letters on startup for either Slack or Telegram
            if (config.Slack.PostWeekLettersOnStartup || (config.Telegram.Enabled && config.Telegram.PostWeekLettersOnStartup))
            {
                foreach (var child in allChildren)
                {
                    var weekLetter = await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                    if (weekLetter != null)
                    {
                        // Post to Slack if enabled
                        if (config.Slack.PostWeekLettersOnStartup)
                        {
                            await slackBot.PostWeekLetter(weekLetter, child);
                        }

                        // Post to Telegram if enabled
                        if (config.Telegram.Enabled && config.Telegram.PostWeekLettersOnStartup && telegramInteractiveBot != null)
                        {
                            await telegramInteractiveBot.PostWeekLetter(child.FirstName, weekLetter);
                        }
                    }
                }
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

        // Services
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IMinUddannelseClient>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var supabaseService = provider.GetRequiredService<ISupabaseService>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new MinUddannelseClient(config, supabaseService, loggerFactory);
        });
        services.AddSingleton<SlackBot>();
        services.AddSingleton<TelegramClient>();
        services.AddSingleton<GoogleCalendar>();
        services.AddSingleton<IOpenAiService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var aiToolsManager = provider.GetRequiredService<AiToolsManager>();
            return new OpenAiService(config.OpenAi.ApiKey, loggerFactory, aiToolsManager);
        });
        services.AddSingleton<IAgentService, AgentService>();
        services.AddSingleton<SlackInteractiveBot>();

        // Only register TelegramInteractiveBot if enabled
        var tempConfig = new Config();
        configuration.Bind(tempConfig);
        if (tempConfig.Telegram.Enabled && !string.IsNullOrEmpty(tempConfig.Telegram.Token))
        {
            services.AddSingleton<TelegramInteractiveBot>();
        }

        services.AddSingleton<ISupabaseService, SupabaseService>();
        services.AddSingleton<AiToolsManager>();
        services.AddSingleton<IWeekLetterSeeder, WeekLetterSeeder>();
        services.AddSingleton<ISchedulingService>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var supabaseService = provider.GetRequiredService<ISupabaseService>();
            var agentService = provider.GetRequiredService<IAgentService>();
            var slackBot = provider.GetRequiredService<SlackInteractiveBot>();
            var telegramBot = provider.GetService<TelegramInteractiveBot>(); // May be null
            var config = provider.GetRequiredService<Config>();

            return new SchedulingService(loggerFactory, supabaseService, agentService, slackBot, telegramBot, config);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// ONE-OFF METHOD: Test week letter storage fix
    /// This tests if the database storage issue for "Soren Johannes" is resolved
    /// Remove this method once storage is confirmed working
    /// </summary>
    private static async Task TestWeekLetterStorage(IServiceProvider serviceProvider, ILogger logger)
    {
        try
        {
            var agentService = serviceProvider.GetRequiredService<IAgentService>();
            var supabaseService = serviceProvider.GetRequiredService<ISupabaseService>();
            var config = serviceProvider.GetRequiredService<Config>();

            logger.LogInformation("🧪 Testing week letter storage for both children");

            // Login to MinUddannelse
            var loginSuccess = await agentService.LoginAsync();
            if (!loginSuccess)
            {
                logger.LogWarning("Failed to login to MinUddannelse - skipping storage test");
                return;
            }

            var allChildren = await agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                logger.LogWarning("No children configured - skipping storage test");
                return;
            }

            // Test with current week to ensure we have data
            var today = DateOnly.FromDateTime(DateTime.Today);
            var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue));
            var currentYear = today.Year;

            logger.LogInformation("🧪 Testing storage for week {WeekNumber}/{Year}", currentWeek, currentYear);

            foreach (var child in allChildren)
            {
                try
                {
                    logger.LogInformation("🧪 Testing storage for child: '{ChildFirstName}'", child.FirstName);
                    
                    // Try to fetch current week letter
                    var weekLetter = await agentService.GetWeekLetterAsync(child, today, false);
                    if (weekLetter != null)
                    {
                        // Test storage
                        var contentHash = ComputeContentHash(weekLetter.ToString());
                        await supabaseService.StoreWeekLetterAsync(
                            child.FirstName,
                            currentWeek,
                            currentYear,
                            contentHash,
                            weekLetter.ToString(),
                            false,
                            false);

                        logger.LogInformation("✅ Successfully stored week letter for {ChildName}", child.FirstName);
                        
                        // Test retrieval
                        var retrievedContent = await supabaseService.GetStoredWeekLetterAsync(child.FirstName, currentWeek, currentYear);
                        if (!string.IsNullOrEmpty(retrievedContent))
                        {
                            logger.LogInformation("✅ Successfully retrieved stored week letter for {ChildName}", child.FirstName);
                        }
                        else
                        {
                            logger.LogWarning("❌ Failed to retrieve stored week letter for {ChildName}", child.FirstName);
                        }
                    }
                    else
                    {
                        logger.LogInformation("⚠️ No week letter available for {ChildName} this week", child.FirstName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Error testing storage for {ChildName}", child.FirstName);
                }
            }

            logger.LogInformation("🧪 Storage test complete - check logs above for results");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during storage test");
        }
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
