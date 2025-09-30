using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Services;

public class ChildServiceCoordinatorTests
{
	private readonly Mock<IChildOperationExecutor> _mockExecutor;
	private readonly Mock<IAgentService> _mockAgentService;
	private readonly Config _config;
	private readonly Mock<ILogger<ChildServiceCoordinator>> _mockLogger;
	private readonly ChildServiceCoordinator _coordinator;
	private readonly List<Child> _testChildren;

	public ChildServiceCoordinatorTests()
	{
		_mockExecutor = new Mock<IChildOperationExecutor>();
		_mockAgentService = new Mock<IAgentService>();
		_config = new Config();
		_mockLogger = new Mock<ILogger<ChildServiceCoordinator>>();

		_testChildren = new List<Child>
		{
			new Child { FirstName = "Child1", LastName = "Test" },
			new Child { FirstName = "Child2", LastName = "Test" }
		};

		_mockAgentService.Setup(a => a.GetAllChildrenAsync())
			.ReturnsAsync(_testChildren);

		_coordinator = new ChildServiceCoordinator(
			_mockExecutor.Object,
			_mockAgentService.Object,
			_config,
			_mockLogger.Object);
	}

	[Fact]
	public async Task PreloadWeekLettersForAllChildrenAsync_ExecutesForEachChild()
	{
		// Arrange
		var results = new Dictionary<Child, object>();
		foreach (var child in _testChildren)
		{
			results[child] = new { CurrentWeek = "week1", LastWeek = "week2", TwoWeeksAgo = "week3" };
		}

		_mockExecutor.Setup(e => e.ExecuteForAllChildrenAsync(
			It.IsAny<IEnumerable<Child>>(),
			It.IsAny<Func<IServiceProvider, Task<object>>>(),
			"PreloadWeekLetters"))
			.ReturnsAsync(results);

		// Act
		await _coordinator.PreloadWeekLettersForAllChildrenAsync();

		// Assert
		_mockExecutor.Verify(e => e.ExecuteForAllChildrenAsync(
			It.Is<IEnumerable<Child>>(c => c.Count() == 2),
			It.IsAny<Func<IServiceProvider, Task<object>>>(),
			"PreloadWeekLetters"), Times.Once);
	}

	[Fact]
	public async Task PostWeekLettersToChannelsAsync_ExecutesForEachChild()
	{
		// Arrange
		var results = new Dictionary<Child, bool>();
		foreach (var child in _testChildren)
		{
			results[child] = true;
		}

		_mockExecutor.Setup(e => e.ExecuteForAllChildrenAsync(
			It.IsAny<IEnumerable<Child>>(),
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"PostWeekLetters"))
			.ReturnsAsync(results);

		// Act
		await _coordinator.PostWeekLettersToChannelsAsync();

		// Assert
		_mockExecutor.Verify(e => e.ExecuteForAllChildrenAsync(
			It.Is<IEnumerable<Child>>(c => c.Count() == 2),
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"PostWeekLetters"), Times.Once);
	}

