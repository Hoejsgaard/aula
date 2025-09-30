using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Scheduling;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Scheduling;

public class SecureChildSchedulerTests
{
	private readonly Mock<IChildContext> _mockContext;
	private readonly Mock<IChildContextValidator> _mockContextValidator;
	private readonly Mock<IChildAuditService> _mockAuditService;
	private readonly Mock<IChildSchedulingRateLimiter> _mockRateLimiter;
	private readonly Mock<ILogger<SecureChildScheduler>> _mockLogger;
	private readonly SecureChildScheduler _scheduler;
	private readonly Child _testChild;

	public SecureChildSchedulerTests()
	{
		_mockContext = new Mock<IChildContext>();
		_mockContextValidator = new Mock<IChildContextValidator>();
		_mockAuditService = new Mock<IChildAuditService>();
		_mockRateLimiter = new Mock<IChildSchedulingRateLimiter>();
		_mockLogger = new Mock<ILogger<SecureChildScheduler>>();

		_testChild = new Child { FirstName = "Test", LastName = "Child" };
		_mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
		_mockContext.Setup(c => c.ContextId).Returns(Guid.NewGuid());

		_scheduler = new SecureChildScheduler(
			_mockContext.Object,
			_mockContextValidator.Object,
			_mockAuditService.Object,
			_mockRateLimiter.Object,
			_mockLogger.Object);
	}

	[Fact]
	public async Task ScheduleTaskAsync_WithValidPermissions_CreatesTask()
	{
		// Arrange
		var taskName = "TestTask";
		var cronExpression = "0 0 * * *"; // Daily at midnight
		var description = "Test task description";

		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.GetScheduledTaskCountAsync(_testChild))
			.ReturnsAsync(0);

		// Act
		var taskId = await _scheduler.ScheduleTaskAsync(taskName, cronExpression, description);

