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
            logger.LogInformation("Preloading week letters for all children");
            var agentService = serviceProvider.GetRequiredService<IAgentService>();
            await agentService.LoginAsync();

            await PreloadChildrenWeekLetters(agentService, logger);

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
                var allChildren = await agentService.GetAllChildrenAsync();
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
        services.AddSingleton<IAgentService, AgentService>();
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
            var agentService = provider.GetRequiredService<IAgentService>();
            var channelManager = provider.GetRequiredService<IChannelManager>();
            var config = provider.GetRequiredService<Config>();

            return new SchedulingService(loggerFactory, supabaseService, agentService, channelManager, config);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Preloads week letters for all children to ensure data is available for interactive bots.
    /// This improves response times and provides better user experience.
    /// </summary>
    private static async Task PreloadChildrenWeekLetters(IAgentService agentService, ILogger logger)
    {
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
    }
}
