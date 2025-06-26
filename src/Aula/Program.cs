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
            var telegramBot = serviceProvider.GetRequiredService<TelegramClient>();
            var agentService = serviceProvider.GetRequiredService<IAgentService>();

            // Start the interactive Slack bot if enabled
            if (config.Slack.EnableInteractiveBot && !string.IsNullOrEmpty(config.Slack.ApiToken))
            {
                logger.LogInformation("Starting interactive Slack bot");
                var slackInteractiveBot = serviceProvider.GetRequiredService<SlackInteractiveBot>();
                await slackInteractiveBot.Start();
                logger.LogInformation("Interactive Slack bot started");
            }

            var loggedIn = await agentService.LoginAsync();

            if (loggedIn)
            {
                logger.LogInformation("Successfully logged into MinUddannelse");
                foreach (var child in config.Children)
                {
                    logger.LogInformation("Fetching week letter for {ChildName}", child.FirstName);
                    var weekLetter = await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
                    await slackBot.PostWeekLetter(weekLetter, child);
                    
                    if (config.Telegram.Enabled)
                    {
                        await telegramBot.PostWeekLetter(config.Telegram.ChannelId, weekLetter, child);
                    }
                    
                    // Demonstrate OpenAI functionality if API key is provided
                    if (!string.IsNullOrEmpty(config.OpenAi.ApiKey))
                    {
                        try
                        {
                            // Get a summary of the week letter
                            logger.LogInformation("Generating summary of week letter for {ChildName}", child.FirstName);
                            var summary = await agentService.SummarizeWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
                            
                            // Post the summary to Slack
                            await slackBot.PostMessage($"*Summary of {child.FirstName}'s week letter:*\n{summary}");
                            
                            // Extract key information
                            logger.LogInformation("Extracting key information from week letter for {ChildName}", child.FirstName);
                            var keyInfo = await agentService.ExtractKeyInformationFromWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
                            
                            // Post the key information to Slack
                            await slackBot.PostMessage($"*Key information from {child.FirstName}'s week letter:*\n```{keyInfo}```");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error using OpenAI services");
                        }
                    }
                }
            }
            else
            {
                logger.LogError("Failed to log into MinUddannelse");
            }

            // If interactive bot is enabled, keep the application running
            if (config.Slack.EnableInteractiveBot && !string.IsNullOrEmpty(config.Slack.ApiToken))
            {
                logger.LogInformation("Interactive bot is running. Press Ctrl+C to exit.");
                
                // Keep the application running
                var exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (sender, eventArgs) => {
                    eventArgs.Cancel = true;
                    exitEvent.Set();
                };
                
                // Wait for Ctrl+C
                exitEvent.WaitOne();
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred");
            throw;
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(configure => configure.AddConsole());

        // Register services
        serviceCollection.AddSingleton<SlackBot>();
        serviceCollection.AddSingleton<TelegramClient>();
        serviceCollection.AddSingleton<SlackInteractiveBot>();

        // Register memory cache
        serviceCollection.AddMemoryCache();

        // Register interfaces and implementations
        serviceCollection.AddSingleton<IMinUddannelseClient, MinUddannelseClient>();
        serviceCollection.AddSingleton<IDataManager, DataManager>();
        serviceCollection.AddSingleton<IAgentService, AgentService>();

        // Register configuration
        var config = LoadConfigurationAsync().GetAwaiter().GetResult();
        serviceCollection.AddSingleton(config);
        
        // Register OpenAI service
        serviceCollection.AddSingleton<IOpenAiService>(provider => 
            new OpenAiService(
                config.OpenAi.ApiKey, 
                provider.GetRequiredService<ILoggerFactory>()));

        return serviceCollection.BuildServiceProvider();
    }

    private static async Task<Config> LoadConfigurationAsync()
    {
        var builder = new ConfigurationBuilder();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Aula.appsettings.json";

        await using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    var jsonConfig = await reader.ReadToEndAsync();
                    builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(jsonConfig)));
                }
            }
        }

        var configuration = builder.Build();
        var config = new Config();
        configuration.Bind(config);
        return config;
    }
}
