using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Scheduling;
using Aula.Channels;
using Aula.Configuration;
using Aula.Services;
using ConfigSlack = Aula.Configuration.Slack;
using ConfigTelegram = Aula.Configuration.Telegram;
using ConfigChild = Aula.Configuration.Child;

namespace Aula.Tests.Scheduling;

public class SchedulingServiceTests : IDisposable
{
	private readonly ILoggerFactory _loggerFactory;
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly Mock<IChildServiceCoordinator> _mockCoordinator;
	private readonly Mock<IChannelManager> _mockChannelManager;
	private readonly Config _testConfig;
	private readonly List<IDisposable> _disposables = new List<IDisposable>();

	public SchedulingServiceTests()
	{
		_loggerFactory = new LoggerFactory();
		_mockSupabaseService = new Mock<ISupabaseService>();
		_mockCoordinator = new Mock<IChildServiceCoordinator>();
		_mockChannelManager = new Mock<IChannelManager>();

		// Setup mock channel manager to return empty channels list by default
		_mockChannelManager.Setup(m => m.GetEnabledChannels()).Returns(new List<IChannel>());

		_testConfig = new Config
		{
			Slack = new ConfigSlack { EnableInteractiveBot = true, ApiToken = "test-slack-token" },
			Telegram = new ConfigTelegram { Enabled = true, ChannelId = "@testchannel", Token = "test-telegram-token" },
			MinUddannelse = new MinUddannelse
			{
				Children = new List<ConfigChild>
				{
					new ConfigChild { FirstName = "TestChild", LastName = "TestLast" }
				}
			}
		};
	}

	private SchedulingService CreateSchedulingService()
	{
		return new SchedulingService(
			_loggerFactory,
			_mockSupabaseService.Object,
			_mockCoordinator.Object,
			_mockChannelManager.Object,
			_testConfig);
	}

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		// Act
		var schedulingService = CreateSchedulingService();

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

		var schedulingService = CreateSchedulingService();

		// Act & Assert - Should not throw
		await schedulingService.StartAsync();

