using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Supabase;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Configuration;
using Aula.Services;
using Aula.Repositories;
using Aula.Agents;
using Aula.Authentication;
using Aula.Channels;
using Aula.Tools;
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
            logger.LogInformation("Starting MinUddannelse");

            if (!await ValidateConfigurationAsync(serviceProvider, logger))
                return;

            await InitializeSupabaseAsync(serviceProvider, logger);

            var childAgents = await StartChildAgentsAsync(serviceProvider, logger);

            logger.LogInformation("MinUddannelse started");

            var cancellationTokenSource = new CancellationTokenSource();
            ConfigureGracefulShutdown(childAgents, cancellationTokenSource, logger);

            await RunApplicationAsync(logger, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting MinUddannelse");
        }
    }

    private static async Task<bool> ValidateConfigurationAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var config = serviceProvider.GetRequiredService<Config>();
        var configValidator = serviceProvider.GetRequiredService<IConfigurationValidator>();
        var validationResult = await configValidator.ValidateConfigurationAsync(config);

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                logger.LogError("Configuration error: {Error}", error);
            }
            logger.LogError("Application startup failed due to configuration errors");
            return false;
        }

        foreach (var warning in validationResult.Warnings)
        {
            logger.LogWarning("Configuration warning: {Warning}", warning);
        }

        return true;
    }

    private static async Task InitializeSupabaseAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var supabaseClient = serviceProvider.GetRequiredService<Client>();

        // Test connection by attempting to get a simple query
        try
        {
            await supabaseClient.From<Reminder>().Limit(1).Get();
            logger.LogInformation("Supabase connection test successful");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Supabase connection test failed - continuing without database features");
        }
    }

    private static async Task<List<IChildAgent>> StartChildAgentsAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var config = serviceProvider.GetRequiredService<Config>();
        var schedulingService = serviceProvider.GetRequiredService<ISchedulingService>();
        var factory = serviceProvider.GetRequiredService<IChildAgentFactory>();

        await schedulingService.StartAsync();
        logger.LogInformation("SchedulingService started");

        var childAgents = new List<IChildAgent>();
        foreach (var child in config.MinUddannelse?.Children ?? new List<Child>())
        {
            logger.LogInformation("Starting agent for child: {ChildName}", child.FirstName);

            var childAgent = factory.CreateChildAgent(child, schedulingService);
            await childAgent.StartAsync();
            childAgents.Add(childAgent);
        }

        logger.LogInformation("Started {Count} child agents", childAgents.Count);
        return childAgents;
    }

    private static void ConfigureGracefulShutdown(List<IChildAgent> childAgents, CancellationTokenSource cancellationTokenSource, ILogger logger)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
            logger.LogInformation("Shutdown requested");

            foreach (var agent in childAgents)
            {
                _ = agent.StopAsync();
            }
            logger.LogInformation("Stopping {Count} child agents", childAgents.Count);
        };
    }

    private static async Task RunApplicationAsync(ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Application shutting down gracefully");
        }
    }

    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<Config>(provider =>
        {
            var config = new Config();
            configuration.Bind(config);
            return config;
        });

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

        services.AddMemoryCache();

        // Child-aware services are singletons that accept Child parameters
        services.AddSingleton<IChildAuditService, ChildAuditService>();
        services.AddSingleton<IChildRateLimiter, ChildRateLimiter>();
        services.AddSingleton<IWeekLetterService, SecureWeekLetterService>();
        services.AddSingleton<IChildChannelManager, SecureChildChannelManager>();
        services.AddSingleton<IChildScheduler, SecureChildScheduler>();
        services.AddSingleton<IOpenAiService, SecureOpenAiService>();

        // Factory for creating ChildAgent instances with proper dependency resolution
        services.AddSingleton<IChildAgentFactory, ChildAgentFactory>();

        services.AddScoped<DataService>();
        services.AddScoped<IMinUddannelseClient, MinUddannelseClient>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddSingleton<IPromptSanitizer, PromptSanitizer>();
        services.AddSingleton<IMessageContentFilter, MessageContentFilter>();

        services.AddSingleton<IChannelManager, ChannelManager>();

        services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IAiToolsManager, AiToolsManager>();
        services.AddSingleton<IWeekLetterAiService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var aiToolsManager = provider.GetRequiredService<IAiToolsManager>();
            var conversationManager = provider.GetRequiredService<IConversationManager>();
            var promptBuilder = provider.GetRequiredService<IPromptBuilder>();
            return new WeekLetterAiService(config.OpenAi.ApiKey, loggerFactory, aiToolsManager, conversationManager, promptBuilder, config.OpenAi.Model);
        });

        // Supabase Client singleton (initialized asynchronously via factory)
        services.AddSingleton<Client>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SupabaseClient");

            logger.LogInformation("Initializing Supabase connection");

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false, // We don't need realtime for this use case
                AutoRefreshToken = false     // We're using service role key
            };

            var client = new Client(config.Supabase.Url, config.Supabase.ServiceRoleKey, options);
            client.InitializeAsync().GetAwaiter().GetResult();

            logger.LogInformation("Supabase client initialized successfully");
            return client;
        });

        // Repository singletons (no state, all parameters passed as method arguments)
        services.AddSingleton<IReminderRepository, ReminderRepository>();
        services.AddSingleton<IWeekLetterRepository, WeekLetterRepository>();
        services.AddSingleton<IAppStateRepository, AppStateRepository>();
        services.AddSingleton<IRetryTrackingRepository, RetryTrackingRepository>();
        services.AddSingleton<IScheduledTaskRepository, ScheduledTaskRepository>();

        services.AddSingleton<IWeekLetterSeeder, WeekLetterSeeder>();
        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        services.AddSingleton<ISchedulingService, SchedulingService>();

        return services.BuildServiceProvider();
    }

}
