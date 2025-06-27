using System.Reflection;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            // Start Slack interactive bot if enabled
            SlackInteractiveBot? slackInteractiveBot = null;
            if (config.Slack.EnableInteractiveBot)
            {
                slackInteractiveBot = serviceProvider.GetRequiredService<SlackInteractiveBot>();
                await slackInteractiveBot.Start();
            }

            // Start Telegram interactive bot if enabled
            TelegramInteractiveBot? telegramInteractiveBot = null;
            if (config.Telegram.Enabled)
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

            logger.LogInformation("Aula started");

            // Keep the application running
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting aula");
        }
    }

    private static IServiceProvider ConfigureServices()
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

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        // Memory cache
        services.AddMemoryCache();

        // Services
        services.AddSingleton<IDataManager, DataManager>();
        services.AddSingleton<IMinUddannelseClient, MinUddannelseClient>();
        services.AddSingleton<SlackBot>();
        services.AddSingleton<TelegramClient>();
        services.AddSingleton<GoogleCalendar>();
        services.AddSingleton<IOpenAiService>(provider => 
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new OpenAiService(config.OpenAi.ApiKey, loggerFactory);
        });
        services.AddSingleton<IAgentService, AgentService>();
        services.AddSingleton<SlackInteractiveBot>();
        services.AddSingleton<TelegramInteractiveBot>();

        return services.BuildServiceProvider();
    }
}
