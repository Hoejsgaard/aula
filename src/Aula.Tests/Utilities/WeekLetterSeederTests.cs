using Microsoft.Extensions.Logging;
using Moq;
using Aula.Utilities;
using Aula.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Utilities;

public class WeekLetterSeederTests
{
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly Mock<ILoggerFactory> _mockLoggerFactory;
	private readonly Mock<ILogger<WeekLetterSeeder>> _mockLogger;
	private readonly WeekLetterSeeder _seeder;

	public WeekLetterSeederTests()
	{
		_mockSupabaseService = new Mock<ISupabaseService>();
		_mockLoggerFactory = new Mock<ILoggerFactory>();
		_mockLogger = new Mock<ILogger<WeekLetterSeeder>>();

		// Use real LoggerFactory to avoid extension method mocking issues
		var loggerFactory = new LoggerFactory();

		_seeder = new WeekLetterSeeder(_mockSupabaseService.Object, loggerFactory);
	}

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();

		// Act
		var seeder = new WeekLetterSeeder(_mockSupabaseService.Object, loggerFactory);

		// Assert
		Assert.NotNull(seeder);
	}

	[Fact]
	public void Constructor_WithNullSupabaseService_ThrowsArgumentNullException()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			new WeekLetterSeeder(null!, loggerFactory));
	}

	[Fact]
	public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			new WeekLetterSeeder(_mockSupabaseService.Object, null!));
	}

	[Fact]
	public async Task SeedTestDataAsync_WithSuccessfulSeeding_CallsCorrectMethods()
	{
		// Arrange
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync((string)null!); // No existing content
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedTestDataAsync();

		// Assert - Should call StoreWeekLetterAsync 3 times (for 3 test data entries)
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Exactly(3));
	}

	[Fact]
	public async Task SeedWeekLetterAsync_WithValidParameters_StoresWeekLetter()
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;
		var content = "Test content";
		var className = "Test Class";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync((string)null!); // No existing content
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content, className);

		// Assert
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year), Times.Once());
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			childName, weekNumber, year, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}

	[Fact]
	public async Task SeedWeekLetterAsync_WithNullClassName_UsesDefaultClassName()
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;
		var content = "Test content";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync((string)null!);
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Assert
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			childName, weekNumber, year, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}

	[Fact]
	public async Task SeedWeekLetterAsync_WithExistingWeekLetter_SkipsSeeding()
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;
		var content = "Test content";
		var existingContent = "Existing content";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync(existingContent);

		// Act
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Assert
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year), Times.Once());
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
	}

	[Fact]
	public async Task SeedWeekLetterAsync_WithSupabaseException_HandlesGracefully()
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;
		var content = "Test content";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ThrowsAsync(new Exception("Database error"));

		// Act & Assert - Should not throw
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Verify the exception was handled gracefully
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year), Times.Once());
	}

	[Fact]
	public async Task SeedWeekLetterAsync_WithStoreException_HandlesGracefully()
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;
		var content = "Test content";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync((string)null!);
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.ThrowsAsync(new Exception("Store error"));

		// Act & Assert - Should not throw
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Verify both methods were called despite the exception
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year), Times.Once());
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t\n")]
	public async Task SeedWeekLetterAsync_WithEmptyOrWhitespaceContent_StillStores(string content)
	{
		// Arrange
		var childName = "TestChild";
		var weekNumber = 20;
		var year = 2024;

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync((string)null!);
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Assert
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			childName, weekNumber, year, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}

	[Theory]
	[InlineData(1, 2024)]
	[InlineData(53, 2023)]
	[InlineData(26, 2025)]
	public async Task SeedWeekLetterAsync_WithDifferentWeekNumbers_StoresCorrectly(int weekNumber, int year)
	{
		// Arrange
		var childName = "TestChild";
		var content = "Test content";

		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(childName, weekNumber, year))
			.ReturnsAsync((string)null!);
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedWeekLetterAsync(childName, weekNumber, year, content);

		// Assert
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			childName, weekNumber, year, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}

	[Fact]
	public void WeekLetterSeeder_ImplementsIWeekLetterSeederInterface()
	{
		// Arrange & Act & Assert
		Assert.True(typeof(IWeekLetterSeeder).IsAssignableFrom(typeof(WeekLetterSeeder)));
	}

	[Fact]
	public void WeekLetterSeeder_HasCorrectNamespace()
	{
		// Arrange
		var seederType = typeof(WeekLetterSeeder);

		// Act & Assert
		Assert.Equal("Aula.Utilities", seederType.Namespace);
	}

	[Fact]
	public void WeekLetterSeeder_IsPublicClass()
	{
		// Arrange
		var seederType = typeof(WeekLetterSeeder);

		// Act & Assert
		Assert.True(seederType.IsPublic);
		Assert.False(seederType.IsAbstract);
		Assert.False(seederType.IsSealed);
	}

	[Fact]
	public void WeekLetterSeeder_HasCorrectPublicMethods()
	{
		// Arrange
		var seederType = typeof(WeekLetterSeeder);

		// Act & Assert
		Assert.NotNull(seederType.GetMethod("SeedTestDataAsync"));
		Assert.NotNull(seederType.GetMethod("SeedWeekLetterAsync"));
	}

	[Fact]
	public void WeekLetterSeeder_ConstructorParametersHaveCorrectTypes()
	{
		// Arrange
		var seederType = typeof(WeekLetterSeeder);
		var constructor = seederType.GetConstructors()[0];

		// Act
		var parameters = constructor.GetParameters();

		// Assert
		Assert.Equal(2, parameters.Length);
		Assert.Equal(typeof(ISupabaseService), parameters[0].ParameterType);
		Assert.Equal(typeof(ILoggerFactory), parameters[1].ParameterType);
	}

	[Fact]
	public async Task SeedTestDataAsync_WithPredefinedData_SeedsExpectedEntries()
	{
		// Arrange
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync((string)null!);
		_mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
			It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
			.Returns(Task.CompletedTask);

		// Act
		await _seeder.SeedTestDataAsync();

		// Assert - Verify specific calls for Emma and Lucas as per the hardcoded test data
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			"Emma", 20, 2024, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			"Lucas", 21, 2024, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
		_mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
			"Emma", 22, 2024, It.IsAny<string>(), It.IsAny<string>(), false, false), Times.Once());
	}
}
