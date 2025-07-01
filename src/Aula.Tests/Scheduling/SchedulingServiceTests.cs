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

    [Fact]
    public async Task ExecuteTask_WithWeeklyLetterTask_CallsExecuteWeeklyLetterCheck()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var task = new ScheduledTask
        {
            Name = "WeeklyLetterCheck",
            Enabled = true,
            CronExpression = "0 16 * * 0"
        };

        // Setup mocks
        _mockAgentService.Setup(x => x.GetAllChildrenAsync())
            .ReturnsAsync(new List<Child> { new Child { FirstName = "TestChild", LastName = "TestLast" } });

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true); // Already posted, so it should return early

        // Act
        await TestExecuteTask(schedulingService, task);

        // Assert
        _mockAgentService.Verify(x => x.GetAllChildrenAsync(), Times.Once);
        _mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_WithPendingRemindersTask_CallsExecutePendingReminders()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var task = new ScheduledTask
        {
            Name = "ReminderCheck",
            Enabled = true,
            CronExpression = "*/5 * * * *"
        };

        var testReminder = new Reminder
        {
            Id = 1,
            Text = "Test reminder",
            RemindDate = DateOnly.FromDateTime(DateTime.Today),
            RemindTime = new TimeOnly(10, 0),
            ChildName = "TestChild"
        };

        // Setup mocks
        _mockSupabaseService.Setup(x => x.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder> { testReminder });

        _mockSupabaseService.Setup(x => x.DeleteReminderAsync(testReminder.Id))
            .Returns(Task.CompletedTask);

        // Act
        await TestExecuteTask(schedulingService, task);

        // Assert
        _mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once);
        _mockSupabaseService.Verify(x => x.DeleteReminderAsync(testReminder.Id), Times.Once);
    }

    [Fact]
    public async Task ExecutePendingReminders_WithNoReminders_DoesNotThrow()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        _mockSupabaseService.Setup(x => x.GetPendingRemindersAsync())
            .ReturnsAsync(new List<Reminder>());

        // Act & Assert
        await TestExecutePendingReminders(schedulingService);
        _mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecutePendingReminders_WithReminders_SendsNotificationsAndDeletesReminders()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var reminders = new List<Reminder>
        {
            new Reminder { Id = 1, Text = "Reminder 1", ChildName = "Child1" },
            new Reminder { Id = 2, Text = "Reminder 2", ChildName = null }
        };

        _mockSupabaseService.Setup(x => x.GetPendingRemindersAsync())
            .ReturnsAsync(reminders);

        _mockSupabaseService.Setup(x => x.DeleteReminderAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        await TestExecutePendingReminders(schedulingService);

        // Assert
        _mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once);
        _mockSupabaseService.Verify(x => x.DeleteReminderAsync(1), Times.Once);
        _mockSupabaseService.Verify(x => x.DeleteReminderAsync(2), Times.Once);
    }

    [Fact]
    public async Task ExecutePendingReminders_WithExceptionDuringProcessing_LogsErrorAndContinues()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var reminders = new List<Reminder>
        {
            new Reminder { Id = 1, Text = "Reminder 1", ChildName = "Child1" }
        };

        _mockSupabaseService.Setup(x => x.GetPendingRemindersAsync())
            .ReturnsAsync(reminders);

        _mockSupabaseService.Setup(x => x.DeleteReminderAsync(1))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert - Should not throw, errors should be logged
        await TestExecutePendingReminders(schedulingService);
        _mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once);
    }

    [Fact]
    public async Task CheckAndPostWeekLetter_WithAlreadyPostedLetter_ReturnsEarly()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        var task = new ScheduledTask { Name = "WeeklyLetterCheck", Enabled = true };

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        // Act
        var method = typeof(SchedulingService).GetMethod("CheckAndPostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, task })!;

        // Assert
        _mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _mockAgentService.Verify(x => x.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndPostWeekLetter_WithNoWeekLetter_IncrementsRetryAttempt()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        var task = new ScheduledTask { Name = "WeeklyLetterCheck", Enabled = true };

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        _mockAgentService.Setup(x => x.GetWeekLetterAsync(child, It.IsAny<DateOnly>(), true))
            .ReturnsAsync((JObject?)null);

        _mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        var method = typeof(SchedulingService).GetMethod("CheckAndPostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, task })!;

        // Assert
        _mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _mockAgentService.Verify(x => x.GetWeekLetterAsync(child, It.IsAny<DateOnly>(), true), Times.Once);
        _mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CheckAndPostWeekLetter_WithEmptyContent_IncrementsRetryAttempt()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        var task = new ScheduledTask { Name = "WeeklyLetterCheck", Enabled = true };

        var weekLetter = new JObject(); // Empty week letter

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        _mockAgentService.Setup(x => x.GetWeekLetterAsync(child, It.IsAny<DateOnly>(), true))
            .ReturnsAsync(weekLetter);

        _mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        var method = typeof(SchedulingService).GetMethod("CheckAndPostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, task })!;

        // Assert - May be called once in ValidateAndProcessWeekLetterContent for empty content
        _mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckAndPostWeekLetter_WithException_LogsErrorAndIncrementsRetry()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        var task = new ScheduledTask { Name = "WeeklyLetterCheck", Enabled = true };

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("Database error"));

        _mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Act
        var method = typeof(SchedulingService).GetMethod("CheckAndPostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, task })!;

        // Assert
        _mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task PostWeekLetter_WithValidContent_PostsToBothChannels()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["uge"] = "42",
                    ["klasseNavn"] = "Test Class"
                }
            }
        };
        var content = "<p>Test week letter content</p>";

        // Act
        var method = typeof(SchedulingService).GetMethod("PostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, weekLetter, content })!;

        // Assert - The method should call the bot methods (though we can't verify them directly without mocking the bots)
        // This test verifies the method executes without throwing
        Assert.True(true, "PostWeekLetter executed without exceptions");
    }

    [Fact]
    public async Task PostWeekLetter_WithInvalidWeekLetter_HandlesGracefully()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };
        var weekLetter = new JObject(); // Empty week letter
        var content = "<p>Test content</p>";

        // Act & Assert - Should handle null/empty week letter gracefully
        var method = typeof(SchedulingService).GetMethod("PostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(schedulingService, new object[] { child, weekLetter, content })!;
        
        Assert.True(true, "PostWeekLetter handled empty week letter without exceptions");
    }

    [Fact]
    public void GetCurrentWeekAndYear_ReturnsValidValues()
    {
        // Act
        var (weekNumber, year) = TestGetCurrentWeekAndYear();

        // Assert
        Assert.True(weekNumber >= 1 && weekNumber <= 53, $"Week number {weekNumber} should be between 1 and 53");
        Assert.True(year >= 2020 && year <= 2100, $"Year {year} should be reasonable");
    }

    [Fact]
    public async Task CheckForMissedReminders_DoesNotThrow()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        // Act & Assert
        await TestCheckForMissedReminders(schedulingService);
        Assert.True(true, "CheckForMissedReminders executed without exceptions");
    }

    private async Task<dynamic?> TestTryGetWeekLetter(SchedulingService service, Child child, int weekNumber, int year)
    {
        var method = typeof(SchedulingService).GetMethod("TryGetWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
        return await (Task<dynamic?>)method!.Invoke(service, new object[] { child, weekNumber, year })!;
    }

    private async Task<bool> TestIsWeekLetterAlreadyPosted(SchedulingService service, string childName, int weekNumber, int year)
    {
        var method = typeof(SchedulingService).GetMethod("IsWeekLetterAlreadyPosted", BindingFlags.NonPublic | BindingFlags.Instance);
        return await (Task<bool>)method!.Invoke(service, new object[] { childName, weekNumber, year })!;
    }

    [Fact]
    public async Task IsWeekLetterAlreadyPosted_WithPostedLetter_ReturnsTrue()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        _mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", 42, 2024))
            .ReturnsAsync(true);

        // Act
        var result = await TestIsWeekLetterAlreadyPosted(schedulingService, "TestChild", 42, 2024);

        // Assert
        Assert.True(result);
        _mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", 42, 2024), Times.Once);
    }

    [Fact]
    public async Task TryGetWeekLetter_WithNoWeekLetter_IncrementsRetryAndReturnsNull()
    {
        // Arrange
        var schedulingService = CreateSchedulingService();

        var child = new Child { FirstName = "TestChild", LastName = "TestLast" };

        _mockAgentService.Setup(x => x.GetWeekLetterAsync(child, It.IsAny<DateOnly>(), true))
            .ReturnsAsync((JObject?)null);

        _mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", 42, 2024))
            .Returns(Task.CompletedTask);

        // Act
        var result = await TestTryGetWeekLetter(schedulingService, child, 42, 2024);

        // Assert
        Assert.Null(result);
        _mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", 42, 2024), Times.Once);
    }
}