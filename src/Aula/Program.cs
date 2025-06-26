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
            SlackInteractiveBot? slackInteractiveBot = null;

            // Start the interactive Slack bot if enabled
            if (config.Slack.EnableInteractiveBot && !string.IsNullOrEmpty(config.Slack.ApiToken))
            {
                logger.LogInformation("Starting interactive Slack bot");
                slackInteractiveBot = serviceProvider.GetRequiredService<SlackInteractiveBot>();
                await slackInteractiveBot.Start();
                logger.LogInformation("Interactive Slack bot started");
            }

            var loggedIn = await agentService.LoginAsync();

            if (loggedIn)
            {
                logger.LogInformation("Successfully logged into MinUddannelse");
                
                // Only post week letters on startup if the option is enabled
                if (config.Slack.PostWeekLettersOnStartup)
                {
                    logger.LogInformation("Posting week letters on startup (can be disabled in config)");
                    foreach (var child in config.Children)
                    {
                        logger.LogInformation("Fetching week letter for {ChildName}", child.FirstName);
                        var weekLetter = await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
                        
                        // Use the interactive bot for posting if it's enabled, otherwise use the regular SlackBot
                        if (slackInteractiveBot != null)
                        {
                            var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
                            var weekLetterTitle = $"Uge {weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? ""} - {weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? ""}";
                            
                            // Convert HTML to markdown
                            var html2MarkdownConverter = new Html2SlackMarkdownConverter();
                            var markdownContent = html2MarkdownConverter.Convert(weekLetterContent).Replace("**", "*");
                            
                            // The PostWeekLetter method will check for duplicates
                            await slackInteractiveBot.PostWeekLetter(child.FirstName, markdownContent, weekLetterTitle);
                        }
                        else
                        {
                            await slackBot.PostWeekLetter(weekLetter, child);
                        }
                        
                        if (config.Telegram.Enabled)
                        {
                            await telegramBot.PostWeekLetter(config.Telegram.ChannelId, weekLetter, child);
                        }
                    }
                }
                else
                {
                    logger.LogInformation("Automatic posting of week letters on startup is disabled");
                    
                    // Still fetch and cache the week letters for later use
                    foreach (var child in config.Children)
                    {
                        logger.LogInformation("Fetching and caching week letter for {ChildName}", child.FirstName);
                        await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
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
