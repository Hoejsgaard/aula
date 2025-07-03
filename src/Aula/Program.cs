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

            // ONE-OFF: Populate database with past 8 weeks of week letters
            // Remove this section once historical data is seeded
            if (connectionTest && config.Features?.UseStoredWeekLetters == true)
            {
                logger.LogInformation("🗂️ Starting one-off historical week letter population");
                await PopulateHistoricalWeekLetters(serviceProvider, logger);
            }

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
    /// ONE-OFF METHOD: Populates database with week letters from the past 8 weeks
    /// This helps with testing during summer holidays when no fresh week letters are available
    /// Remove this method once historical data has been seeded
    /// </summary>
    private static async Task PopulateHistoricalWeekLetters(IServiceProvider serviceProvider, ILogger logger)
    {
        try
        {
            var agentService = serviceProvider.GetRequiredService<IAgentService>();
            var supabaseService = serviceProvider.GetRequiredService<ISupabaseService>();
            var config = serviceProvider.GetRequiredService<Config>();

            logger.LogInformation("📅 Fetching historical week letters from weeks 8-20 ago (avoiding recent summer holidays)");

            // Login to MinUddannelse
            var loginSuccess = await agentService.LoginAsync();
            if (!loginSuccess)
            {
                logger.LogWarning("Failed to login to MinUddannelse - skipping historical data population");
                return;
            }

            var allChildren = await agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                logger.LogWarning("No children configured - skipping historical data population");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            logger.LogInformation("📅 Today is: {Today} (calculated from DateTime.Today: {DateTimeToday})", today, DateTime.Today);
            var successCount = 0;
            var totalAttempts = 0;

            // Go back 8-20 weeks from today to find school weeks (avoiding summer holidays)
            for (int weeksBack = 8; weeksBack <= 20; weeksBack++)
            {
                var targetDate = today.AddDays(-7 * weeksBack);
                var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(targetDate.ToDateTime(TimeOnly.MinValue));
                var year = targetDate.Year;

                logger.LogInformation("📆 Processing week {WeekNumber}/{Year} (date: {Date})", weekNumber, year, targetDate);

                foreach (var child in allChildren)
                {
                    totalAttempts++;

                    try
                    {
                        // Check if we already have this week letter stored
                        var existingContent = await supabaseService.GetStoredWeekLetterAsync(child.FirstName, weekNumber, year);
                        if (!string.IsNullOrEmpty(existingContent))
                        {
                            logger.LogInformation("✅ Week letter for {ChildName} week {WeekNumber}/{Year} already exists - skipping",
                                child.FirstName, weekNumber, year);
                            successCount++;
                            continue;
                        }

                        // Try to fetch week letter for this historical date
                        var weekLetter = await agentService.GetWeekLetterAsync(child, targetDate, false);
                        if (weekLetter != null)
                        {
                            // Check if it has actual content (not just the "no week letter" placeholder)
                            var content = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(content) && !content.Contains("Der er ikke skrevet nogen ugenoter"))
                            {
                                // Store the week letter
                                var contentHash = ComputeContentHash(weekLetter.ToString());
                                await supabaseService.StoreWeekLetterAsync(
                                    child.FirstName,
                                    weekNumber,
                                    year,
                                    contentHash,
                                    weekLetter.ToString(),
                                    false,
                                    false);

                                successCount++;
                                logger.LogInformation("✅ Stored week letter for {ChildName} week {WeekNumber}/{Year} ({ContentLength} chars)",
                                    child.FirstName, weekNumber, year, content.Length);
                            }
                            else
                            {
                                logger.LogInformation("⚠️ Week letter for {ChildName} week {WeekNumber}/{Year} has no content - skipping",
                                    child.FirstName, weekNumber, year);
                            }
                        }
                        else
                        {
                            logger.LogInformation("⚠️ No week letter available for {ChildName} week {WeekNumber}/{Year}",
                                child.FirstName, weekNumber, year);
                        }

                        // Small delay to be respectful to the API
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "❌ Error fetching week letter for {ChildName} week {WeekNumber}/{Year}",
                            child.FirstName, weekNumber, year);
                    }
                }
            }

            logger.LogInformation("🎉 Historical week letter population complete: {SuccessCount}/{TotalAttempts} successful",
                successCount, totalAttempts);

            if (successCount > 0)
            {
                logger.LogInformation("📊 You can now test with stored week letters by setting Features.UseStoredWeekLetters = true");
                logger.LogInformation("🔧 Remember to remove this PopulateHistoricalWeekLetters method once you're done seeding data");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error during historical week letter population");
        }
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
