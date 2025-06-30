using Microsoft.Extensions.Logging;
using Moq;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Bots;
using Aula.Configuration;
using Aula.Services;
using ConfigSlack = Aula.Configuration.Slack;
using ConfigTelegram = Aula.Configuration.Telegram;
using ConfigChild = Aula.Configuration.Child;

namespace Aula.Tests;

public class SchedulingServiceTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Config _testConfig;

    public SchedulingServiceTests()
    {
        _loggerFactory = new LoggerFactory();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockAgentService = new Mock<IAgentService>();

        _testConfig = new Config
        {
            Slack = new ConfigSlack { EnableInteractiveBot = true, ApiToken = "test-slack-token" },
            Telegram = new ConfigTelegram { Enabled = true, ChannelId = "@testchannel", Token = "test-telegram-token" },
            Children = new List<ConfigChild>
            {
                new ConfigChild { FirstName = "TestChild", LastName = "TestLast" }
            }
        };
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        var telegramBot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);

        // Act
        var schedulingService = new SchedulingService(
            _loggerFactory,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            slackBot,
            telegramBot,
            _testConfig);

        // Assert
        Assert.NotNull(schedulingService);
    }

    [Fact]
    public async Task StartAsync_WithValidSetup_DoesNotThrow()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());
        _mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
            .ReturnsAsync(new List<ScheduledTask>());

        var slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        var telegramBot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);

        var schedulingService = new SchedulingService(
            _loggerFactory,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            slackBot,
            telegramBot,
            _testConfig);

        // Act & Assert - Should not throw
        await schedulingService.StartAsync();

        // Cleanup
        await schedulingService.StopAsync();
        slackBot.Dispose();
        // TelegramInteractiveBot does not implement IDisposable
    }

    [Fact]
    public async Task StopAsync_AfterStart_DoesNotThrow()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());
        _mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
            .ReturnsAsync(new List<ScheduledTask>());

        var slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        var telegramBot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);

        var schedulingService = new SchedulingService(
            _loggerFactory,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            slackBot,
            telegramBot,
            _testConfig);

        await schedulingService.StartAsync();

        // Act & Assert - Should not throw
        await schedulingService.StopAsync();

        // Cleanup
        slackBot.Dispose();
        // TelegramInteractiveBot does not implement IDisposable
    }

    [Fact]
    public async Task StartAsync_CallsGetPendingRemindersAsync_AtLeastOnce()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());
        _mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
            .ReturnsAsync(new List<ScheduledTask>());

        var slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        var telegramBot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);

        var schedulingService = new SchedulingService(
            _loggerFactory,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            slackBot,
            telegramBot,
            _testConfig);

        // Act
        await schedulingService.StartAsync();

        // Wait a moment for async operations to complete
        await Task.Delay(100);

        // Assert - The service should have called GetPendingRemindersAsync
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.AtLeastOnce);
        // Note: GetScheduledTasksAsync may be called on a different timer interval

        // Cleanup
        await schedulingService.StopAsync();
        slackBot.Dispose();
        // TelegramInteractiveBot does not implement IDisposable
    }
}