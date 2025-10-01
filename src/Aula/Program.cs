using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Configuration;
using Aula.Services;
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
			logger.LogInformation("Starting aula");

			if (!await ValidateConfigurationAsync(serviceProvider, logger))
				return;

			await InitializeSupabaseAsync(serviceProvider, logger);

			var childAgents = await StartChildAgentsAsync(serviceProvider, logger);

			logger.LogInformation("Aula started");

			var cancellationTokenSource = new CancellationTokenSource();
			ConfigureGracefulShutdown(childAgents, cancellationTokenSource, logger);

			await RunApplicationAsync(logger, cancellationTokenSource.Token);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error starting aula");
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
		var supabaseService = serviceProvider.GetRequiredService<ISupabaseService>();
		await supabaseService.InitializeAsync();

		var connectionTest = await supabaseService.TestConnectionAsync();
		if (!connectionTest)
		{
			logger.LogWarning("Supabase connection test failed - continuing without database features");
		}
		else
		{
			logger.LogInformation("Supabase connection test successful");
		}
	}

	private static async Task<List<IChildAgent>> StartChildAgentsAsync(IServiceProvider serviceProvider, ILogger logger)
	{
		var config = serviceProvider.GetRequiredService<Config>();
		var schedulingService = serviceProvider.GetRequiredService<ISchedulingService>();
		var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

		await schedulingService.StartAsync();
		logger.LogInformation("SchedulingService started");

		var childAgents = new List<IChildAgent>();
		foreach (var child in config.MinUddannelse?.Children ?? new List<Child>())
		{
			logger.LogInformation("Starting agent for child: {ChildName}", child.FirstName);
			var childAgent = new ChildAgent(child, serviceProvider, config, schedulingService, loggerFactory);
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
		services.AddSingleton<IChildDataService, SecureChildDataService>();
		services.AddSingleton<IChildChannelManager, SecureChildChannelManager>();
		services.AddSingleton<IChildScheduler, SecureChildScheduler>();
		services.AddSingleton<IChildAwareOpenAiService, SecureChildAwareOpenAiService>();

		services.AddScoped<IDataService, DataService>();
		services.AddScoped<IMinUddannelseClient, MinUddannelseClient>();
		services.AddScoped<IAgentService, AgentService>();
		services.AddSingleton<IPromptSanitizer, PromptSanitizer>();
		services.AddSingleton<IMessageContentFilter, MessageContentFilter>();

		services.AddSingleton<IChannelManager, ChannelManager>();

		services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
		services.AddSingleton<IConversationManager, ConversationManager>();
		services.AddSingleton<IPromptBuilder, PromptBuilder>();
		services.AddSingleton<IAiToolsManager, AiToolsManager>();
		services.AddSingleton<IOpenAiService>(provider =>
		{
			var config = provider.GetRequiredService<Config>();
			var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
			var aiToolsManager = provider.GetRequiredService<IAiToolsManager>();
			var conversationManager = provider.GetRequiredService<IConversationManager>();
			var promptBuilder = provider.GetRequiredService<IPromptBuilder>();
			return new OpenAiService(config.OpenAi.ApiKey, loggerFactory, aiToolsManager, conversationManager, promptBuilder);
		});

		services.AddSingleton<ISupabaseService, SupabaseService>();
		services.AddSingleton<IWeekLetterSeeder, WeekLetterSeeder>();
		services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

		services.AddSingleton<ISchedulingService, SchedulingService>();

		return services.BuildServiceProvider();
	}

}
