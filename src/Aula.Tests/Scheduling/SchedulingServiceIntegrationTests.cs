using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Bots;
using Aula.Channels;
using Aula.Configuration;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Services;

namespace Aula.Tests.Scheduling;

public class SchedulingServiceIntegrationTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<IChannelManager> _mockChannelManager;
    private readonly Config _testConfig;
    private readonly SchedulingService _schedulingService;

    public SchedulingServiceIntegrationTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockAgentService = new Mock<IAgentService>();
        _mockChannelManager = new Mock<IChannelManager>();

        _mockLoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);

        _testConfig = new Config
        {
            Slack = new Aula.Configuration.Slack
            {
                Enabled = true,
                ApiToken = "test-token"
            },
            Telegram = new Aula.Configuration.Telegram
            {
                Enabled = true,
                Token = "test-token"
            },
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Test" },
                    new Child { FirstName = "Søren Johannes", LastName = "Test" }
                }
            }
        };

        _schedulingService = new SchedulingService(
            _mockLoggerFactory.Object,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            _mockChannelManager.Object,
            _testConfig);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        Assert.NotNull(_schedulingService);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingService(
                null!,
                _mockSupabaseService.Object,
                _mockAgentService.Object,
                _mockChannelManager.Object,
                _testConfig));
        Assert.Equal("loggerFactory", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingService(
                _mockLoggerFactory.Object,
                null!,
                _mockAgentService.Object,
                _mockChannelManager.Object,
                _testConfig));
        Assert.Equal("supabaseService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullAgentService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingService(
                _mockLoggerFactory.Object,
                _mockSupabaseService.Object,
                null!,
                _mockChannelManager.Object,
                _testConfig));
        Assert.Equal("agentService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullChannelManager_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingService(
                _mockLoggerFactory.Object,
                _mockSupabaseService.Object,
                _mockAgentService.Object,
                null!,
                _testConfig));
        Assert.Equal("channelManager", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingService(
                _mockLoggerFactory.Object,
                _mockSupabaseService.Object,
                _mockAgentService.Object,
                _mockChannelManager.Object,
                null!));
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidChannelManager_InitializesCorrectly()
    {
        var mockChannelManager = new Mock<IChannelManager>();
        var service = new SchedulingService(
            _mockLoggerFactory.Object,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            mockChannelManager.Object,
            _testConfig);

        Assert.NotNull(service);
    }

    [Fact]
    public async Task StartAsync_LogsInformationAndDoesNotThrow()
    {
        await _schedulingService.StartAsync();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting scheduling service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task StopAsync_LogsInformationAndDoesNotThrow()
    {
        await _schedulingService.StopAsync();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping scheduling service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task StartAsync_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        await _schedulingService.StartAsync();
        await _schedulingService.StartAsync();
        await _schedulingService.StartAsync();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task StopAsync_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        await _schedulingService.StopAsync();
        await _schedulingService.StopAsync();
        await _schedulingService.StopAsync();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task StartStop_Sequence_WorksCorrectly()
    {
        await _schedulingService.StartAsync();
        await _schedulingService.StopAsync();
        await _schedulingService.StartAsync();
        await _schedulingService.StopAsync();

        // Should not throw
        Assert.True(true);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        // Should be able to stop without starting
        await _schedulingService.StopAsync();

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping scheduling service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task StartAsync_WhenCalled_LogsCorrectMessage()
    {
        await _schedulingService.StartAsync();

        _mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting scheduling service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_WhenCalled_LogsCorrectMessage()
    {
        await _schedulingService.StopAsync();

        _mockLogger.Verify(
            logger => logger.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping scheduling service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Service_WithDifferentBotConfigurations_WorksCorrectly(bool slackEnabled, bool telegramEnabled)
    {
        var config = new Config
        {
            Slack = new Aula.Configuration.Slack { Enabled = slackEnabled },
            Telegram = new Aula.Configuration.Telegram { Enabled = telegramEnabled },
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>()
            }
        };

        var mockChannelManager = new Mock<IChannelManager>();

        var service = new SchedulingService(
            _mockLoggerFactory.Object,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            mockChannelManager.Object,
            config);

        await service.StartAsync();
        await service.StopAsync();

        // Should work regardless of bot configuration
        Assert.True(true);
    }
}