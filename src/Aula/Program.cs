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

            var loggedIn = await agentService.LoginAsync();

            if (loggedIn)
            {
                logger.LogInformation("Successfully logged into MinUddannelse");
                foreach (var child in config.Children)
                {
                    logger.LogInformation("Fetching week letter for {ChildName}", child.FirstName);
                    var weekLetter = await agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
                    await slackBot.PostWeekLetter(weekLetter, child);
                    await telegramBot.PostWeekLetter(config.Telegram.ChannelId, weekLetter, child);
                }
            }
            else
            {
                logger.LogError("Failed to log into MinUddannelse");
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

        // Register memory cache
        serviceCollection.AddMemoryCache();

        // Register interfaces and implementations
        serviceCollection.AddSingleton<IMinUddannelseClient, MinUddannelseClient>();
        serviceCollection.AddSingleton<IDataManager, DataManager>();
        serviceCollection.AddSingleton<IAgentService, AgentService>();

        // Register configuration
        var config = LoadConfigurationAsync().GetAwaiter().GetResult();
        serviceCollection.AddSingleton(config);

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
