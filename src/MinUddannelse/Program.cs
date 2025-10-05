using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Supabase;
using System.Linq;
using System.Net;
using MinUddannelse.Agents;
using MinUddannelse.AI.Prompts;
using MinUddannelse.AI.Services;
using MinUddannelse.Configuration;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.GoogleCalendar;
using MinUddannelse.Client;
using MinUddannelse.Models;
using MinUddannelse.Repositories;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Scheduling;
using MinUddannelse.Security;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MinUddannelse;

public class Program
{
    public static async Task Main(string[] args)
    {
        var serviceProvider = ConfigureServices();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(Program));

        try
        {
            // Check for test commands first
            var config = serviceProvider.GetRequiredService<Config>();


            logger.LogInformation("Starting MinUddannelse");

            if (!await ValidateConfigurationAsync(serviceProvider, logger))
                return;

            await InitializeSupabaseAsync(serviceProvider, logger);

            var childAgents = await StartChildAgentsAsync(serviceProvider, logger);

            await TriggerStartupAIAnalysisAsync(serviceProvider, logger, childAgents);

            logger.LogInformation("MinUddannelse started");

            var cancellationTokenSource = new CancellationTokenSource();
            ConfigureGracefulShutdown(childAgents, cancellationTokenSource, logger);

            await RunApplicationAsync(logger, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting MinUddannelse");
        }
        finally
        {
            Log.CloseAndFlush();
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
        var supabaseClient = serviceProvider.GetRequiredService<Supabase.Client>();

        try
        {
            await supabaseClient.InitializeAsync();
            logger.LogInformation("Supabase client initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Supabase client initialization failed - continuing without database features");
            return;
        }

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

    private static async Task TriggerStartupAIAnalysisAsync(IServiceProvider serviceProvider, ILogger logger, List<IChildAgent> childAgents)
    {
        try
        {
            var config = serviceProvider.GetRequiredService<Config>();
            if (!config.WeekLetter.RunThisWeeksAIAnalysisOnStartup)
            {
                logger.LogDebug("Startup AI analysis is disabled in configuration");
                return;
            }

            logger.LogInformation("Running startup AI analysis for current week");

            var schedulingService = serviceProvider.GetRequiredService<ISchedulingService>();
            var weekLetterReminderService = serviceProvider.GetRequiredService<IWeekLetterReminderService>();
            var weekLetterService = serviceProvider.GetRequiredService<IWeekLetterService>();

            var now = DateTime.Now;
            var currentDate = DateOnly.FromDateTime(now);
            var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
            var currentYear = now.Year;

            foreach (var child in config.MinUddannelse?.Children ?? new List<Child>())
            {
                try
                {
                    logger.LogInformation("Running AI analysis for {ChildName} week {Week}/{Year}",
                        child.FirstName, currentWeek, currentYear);

                    // Get current week letter
                    var weekLetter = await weekLetterService.GetOrFetchWeekLetterAsync(child, currentDate, false);
                    if (weekLetter == null)
                    {
                        logger.LogWarning("No week letter found for {ChildName} week {Week}/{Year}",
                            child.FirstName, currentWeek, currentYear);
                        continue;
                    }

                    // Get repository services to check for existing posted letter
                    var weekLetterRepository = serviceProvider.GetRequiredService<IWeekLetterRepository>();

                    // Check if this week letter has already been posted
                    var existingPostedLetter = await weekLetterRepository.GetPostedLetterByHashAsync(child.FirstName, currentWeek, currentYear);

                    string contentHash;
                    if (existingPostedLetter != null)
                    {
                        // Use existing content hash from posted letter
                        contentHash = existingPostedLetter.ContentHash;
                        logger.LogInformation("Using existing posted letter hash for {ChildName} week {Week}/{Year}",
                            child.FirstName, currentWeek, currentYear);
                    }
                    else
                    {
                        // Create content hash from current week letter
                        var content = ExtractWeekLetterContent(weekLetter);
                        contentHash = ComputeContentHash(content);

                        // Check if we should create a posted letter record for analysis
                        // This is only needed if the week letter was never processed before
                        var hasBeenPosted = await weekLetterRepository.HasWeekLetterBeenPostedAsync(child.FirstName, currentWeek, currentYear);
                        if (!hasBeenPosted)
                        {
                            // Create minimal posted letter record specifically for AI analysis
                            // Note: PostedToSlack/Telegram are false since this is analysis-only
                            await weekLetterRepository.StoreWeekLetterAsync(
                                child.FirstName, currentWeek, currentYear, contentHash,
                                weekLetter.ToString(), false, false);

                            logger.LogInformation("Created posted letter record for startup AI analysis: {ChildName} week {Week}/{Year}",
                                child.FirstName, currentWeek, currentYear);
                        }
                    }

                    // Reset AutoRemindersExtracted flag to force reprocessing when RunThisWeeksAIAnalysisOnStartup is enabled
                    await weekLetterRepository.ResetAutoRemindersExtractedAsync(child.FirstName, currentWeek, currentYear);
                    logger.LogInformation("Reset AutoRemindersExtracted flag for {ChildName} week {Week}/{Year} to enable reprocessing",
                        child.FirstName, currentWeek, currentYear);

                    // Run AI analysis
                    var extractionResult = await weekLetterReminderService.ExtractAndStoreRemindersAsync(
                        child.FirstName, currentWeek, currentYear, weekLetter, contentHash);

                    if (extractionResult.Success && extractionResult.RemindersCreated > 0)
                    {
                        logger.LogInformation("Created {Count} reminders for {ChildName}",
                            extractionResult.RemindersCreated, child.FirstName);

                        // Send success message to child's channels
                        var successMessage = FormatReminderSuccessMessage(extractionResult.RemindersCreated, currentWeek, extractionResult.CreatedReminders);
                        var childAgent = childAgents.FirstOrDefault(a => a.Child.FirstName == child.FirstName);
                        if (childAgent != null)
                        {
                            await childAgent.SendReminderMessageAsync(successMessage);
                        }
                        else
                        {
                            logger.LogWarning("No child agent found for {ChildName}", child.FirstName);
                        }
                    }
                    else if (extractionResult.Success && extractionResult.NoRemindersFound)
                    {
                        logger.LogInformation("No reminders found for {ChildName}", child.FirstName);
                    }
                    else
                    {
                        logger.LogWarning("AI analysis failed for {ChildName}: {Error}",
                            child.FirstName, extractionResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error running startup AI analysis for {ChildName}", child.FirstName);
                }
            }

            logger.LogInformation("Startup AI analysis completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during startup AI analysis");
        }
    }

    private static string FormatReminderSuccessMessage(int reminderCount, int weekNumber, List<CreatedReminderInfo> createdReminders)
    {
        var message = $"Jeg har oprettet {reminderCount} påmindelser for uge {weekNumber}:";

        // Group reminders by day
        var groupedByDay = createdReminders
            .GroupBy(r => r.Date.ToString("dddd", new System.Globalization.CultureInfo("da-DK")))
            .OrderBy(g => createdReminders.First(r => r.Date.ToString("dddd", new System.Globalization.CultureInfo("da-DK")) == g.Key).Date);

        foreach (var dayGroup in groupedByDay)
        {
            foreach (var reminder in dayGroup)
            {
                var timeInfo = !string.IsNullOrEmpty(reminder.EventTime) ? $" kl. {reminder.EventTime}" : "";
                message += $"\n• {char.ToUpper(dayGroup.Key[0])}{dayGroup.Key.Substring(1)}: {reminder.Title}{timeInfo}";
            }
        }

        return message;
    }

    private static string ExtractWeekLetterContent(dynamic weekLetter)
    {
        if (weekLetter is Newtonsoft.Json.Linq.JObject jobject)
        {
            return MinUddannelse.Content.WeekLetters.WeekLetterContentExtractor.ExtractContent(jobject, null);
        }
        throw new ArgumentException("Expected JObject for week letter content extraction", nameof(weekLetter));
    }

    private static string ComputeContentHash(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
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
            .AddJsonStream(GetEmbeddedAppsettings())
            .Build();

        services.AddSingleton<Config>(provider =>
        {
            var config = new Config();
            configuration.Bind(config);
            return config;
        });

        // Configure Serilog for both console and file logging with rotation
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "minuddannelse-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50_000_000,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        services.AddMemoryCache();

        services.AddHttpClient("UniLogin", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.Timeout = TimeSpan.FromMinutes(2); // Explicit timeout
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        });

        // Child-aware services are singletons that accept Child parameters
        services.AddSingleton<IChildAuditService, ChildAuditService>();
        services.AddSingleton<IChildRateLimiter, ChildRateLimiter>();
        services.AddSingleton<IWeekLetterService, WeekLetterService>();
        services.AddSingleton<IChildScheduler, ChildScheduler>();
        services.AddSingleton<IOpenAiService, OpenAiService>();

        services.AddSingleton<IChildAgentFactory, ChildAgentFactory>();

        services.AddScoped<WeekLetterCache>();
        services.AddScoped<IMinUddannelseClient, MinUddannelseClient>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddSingleton<IPromptSanitizer, PromptSanitizer>();


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

        // Supabase Client singleton (lazy initialization on first use)
        services.AddSingleton<Supabase.Client>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SupabaseClient");

            logger.LogInformation("Creating Supabase client (will initialize on first use)");

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false, // We don't need realtime for this use case
                AutoRefreshToken = false     // We're using service role key
            };

            var client = new Supabase.Client(config.Supabase.Url, config.Supabase.ServiceRoleKey, options);
            // Removed blocking InitializeAsync().GetAwaiter().GetResult() call
            // Client will be initialized on first repository use

            logger.LogInformation("Supabase client created successfully");
            return client;
        });

        // Repository singletons (no state, all parameters passed as method arguments)
        services.AddSingleton<IReminderRepository, ReminderRepository>();
        services.AddSingleton<IWeekLetterRepository, WeekLetterRepository>();
        services.AddSingleton<IAppStateRepository, AppStateRepository>();
        services.AddSingleton<IRetryTrackingRepository, RetryTrackingRepository>();
        services.AddSingleton<IScheduledTaskRepository, ScheduledTaskRepository>();

        services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

        services.AddSingleton<ISchedulingService>(provider =>
        {
            return new SchedulingService(
                provider.GetRequiredService<ILoggerFactory>(),
                provider.GetRequiredService<IReminderRepository>(),
                provider.GetRequiredService<IScheduledTaskRepository>(),
                provider.GetRequiredService<IWeekLetterRepository>(),
                provider.GetRequiredService<IRetryTrackingRepository>(),
                provider.GetRequiredService<IAppStateRepository>(),
                provider.GetRequiredService<IWeekLetterService>(),
                provider.GetRequiredService<IWeekLetterReminderService>(),
                provider.GetRequiredService<Config>());
        });

        services.AddSingleton<IWeekLetterReminderService>(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            return new WeekLetterReminderService(
                provider.GetRequiredService<IOpenAiService>(),
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<WeekLetterReminderService>(),
                provider.GetRequiredService<IWeekLetterRepository>(),
                provider.GetRequiredService<IReminderRepository>(),
                config.OpenAi.Model,
                TimeOnly.Parse(config.Scheduling.DefaultOnDateReminderTime));
        });

        return services.BuildServiceProvider();
    }

    private static Stream GetEmbeddedAppsettings()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = "MinUddannelse.appsettings.json";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        return stream;
    }

}
