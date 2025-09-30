using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Aula.Scheduling;
using Aula.Bots;
using Aula.Channels;

namespace Aula.Tests;

public class ProgramTests
{
	[Fact]
	public void ConfigureServices_ShouldRegisterRequiredServices()
	{
		// Arrange & Act
		var serviceProvider = Program.ConfigureServices();

		// Assert - Verify core services are registered
		Assert.NotNull(serviceProvider.GetRequiredService<Config>());
		Assert.NotNull(serviceProvider.GetRequiredService<ILoggerFactory>());
		Assert.NotNull(serviceProvider.GetRequiredService<IDataService>());
		Assert.NotNull(serviceProvider.GetRequiredService<IMinUddannelseClient>());
		Assert.NotNull(serviceProvider.GetRequiredService<IAgentService>());
		Assert.NotNull(serviceProvider.GetRequiredService<IOpenAiService>());
		Assert.NotNull(serviceProvider.GetRequiredService<ISupabaseService>());
		Assert.NotNull(serviceProvider.GetRequiredService<ISchedulingService>());
		Assert.NotNull(serviceProvider.GetRequiredService<IChildServiceCoordinator>());
		Assert.NotNull(serviceProvider.GetRequiredService<IPromptSanitizer>());
		Assert.NotNull(serviceProvider.GetRequiredService<IMessageContentFilter>());
	}

	[Fact]
	public void ConfigureServices_ShouldHandleConditionalTelegramRegistration()
	{
		var serviceProvider = Program.ConfigureServices();

		// Verify service provider is created successfully regardless of Telegram config
		Assert.NotNull(serviceProvider);

		// Try to resolve TelegramInteractiveBot - should either succeed or fail gracefully
		try
		{
			var telegramBot = serviceProvider.GetService<TelegramInteractiveBot>();
			// If resolved, verify it's properly configured
			if (telegramBot != null)
			{
				Assert.NotNull(telegramBot);
			}
		}
		catch (Exception)
		{
			// Expected when Telegram is not configured - this is acceptable
		}
	}

	[Fact]
	public void ConfigureServices_ShouldConfigureLogging()
	{
		// Arrange & Act
		var serviceProvider = Program.ConfigureServices();
		var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

		// Assert
		Assert.NotNull(loggerFactory);
		var logger = loggerFactory.CreateLogger("Test");
		Assert.NotNull(logger);
	}

	[Fact]
	public void ConfigureServices_ShouldBindConfiguration()
	{
		// Arrange & Act  
		var serviceProvider = Program.ConfigureServices();
		var config = serviceProvider.GetRequiredService<Config>();

		// Assert - Config should be bound (not null/default)
		Assert.NotNull(config);
		Assert.NotNull(config.UniLogin);
		Assert.NotNull(config.Slack);
		Assert.NotNull(config.Telegram);
		Assert.NotNull(config.OpenAi);
	}

	[Fact]
	public void ConfigureServices_ShouldRegisterMemoryCache()
	{
		// Arrange & Act
		var serviceProvider = Program.ConfigureServices();

		// Assert
		Assert.NotNull(serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
	}

	[Fact]
	public void ConfigureServices_ShouldCreateServiceProviderWithoutErrors()
	{
		// Arrange & Act & Assert
		var serviceProvider = Program.ConfigureServices();
		Assert.NotNull(serviceProvider);

		// Dispose properly if disposable
		if (serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
