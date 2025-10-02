using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Configuration;
using Aula.MinUddannelse;
using Aula.MinUddannelse;
using Aula.GoogleCalendar;
using Aula.MinUddannelse;
using Aula.MinUddannelse;
using Aula.Core.Security;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using Aula.Scheduling;
using Aula.Communication.Bots;
using Aula.Communication.Channels;

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
        Assert.NotNull(serviceProvider.GetRequiredService<DataService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IMinUddannelseClient>());
        Assert.NotNull(serviceProvider.GetRequiredService<IAgentService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IWeekLetterAiService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISchedulingService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IPromptSanitizer>());
        Assert.NotNull(serviceProvider.GetRequiredService<IMessageContentFilter>());
    }

    [Fact]
    public void ConfigureServices_ShouldHandleConditionalTelegramRegistration()
    {
        var serviceProvider = Program.ConfigureServices();

        // Verify service provider is created successfully regardless of Telegram config
        Assert.NotNull(serviceProvider);

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