		// Assert
		Assert.True(taskId > 0);
		_mockContext.Verify(c => c.ValidateContext(), Times.Once);
		_mockRateLimiter.Verify(r => r.RecordTaskScheduledAsync(_testChild), Times.Once);
		_mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "ScheduleTask", taskName, true), Times.Once);
	}

	[Fact]
	public async Task ScheduleTaskAsync_WithoutPermission_ThrowsUnauthorizedException()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(false);

		// Act & Assert
		await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
			_scheduler.ScheduleTaskAsync("Task", "0 0 * * *"));

		_mockAuditService.Verify(a => a.LogSecurityEventAsync(
			_testChild, "PermissionDenied", "schedule:create", SecuritySeverity.Warning), Times.Once);
	}

	[Fact]
	public async Task ScheduleTaskAsync_WithInvalidCron_ThrowsArgumentException()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(() =>
			_scheduler.ScheduleTaskAsync("Task", "invalid cron"));
	}

	[Fact]
	public async Task ScheduleTaskAsync_ExceedsTaskLimit_ThrowsInvalidOperationException()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:read"))
			.ReturnsAsync(true);

		// Schedule max tasks
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild)).ReturnsAsync(true);

		for (int i = 0; i < 10; i++)
		{
			await _scheduler.ScheduleTaskAsync($"Task{i}", "0 0 * * *");
		}

		// Act & Assert - Try to schedule one more
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_scheduler.ScheduleTaskAsync("ExtraTask", "0 0 * * *"));
	}

	[Fact]
	public async Task GetScheduledTasksAsync_ReturnsOnlyChildTasks()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:read"))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild)).ReturnsAsync(true);

		// Schedule tasks for test child
		await _scheduler.ScheduleTaskAsync("Task1", "0 0 * * *");
		await _scheduler.ScheduleTaskAsync("Task2", "0 12 * * *");

		// Act
		var tasks = await _scheduler.GetScheduledTasksAsync();

		// Assert
		Assert.Equal(2, tasks.Count);
		Assert.All(tasks, t =>
		{
			Assert.Equal(_testChild.FirstName, t.ChildFirstName);
			Assert.Equal(_testChild.LastName, t.ChildLastName);
		});
	}

	[Fact]
	public async Task CancelTaskAsync_WithOwnedTask_ReturnsTrue()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:delete"))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild)).ReturnsAsync(true);

		var taskId = await _scheduler.ScheduleTaskAsync("Task", "0 0 * * *");

		// Act
		var result = await _scheduler.CancelTaskAsync(taskId);

		// Assert
		Assert.True(result);
		_mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "CancelTask", $"TaskId:{taskId}", true), Times.Once);
	}

	[Fact]
	public async Task CancelTaskAsync_WithNonExistentTask_ReturnsFalse()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:delete"))
			.ReturnsAsync(true);

		// Act
		var result = await _scheduler.CancelTaskAsync(99999);

		// Assert
		Assert.False(result);
		_mockAuditService.Verify(a => a.LogSecurityEventAsync(
			_testChild, "TaskNotFound", "TaskId:99999", SecuritySeverity.Information), Times.Once);
	}

	[Fact]
	public async Task ExecuteTaskAsync_WithRateLimit_ThrowsInvalidOperationException()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:execute"))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanExecuteTaskAsync(_testChild, "Task"))
			.ReturnsAsync(false);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_scheduler.ExecuteTaskAsync("Task"));

		_mockAuditService.Verify(a => a.LogSecurityEventAsync(
			_testChild, "ExecutionRateLimitExceeded", "Task", SecuritySeverity.Warning), Times.Once);
	}

	[Fact]
	public async Task SetTaskEnabledAsync_UpdatesTaskStatus()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:create"))
			.ReturnsAsync(true);
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "schedule:update"))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild)).ReturnsAsync(true);

		var taskId = await _scheduler.ScheduleTaskAsync("Task", "0 0 * * *");

		// Act
		var result = await _scheduler.SetTaskEnabledAsync(taskId, false);

		// Assert
		Assert.True(result);
		_mockAuditService.Verify(a => a.LogDataAccessAsync(
			_testChild, "SetTaskEnabled", $"TaskId:{taskId},Enabled:False", true), Times.Once);
	}

	[Fact]
	public async Task ShouldRunTaskAsync_WithDueTask_ReturnsTrue()
	{
		// Arrange
		var task = new ChildScheduledTask
		{
			TaskName = "Test",
			Enabled = true,
			CronExpression = "* * * * *", // Every minute
			LastRun = DateTime.UtcNow.AddMinutes(-2),
			NextRun = DateTime.UtcNow.AddSeconds(-10) // Past due
		};

		// Act
		var shouldRun = await _scheduler.ShouldRunTaskAsync(task);

		// Assert
		Assert.True(shouldRun);
	}

	[Fact]
	public async Task ShouldRunTaskAsync_WithDisabledTask_ReturnsFalse()
	{
		// Arrange
		var task = new ChildScheduledTask
		{
			TaskName = "Test",
			Enabled = false,
			CronExpression = "* * * * *"
		};

		// Act
		var shouldRun = await _scheduler.ShouldRunTaskAsync(task);

		// Assert
		Assert.False(shouldRun);
	}

	[Fact]
	public async Task ProcessDueTasksAsync_ExecutesDueTasks()
	{
		// Arrange
		_mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, It.IsAny<string>()))
			.ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanScheduleTaskAsync(_testChild)).ReturnsAsync(true);
		_mockRateLimiter.Setup(r => r.CanExecuteTaskAsync(_testChild, It.IsAny<string>())).ReturnsAsync(true);

		// Schedule a task that's always due
		await _scheduler.ScheduleTaskAsync("AlwaysRun", "* * * * *");

		// Act
		await _scheduler.ProcessDueTasksAsync();

		// Assert
		_mockRateLimiter.Verify(r => r.RecordTaskExecutedAsync(_testChild, "AlwaysRun"), Times.Once);
	}

	[Fact]
	public Task Constructor_WithNullDependencies_ThrowsArgumentNullException()
	{
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => new SecureChildScheduler(
			null!, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object, _mockLogger.Object));

		Assert.Throws<ArgumentNullException>(() => new SecureChildScheduler(
			_mockContext.Object, null!, _mockAuditService.Object, _mockRateLimiter.Object, _mockLogger.Object));

		Assert.Throws<ArgumentNullException>(() => new SecureChildScheduler(
			_mockContext.Object, _mockContextValidator.Object, null!, _mockRateLimiter.Object, _mockLogger.Object));

		Assert.Throws<ArgumentNullException>(() => new SecureChildScheduler(
			_mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, null!, _mockLogger.Object));

		Assert.Throws<ArgumentNullException>(() => new SecureChildScheduler(
			_mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object, null!));

		return Task.CompletedTask;
	}
}
