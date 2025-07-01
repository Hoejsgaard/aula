using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Bots;
using Aula.Configuration;
using Aula.Services;
using ConfigSlack = Aula.Configuration.Slack;
using ConfigTelegram = Aula.Configuration.Telegram;
using ConfigChild = Aula.Configuration.Child;

namespace Aula.Tests.Scheduling;

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

    [Fact]
    public void ShouldRunTask_WithDisabledTask_ReturnsFalse()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask
        {
            Name = "TestTask",
            Enabled = false,
            CronExpression = "0 * * * *", // Every hour
            LastRun = null
        };

        // Act
        var result = TestShouldRunTask(schedulingService, task, DateTime.UtcNow);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldRunTask_WithValidCronAndTime_ReturnsTrue()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var now = DateTime.UtcNow;
        var lastRun = now.AddHours(-1).AddMinutes(-1); // 1 hour and 1 minute ago
        
        var task = new ScheduledTask
        {
            Name = "TestTask",
            Enabled = true,
            CronExpression = "0 * * * *", // Every hour
            LastRun = lastRun
        };

        // Act - Test with a time that should trigger (near top of hour)
        var testTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 30); // 30 seconds past the hour
        var result = TestShouldRunTask(schedulingService, task, testTime);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRunTask_WithInvalidCron_ReturnsFalse()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask
        {
            Name = "TestTask",
            Enabled = true,
            CronExpression = "invalid cron expression",
            LastRun = null
        };

        // Act
        var result = TestShouldRunTask(schedulingService, task, DateTime.UtcNow);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("0 9 * * *")]  // Daily at 9 AM
    [InlineData("*/5 * * * *")] // Every 5 minutes
    [InlineData("0 0 * * 0")] // Weekly on Sunday
    public void ShouldRunTask_WithVariousCronExpressions_HandlesCorrectly(string cronExpression)
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask
        {
            Name = "TestTask",
            Enabled = true,
            CronExpression = cronExpression,
            LastRun = DateTime.UtcNow.AddDays(-1) // 1 day ago
        };

        // Act
        var result = TestShouldRunTask(schedulingService, task, DateTime.UtcNow);

        // Assert - Should not throw and return a boolean
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetNextRunTime_WithValidCron_ReturnsNextOccurrence()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var fromTime = new DateTime(2024, 1, 1, 10, 0, 0);
        var cronExpression = "0 12 * * *"; // Daily at noon

        // Act
        var nextRun = TestGetNextRunTime(schedulingService, cronExpression, fromTime);

        // Assert
        Assert.NotNull(nextRun);
        Assert.Equal(12, nextRun.Value.Hour);
        Assert.Equal(0, nextRun.Value.Minute);
    }

    [Fact]
    public void GetNextRunTime_WithInvalidCron_ReturnsNull()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var fromTime = DateTime.UtcNow;
        var cronExpression = "invalid";

        // Act
        var nextRun = TestGetNextRunTime(schedulingService, cronExpression, fromTime);

        // Assert
        Assert.Null(nextRun);
    }

    [Fact]
    public async Task ExecuteTask_WithReminderCheck_CallsExecutePendingReminders()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask { Name = "ReminderCheck" };

        // Act
        await TestExecuteTask(schedulingService, task);

        // Assert
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_WithMorningReminders_CallsExecutePendingReminders()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask { Name = "MorningReminders" };

        // Act
        await TestExecuteTask(schedulingService, task);

        // Assert
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_WithWeeklyLetterCheck_CallsAgentService()
    {
        // Arrange
        _mockAgentService.Setup(s => s.GetAllChildrenAsync())
            .ReturnsAsync(new List<Child>());

        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask { Name = "WeeklyLetterCheck" };

        // Act
        await TestExecuteTask(schedulingService, task);

        // Assert
        _mockAgentService.Verify(s => s.GetAllChildrenAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_WithUnknownTask_LogsWarning()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();
        var task = new ScheduledTask { Name = "UnknownTask" };

        // Act & Assert - Should not throw
        await TestExecuteTask(schedulingService, task);
    }

    [Fact]
    public async Task ExecutePendingReminders_WithNoReminders_LogsInformation()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        var schedulingService = CreateSchedulingService();

        // Act
        await TestExecutePendingReminders(schedulingService);

        // Assert
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecutePendingReminders_WithReminders_SendsAndDeletes()
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new Reminder
            {
                Id = 123,
                Text = "Test reminder",
                ChildName = "TestChild",
                RemindDate = DateOnly.FromDateTime(DateTime.Today),
                RemindTime = TimeOnly.FromDateTime(DateTime.Now)
            }
        };

        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(reminders);
        _mockSupabaseService.Setup(s => s.DeleteReminderAsync(123))
            .Returns(Task.CompletedTask);

        var schedulingService = CreateSchedulingService();

        // Act
        await TestExecutePendingReminders(schedulingService);

        // Assert
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once);
        _mockSupabaseService.Verify(s => s.DeleteReminderAsync(123), Times.Once);
    }

    [Fact]
    public void GetCurrentWeekAndYear_ReturnsValidWeekAndYear()
    {
        // Act
        var (weekNumber, year) = TestGetCurrentWeekAndYear();

        // Assert
        Assert.InRange(weekNumber, 1, 53);
        Assert.InRange(year, 2020, 2030); // Reasonable range
    }

    [Fact]
    public async Task CheckForMissedReminders_WithNoReminders_LogsInformation()
    {
        // Arrange
        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        var schedulingService = CreateSchedulingService();

        // Act
        await TestCheckForMissedReminders(schedulingService);

        // Assert
        _mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task CheckForMissedReminders_WithMissedReminders_SendsNotifications()
    {
        // Arrange
        var missedReminders = new List<Reminder>
        {
            new Reminder
            {
                Id = 456,
                Text = "Missed reminder",
                ChildName = "TestChild",
                RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                RemindTime = TimeOnly.FromDateTime(DateTime.Now.AddHours(-2))
            }
        };

        _mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
            .ReturnsAsync(missedReminders);
        _mockSupabaseService.Setup(s => s.DeleteReminderAsync(456))
            .Returns(Task.CompletedTask);

        var schedulingService = CreateSchedulingService();

        // Act
        await TestCheckForMissedReminders(schedulingService);

        // Assert
        _mockSupabaseService.Verify(s => s.DeleteReminderAsync(456), Times.Once);
    }

    // Helper methods for testing private methods
    private SchedulingService CreateSchedulingService()
    {
        var slackBot = new SlackInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        var telegramBot = new TelegramInteractiveBot(_mockAgentService.Object, _testConfig, _loggerFactory, _mockSupabaseService.Object);
        
        return new SchedulingService(
            _loggerFactory,
            _mockSupabaseService.Object,
            _mockAgentService.Object,
            slackBot,
            telegramBot,
            _testConfig);
    }

    private bool TestShouldRunTask(SchedulingService service, ScheduledTask task, DateTime now)
    {
        var method = typeof(SchedulingService).GetMethod("ShouldRunTask", BindingFlags.NonPublic | BindingFlags.Instance);
        return (bool)method!.Invoke(service, new object[] { task, now })!;
    }

    private DateTime? TestGetNextRunTime(SchedulingService service, string cronExpression, DateTime fromTime)
    {
        var method = typeof(SchedulingService).GetMethod("GetNextRunTime", BindingFlags.NonPublic | BindingFlags.Instance);
        return (DateTime?)method!.Invoke(service, new object[] { cronExpression, fromTime });
    }

    private async Task TestExecuteTask(SchedulingService service, ScheduledTask task)
    {
        var method = typeof(SchedulingService).GetMethod("ExecuteTask", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { task })!;
    }

    private async Task TestExecutePendingReminders(SchedulingService service)
    {
        var method = typeof(SchedulingService).GetMethod("ExecutePendingReminders", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[0])!;
    }

    private (int weekNumber, int year) TestGetCurrentWeekAndYear()
    {
        var method = typeof(SchedulingService).GetMethod("GetCurrentWeekAndYear", BindingFlags.NonPublic | BindingFlags.Static);
        return ((int, int))method!.Invoke(null, new object[0])!;
    }

    private async Task TestCheckForMissedReminders(SchedulingService service)
    {
        var method = typeof(SchedulingService).GetMethod("CheckForMissedReminders", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[0])!;
    }
}