	[Fact]
	public async Task FetchWeekLetterForChildAsync_ExecutesInChildContext()
	{
		// Arrange
		var testChild = _testChildren.First();
		var testDate = DateOnly.FromDateTime(DateTime.Today);

		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"FetchWeekLetter"))
			.ReturnsAsync(true);

		// Act
		var result = await _coordinator.FetchWeekLetterForChildAsync(testChild, testDate);

		// Assert
		Assert.True(result);
		_mockExecutor.Verify(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"FetchWeekLetter"), Times.Once);
	}

	[Fact]
	public async Task ProcessScheduledTasksForChildAsync_ExecutesInChildContext()
	{
		// Arrange
		var testChild = _testChildren.First();

		// Act
		await _coordinator.ProcessScheduledTasksForChildAsync(testChild);

		// Assert
		_mockExecutor.Verify(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task>>(),
			"ProcessScheduledTasks"), Times.Once);
	}

	[Fact]
	public async Task SendReminderToChildAsync_ReturnsResult()
	{
		// Arrange
		var testChild = _testChildren.First();
		var reminderMessage = "Test reminder";

		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"SendReminder"))
			.ReturnsAsync(true);

		// Act
		var result = await _coordinator.SendReminderToChildAsync(testChild, reminderMessage);

		// Assert
		Assert.True(result);
		_mockExecutor.Verify(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"SendReminder"), Times.Once);
	}

	[Fact]
	public async Task ProcessAiQueryForChildAsync_ReturnsResponse()
	{
		// Arrange
		var testChild = _testChildren.First();
		var query = "Test query";
		var expectedResponse = "AI response";

		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<string>>>(),
			"ProcessAiQuery"))
			.ReturnsAsync(expectedResponse);

		// Act
		var result = await _coordinator.ProcessAiQueryForChildAsync(testChild, query);

		// Assert
		Assert.Equal(expectedResponse, result);
		_mockExecutor.Verify(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<string>>>(),
			"ProcessAiQuery"), Times.Once);
	}

	[Fact]
	public async Task SeedHistoricalDataForChildAsync_ExecutesCorrectly()
	{
		// Arrange
		var testChild = _testChildren.First();
		var weeksBack = 8;

		// Act
		await _coordinator.SeedHistoricalDataForChildAsync(testChild, weeksBack);

		// Assert
		_mockExecutor.Verify(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task>>(),
			"SeedHistoricalData"), Times.Once);
	}

	[Fact]
	public async Task GetNextScheduledTaskTimeForChildAsync_ReturnsDateTime()
	{
		// Arrange
		var testChild = _testChildren.First();
		var expectedTime = DateTime.UtcNow.AddHours(1);

		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			testChild,
			It.IsAny<Func<IServiceProvider, Task<DateTime?>>>(),
			"GetNextScheduledTaskTime"))
			.ReturnsAsync(expectedTime);

		// Act
		var result = await _coordinator.GetNextScheduledTaskTimeForChildAsync(testChild);

		// Assert
		Assert.Equal(expectedTime, result);
	}

	[Fact]
	public async Task ValidateChildServicesAsync_ReturnsTrueWhenValid()
	{
		// Arrange
		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			It.IsAny<Child>(),
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"ValidateServices"))
			.ReturnsAsync(true);

		// Act
		var result = await _coordinator.ValidateChildServicesAsync();

		// Assert
		Assert.True(result);
	}

	[Fact]
	public async Task ValidateChildServicesAsync_ReturnsFalseWhenNoChildren()
	{
		// Arrange
		_mockAgentService.Setup(a => a.GetAllChildrenAsync())
			.ReturnsAsync(new List<Child>());

		// Act
		var result = await _coordinator.ValidateChildServicesAsync();

		// Assert
		Assert.False(result);
	}

	[Fact]
	public async Task GetChildServicesHealthAsync_ReturnsHealthStatus()
	{
		// Arrange
		var healthStatus = new Dictionary<string, bool>
		{
			{ "ChildContext", true },
			{ "ChildDataService", true },
			{ "ChildChannelManager", false }
		};

		_mockExecutor.Setup(e => e.ExecuteInChildContextAsync(
			It.IsAny<Child>(),
			It.IsAny<Func<IServiceProvider, Task<Dictionary<string, bool>>>>(),
			"HealthCheck"))
			.ReturnsAsync(healthStatus);

		// Act
		var result = await _coordinator.GetChildServicesHealthAsync();

		// Assert
		Assert.NotEmpty(result);
		Assert.Contains("AgentService", result.Keys);
		Assert.True(result["AgentService"]); // Has children
	}

	[Fact]
	public async Task ProcessScheduledTasksForAllChildrenAsync_ExecutesForAllChildren()
	{
		// Arrange
		var results = new Dictionary<Child, bool>();
		foreach (var child in _testChildren)
		{
			results[child] = true;
		}

		_mockExecutor.Setup(e => e.ExecuteForAllChildrenAsync(
			It.IsAny<IEnumerable<Child>>(),
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"ProcessScheduledTasksForAll"))
			.ReturnsAsync(results);

		// Act
		await _coordinator.ProcessScheduledTasksForAllChildrenAsync();

		// Assert
		_mockExecutor.Verify(e => e.ExecuteForAllChildrenAsync(
			It.Is<IEnumerable<Child>>(c => c.Count() == 2),
			It.IsAny<Func<IServiceProvider, Task<bool>>>(),
			"ProcessScheduledTasksForAll"), Times.Once);
	}

	[Fact]
	public async Task FetchWeekLettersForAllChildrenAsync_ExecutesForAllChildren()
	{
		// Arrange
		var testDate = DateOnly.FromDateTime(DateTime.Today);
		var results = new Dictionary<Child, (Child child, JObject? weekLetter)>();
		foreach (var child in _testChildren)
		{
			results[child] = (child, new JObject());
		}

		_mockExecutor.Setup(e => e.ExecuteForAllChildrenAsync(
			It.IsAny<IEnumerable<Child>>(),
			It.IsAny<Func<IServiceProvider, Task<(Child child, JObject? weekLetter)>>>(),
			"FetchWeekLettersForAll"))
			.ReturnsAsync(results);

		// Act
		await _coordinator.FetchWeekLettersForAllChildrenAsync(testDate);

		// Assert
		_mockExecutor.Verify(e => e.ExecuteForAllChildrenAsync(
			It.Is<IEnumerable<Child>>(c => c.Count() == 2),
			It.IsAny<Func<IServiceProvider, Task<(Child child, JObject? weekLetter)>>>(),
			"FetchWeekLettersForAll"), Times.Once);
	}
}