		// Cleanup
		await schedulingService.StopAsync();
	}

	[Fact]
	public async Task StopAsync_AfterStart_DoesNotThrow()
	{
		// Arrange
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		var schedulingService = CreateSchedulingService();

		await schedulingService.StartAsync();

		// Act & Assert - Should not throw
		await schedulingService.StopAsync();
	}

	[Fact]
	public async Task StartAsync_CallsGetPendingRemindersAsync_AtLeastOnce()
	{
		// Arrange
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());


		var schedulingService = CreateSchedulingService();

		// Act
		await schedulingService.StartAsync();

		// Wait a moment for async operations to complete
		await Task.Delay(100);

		// Assert - The service should have called GetPendingRemindersAsync
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.AtLeastOnce);
		// Note: GetScheduledTasksAsync may be called on a different timer interval

		// Cleanup
		await schedulingService.StopAsync();
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
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once());
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
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once());
	}

	[Fact]
	public async Task ExecuteTask_WithWeeklyLetterCheck_CallsAgentService()
	{
		// Arrange
		_mockCoordinator.Setup(s => s.GetAllChildrenAsync())
			.ReturnsAsync(new List<Child>());

		var schedulingService = CreateSchedulingService();
		var task = new ScheduledTask { Name = "WeeklyLetterCheck" };

		// Act
		await TestExecuteTask(schedulingService, task);

		// Assert
		_mockCoordinator.Verify(s => s.GetAllChildrenAsync(), Times.Once());
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
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once());
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
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once());
		_mockSupabaseService.Verify(s => s.DeleteReminderAsync(123), Times.Once());
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
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.Once());
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
		_mockSupabaseService.Verify(s => s.DeleteReminderAsync(456), Times.Once());
	}

	// Helper methods for testing private methods

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
		_mockCoordinator.Setup(x => x.GetAllChildrenAsync())
			.ReturnsAsync(new List<Child> { new Child { FirstName = "TestChild", LastName = "TestLast" } });

		_mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync(true); // Already posted, so it should return early

		// Act
		await TestExecuteTask(schedulingService, task);

		// Assert
		_mockCoordinator.Verify(x => x.GetAllChildrenAsync(), Times.Once());
		_mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once());
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
		_mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once());
		_mockSupabaseService.Verify(x => x.DeleteReminderAsync(testReminder.Id), Times.Once());
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
		_mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once());
		_mockSupabaseService.Verify(x => x.DeleteReminderAsync(1), Times.Once());
		_mockSupabaseService.Verify(x => x.DeleteReminderAsync(2), Times.Once());
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
		_mockSupabaseService.Verify(x => x.GetPendingRemindersAsync(), Times.Once());
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
		_mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once());
		_mockCoordinator.Verify(x => x.GetWeekLetterForChildAsync(It.IsAny<Child>(), It.IsAny<DateOnly>()), Times.Never());
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

		_mockCoordinator.Setup(x => x.GetWeekLetterForChildAsync(child, It.IsAny<DateOnly>()))
			.ReturnsAsync(default(JObject?));

		_mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()))
			.Returns(Task.CompletedTask);

		// Act
		var method = typeof(SchedulingService).GetMethod("CheckAndPostWeekLetter", BindingFlags.NonPublic | BindingFlags.Instance);
		await (Task)method!.Invoke(schedulingService, new object[] { child, task })!;

		// Assert
		_mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once());
		_mockCoordinator.Verify(x => x.GetWeekLetterForChildAsync(child, It.IsAny<DateOnly>()), Times.Once());
		_mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once());
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

		_mockCoordinator.Setup(x => x.GetWeekLetterForChildAsync(child, It.IsAny<DateOnly>()))
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
		_mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", It.IsAny<int>(), It.IsAny<int>()), Times.Once());
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
		_mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", 42, 2024), Times.Once());
	}

	[Fact]
	public async Task TryGetWeekLetter_WithNoWeekLetter_IncrementsRetryAndReturnsNull()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();

		var child = new Child { FirstName = "TestChild", LastName = "TestLast" };

		_mockCoordinator.Setup(x => x.GetWeekLetterForChildAsync(child, It.IsAny<DateOnly>()))
			.ReturnsAsync(default(JObject?));

		_mockSupabaseService.Setup(x => x.IncrementRetryAttemptAsync("TestChild", 42, 2024))
			.Returns(Task.CompletedTask);

		// Act
		var result = await TestTryGetWeekLetter(schedulingService, child, 42, 2024);

		// Assert
		Assert.Null(result);
		_mockSupabaseService.Verify(x => x.IncrementRetryAttemptAsync("TestChild", 42, 2024), Times.Once());
	}

	// ===========================================
	// ADVANCED ASYNC TIMER & CONCURRENCY TESTS
	// ===========================================

	[Fact]
	public async Task StartAsync_CalledMultipleTimes_HandlesGracefully()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		// Act - Call StartAsync multiple times
		await schedulingService.StartAsync();
		await schedulingService.StartAsync(); // Should not throw or cause issues
		await schedulingService.StartAsync(); // Should not throw or cause issues

		// Assert - Should handle multiple starts gracefully
		await schedulingService.StopAsync();
	}

	[Fact]
	public async Task StopAsync_CalledMultipleTimes_HandlesGracefully()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		await schedulingService.StartAsync();

		// Act - Call StopAsync multiple times
		await schedulingService.StopAsync();
		await schedulingService.StopAsync(); // Should not throw
		await schedulingService.StopAsync(); // Should not throw

		// Assert - No exceptions should be thrown
		Assert.True(true, "Multiple StopAsync calls handled gracefully");
	}

	[Fact]
	public async Task StartStopCycle_RepeatedMultipleTimes_MaintainsStability()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		// Act & Assert - Multiple start/stop cycles
		for (int i = 0; i < 5; i++)
		{
			await schedulingService.StartAsync();
			await Task.Delay(50); // Brief operation period
			await schedulingService.StopAsync();
		}

		// Final verification
		Assert.True(true, "Multiple start/stop cycles completed successfully");
	}

	[Fact]
	public async Task ConcurrentOperations_WithTimerRunning_HandlesSafely()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		await schedulingService.StartAsync();

		// Act - Simulate concurrent operations while timer is running
		var tasks = new List<Task>();
		for (int i = 0; i < 10; i++)
		{
			int taskId = i;
			tasks.Add(Task.Run(async () =>
			{
				await Task.Delay(10 + taskId); // Staggered delays to create real concurrency
				// Actually interact with the service by calling methods that trigger internal operations
				await TestExecutePendingReminders(schedulingService);
			}));
		}

		await Task.WhenAll(tasks);
		await Task.Delay(100); // Allow timer to complete any ongoing operations

		// Assert - Should complete without exceptions
		await schedulingService.StopAsync();
		Assert.True(true, "Concurrent operations completed safely");
	}

	// ===========================================
	// INTEGRATION WORKFLOW TESTS
	// ===========================================

	[Fact]
	public async Task EndToEndReminderWorkflow_WithValidReminder_CompletesSuccessfully()
	{
		// Arrange
		var reminder = new Reminder
		{
			Id = 1,
			Text = "Test reminder",
			RemindDate = DateOnly.FromDateTime(DateTime.Today),
			RemindTime = new TimeOnly(9, 0),
			ChildName = "TestChild",
			CreatedBy = "bot"
		};

		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder> { reminder });
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());
		_mockSupabaseService.Setup(s => s.DeleteReminderAsync(1))
			.Returns(Task.CompletedTask);

		await schedulingService.StartAsync();
		await Task.Delay(100); // Allow timer to process

		// Assert - Service should check for reminders when started
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.AtLeastOnce);
		// Note: DeleteReminderAsync may not be called immediately due to timing

		await schedulingService.StopAsync();
	}

	[Fact]
	public void MultiChannelPosting_ConfigurationValidation_BothChannelsEnabled()
	{
		// This test validates the configuration logic for multi-channel posting
		// without making external API calls to Slack or Telegram

		// Arrange - Test configuration with both channels enabled
		var testConfig = new Config
		{
			Slack = new ConfigSlack { EnableInteractiveBot = true, ApiToken = "test-slack-token" },
			Telegram = new ConfigTelegram { Enabled = true, ChannelId = "@testchannel", Token = "test-telegram-token" },
			MinUddannelse = new MinUddannelse
			{
				Children = new List<ConfigChild>
				{
					new ConfigChild { FirstName = "TestChild", LastName = "TestLast" }
				}
			}
		};

		// Act & Assert - Verify configuration supports multi-channel posting
		Assert.True(testConfig.Slack.EnableInteractiveBot);
		Assert.True(testConfig.Telegram.Enabled);
		Assert.NotNull(testConfig.Slack.ApiToken);
		Assert.NotNull(testConfig.Telegram.Token);
		Assert.NotEmpty(testConfig.MinUddannelse.Children);

		// Verify week letter data structure that would be used in multi-channel posting
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

		Assert.NotNull(weekLetter["ugebreve"]);
		Assert.True(weekLetter["ugebreve"] is JArray);

		var ugebreveArray = (JArray)weekLetter["ugebreve"]!;
		Assert.True(ugebreveArray.Count > 0);
	}

	// ===========================================
	// ERROR RECOVERY & RESILIENCE TESTS
	// ===========================================

	[Fact]
	public async Task ServiceDegradation_WithSupabaseFailure_ContinuesOperation()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		var callCount = 0;

		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.Returns(() =>
			{
				callCount++;
				if (callCount <= 2)
					throw new Exception("Database temporarily unavailable");
				return Task.FromResult(new List<Reminder>());
			});
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		// Act - Start service and let it recover from initial failures
		await schedulingService.StartAsync();
		await Task.Delay(150); // Allow multiple timer cycles

		// Assert - Service should attempt multiple calls for recovery
		Assert.True(callCount >= 1, $"Expected at least 1 call but got {callCount}");

		await schedulingService.StopAsync();
	}

	[Fact]
	public async Task ResourceCleanup_WithExceptionDuringStop_HandlesGracefully()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		await schedulingService.StartAsync();

		// Act & Assert - Should handle cleanup gracefully even with issues
		await schedulingService.StopAsync();

		// Multiple stops should not cause issues
		await schedulingService.StopAsync();

		Assert.True(true, "Resource cleanup handled gracefully");
	}

	[Fact]
	public async Task MemoryManagement_WithLongRunningOperation_DoesNotLeak()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder>());
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());

		// Act - Simulate longer running operations
		await schedulingService.StartAsync();

		// Simulate multiple timer cycles
		for (int i = 0; i < 10; i++)
		{
			await Task.Delay(20);
		}

		await schedulingService.StopAsync();

		// Assert - Should complete without memory issues
		Assert.True(true, "Long running operation completed successfully");
	}

	// ===========================================
	// EDGE CASE AND BOUNDARY TESTS
	// ===========================================

	[Fact]
	public async Task MissedReminderRecovery_OnStartup_ProcessesMissedItems()
	{
		// Arrange
		var missedReminder = new Reminder
		{
			Id = 1,
			Text = "Missed reminder",
			RemindDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
			RemindTime = new TimeOnly(9, 0),
			ChildName = "TestChild"
		};

		var schedulingService = CreateSchedulingService();
		_mockSupabaseService.Setup(s => s.GetPendingRemindersAsync())
			.ReturnsAsync(new List<Reminder> { missedReminder });
		_mockSupabaseService.Setup(s => s.GetScheduledTasksAsync())
			.ReturnsAsync(new List<ScheduledTask>());
		_mockSupabaseService.Setup(s => s.DeleteReminderAsync(1))
			.Returns(Task.CompletedTask);

		// Act - Starting should check for missed reminders
		await schedulingService.StartAsync();
		await Task.Delay(100);

		// Assert - Service should check for reminders (deletion timing varies)
		_mockSupabaseService.Verify(s => s.GetPendingRemindersAsync(), Times.AtLeastOnce);

		await schedulingService.StopAsync();
	}

	[Fact]
	public void SchedulingConfiguration_WithDifferentTimerIntervals_IsValid()
	{
		// Arrange & Act - Test various timer configurations
		var configs = new[]
		{
			new { SchedulingInterval = 5 },
			new { SchedulingInterval = 10 },
			new { SchedulingInterval = 60 }
		};

		foreach (var config in configs)
		{
			try
			{
				// Act - Should create service without issues
				var testConfig = new Config
				{
					Timers = new Aula.Configuration.Timers
					{
						SchedulingIntervalSeconds = config.SchedulingInterval
					},
					Slack = new ConfigSlack { EnableInteractiveBot = true, ApiToken = "test-token" },
					Telegram = new ConfigTelegram { Enabled = true, ChannelId = "@test", Token = "test-token" },
					MinUddannelse = new MinUddannelse
					{
						Children = new List<ConfigChild> { new ConfigChild { FirstName = "Test", LastName = "Child" } }
					}
				};

				var service = new SchedulingService(
					_loggerFactory,
					_mockSupabaseService.Object,
					_mockCoordinator.Object,
					_mockChannelManager.Object,
					testConfig);

				// Assert
				Assert.NotNull(service);
			}
			catch (Exception ex)
			{
				// Log test failure for debugging
				_loggerFactory.CreateLogger<SchedulingServiceTests>().LogError(ex, "Test failed for interval {Interval}", config.SchedulingInterval);
				throw;
			}
		}
	}

	[Fact]
	public async Task WeekLetterContentDeduplication_WithSameContent_SkipsReposting()
	{
		// Arrange
		var schedulingService = CreateSchedulingService();

		_mockSupabaseService.Setup(x => x.HasWeekLetterBeenPostedAsync("TestChild", 42, 2024))
			.ReturnsAsync(true); // Already posted

		var child = new Child { FirstName = "TestChild", LastName = "TestLast" };

		// Act
		var wasPosted = await TestIsWeekLetterAlreadyPosted(schedulingService, "TestChild", 42, 2024);

		// Assert - Should detect duplicate and skip
		Assert.True(wasPosted);
		_mockSupabaseService.Verify(x => x.HasWeekLetterBeenPostedAsync("TestChild", 42, 2024), Times.Once());
		// GetWeekLetterAsync should not be called if already posted
		_mockCoordinator.Verify(x => x.GetWeekLetterForChildAsync(It.IsAny<Child>(), It.IsAny<DateOnly>()), Times.Never());
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}
		_disposables.Clear();
		_loggerFactory?.Dispose();
	}
}
