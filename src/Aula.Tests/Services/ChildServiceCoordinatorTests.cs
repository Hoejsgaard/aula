using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Services;

public class ChildServiceCoordinatorTests
{
	private readonly Mock<IChildDataService> _mockDataService;
	private readonly Mock<IAgentService> _mockAgentService;
	private readonly Config _config;
	private readonly Mock<ILogger<ChildServiceCoordinator>> _mockLogger;
	private readonly Mock<IServiceProvider> _mockServiceProvider;
	private readonly ChildServiceCoordinator _coordinator;
	private readonly List<Child> _testChildren;

	public ChildServiceCoordinatorTests()
	{
		_mockDataService = new Mock<IChildDataService>();
		_mockAgentService = new Mock<IAgentService>();
		_config = new Config();
		_mockLogger = new Mock<ILogger<ChildServiceCoordinator>>();
		_mockServiceProvider = new Mock<IServiceProvider>();

		_testChildren = new List<Child>
		{
			new Child { FirstName = "Child1", LastName = "Test" },
			new Child { FirstName = "Child2", LastName = "Test" }
		};

		_mockAgentService.Setup(a => a.GetAllChildrenAsync())
			.ReturnsAsync(_testChildren);

		_coordinator = new ChildServiceCoordinator(
			_mockDataService.Object,
			_mockAgentService.Object,
			_config,
			_mockLogger.Object,
			_mockServiceProvider.Object);
	}

	[Fact]
	public async Task PreloadWeekLettersForAllChildrenAsync_CallsDataServiceForEachChild()
	{
		// Arrange
		var today = DateOnly.FromDateTime(DateTime.Today);
		var weekLetter = new JObject();

		_mockDataService.Setup(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			It.IsAny<DateOnly>(),
			true))
			.ReturnsAsync(weekLetter);

		// Act
		await _coordinator.PreloadWeekLettersForAllChildrenAsync();

		// Assert
		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			today,
			true), Times.Exactly(2)); // Current week for each child

		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			today.AddDays(-7),
			true), Times.Exactly(2)); // Last week for each child

		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			today.AddDays(-14),
			true), Times.Exactly(2)); // Two weeks ago for each child
	}

	[Fact]
	public async Task PostWeekLettersToChannelsAsync_CallsDataServiceForEachChild()
	{
		// Arrange
		var today = DateOnly.FromDateTime(DateTime.Today);
		var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
		var weekNumber = calendar.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue),
			System.Globalization.CalendarWeekRule.FirstFourDayWeek,
			DayOfWeek.Monday);

		var weekLetter = new JObject();
		_mockDataService.Setup(d => d.GetWeekLetterAsync(
			It.IsAny<Child>(),
			weekNumber,
			today.Year))
			.ReturnsAsync(weekLetter);

		// Act
		await _coordinator.PostWeekLettersToChannelsAsync();

		// Assert
		_mockDataService.Verify(d => d.GetWeekLetterAsync(
			It.IsAny<Child>(),
			weekNumber,
			today.Year), Times.Exactly(2)); // Once for each child
	}

	[Fact]
	public async Task FetchWeekLetterForChildAsync_CallsDataService()
	{
		// Arrange
		var testChild = _testChildren.First();
		var testDate = DateOnly.FromDateTime(DateTime.Today);
		var weekLetter = new JObject();

		_mockDataService.Setup(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			testDate,
			true))
			.ReturnsAsync(weekLetter);

		// Act
		var result = await _coordinator.FetchWeekLetterForChildAsync(testChild, testDate);

		// Assert
		Assert.True(result);
		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			testDate,
			true), Times.Once);
	}

	[Fact]
	public async Task GetWeekLetterForChildAsync_CallsDataService()
	{
		// Arrange
		var testChild = _testChildren.First();
		var testDate = DateOnly.FromDateTime(DateTime.Today);
		var weekLetter = new JObject();

		_mockDataService.Setup(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			testDate,
			true))
			.ReturnsAsync(weekLetter);

		// Act
		var result = await _coordinator.GetWeekLetterForChildAsync(testChild, testDate);

		// Assert
		Assert.NotNull(result);
		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			testDate,
			true), Times.Once);
	}

	[Fact]
	public async Task SeedHistoricalDataForChildAsync_CallsDataServiceForEachWeek()
	{
		// Arrange
		var testChild = _testChildren.First();
		var weeksBack = 3;

		_mockDataService.Setup(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			It.IsAny<DateOnly>(),
			false))
			.ReturnsAsync((JObject?)null);

		// Act
		await _coordinator.SeedHistoricalDataForChildAsync(testChild, weeksBack);

		// Assert
		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			testChild,
			It.IsAny<DateOnly>(),
			false), Times.Exactly(weeksBack));
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
	public async Task FetchWeekLettersForAllChildrenAsync_CallsDataServiceForAllChildren()
	{
		// Arrange
		var testDate = DateOnly.FromDateTime(DateTime.Today);
		var weekLetter = new JObject();

		_mockDataService.Setup(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			testDate,
			true))
			.ReturnsAsync(weekLetter);

		// Act
		var results = await _coordinator.FetchWeekLettersForAllChildrenAsync(testDate);

		// Assert
		Assert.Equal(2, results.Count());
		_mockDataService.Verify(d => d.GetOrFetchWeekLetterAsync(
			It.IsAny<Child>(),
			testDate,
			true), Times.Exactly(2));
	}

	[Fact]
	public async Task GetAllChildrenAsync_ReturnsChildrenFromAgentService()
	{
		// Act
		var result = await _coordinator.GetAllChildrenAsync();

		// Assert
		Assert.Equal(_testChildren, result);
		_mockAgentService.Verify(a => a.GetAllChildrenAsync(), Times.Once);
	}
}
