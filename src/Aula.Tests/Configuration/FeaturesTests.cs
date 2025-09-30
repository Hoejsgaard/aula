using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class FeaturesTests
{
	[Fact]
	public void Constructor_InitializesDefaultValues()
	{
		// Act
		var features = new Features();

		// Assert
		Assert.True(features.WeekLetterPreloading);
		Assert.True(features.ParallelProcessing);
		Assert.Equal(60, features.ConversationCacheExpirationMinutes);
		Assert.False(features.UseStoredWeekLetters);
		Assert.Null(features.TestWeekNumber);
		Assert.Null(features.TestYear);
		Assert.False(features.SeedHistoricalData);
		Assert.False(features.UseMockData);
		Assert.Equal(15, features.MockCurrentWeek);
		Assert.Equal(2025, features.MockCurrentYear);
	}

	[Fact]
	public void WeekLetterPreloading_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();

		// Act
		features.WeekLetterPreloading = false;

		// Assert
		Assert.False(features.WeekLetterPreloading);
	}

	[Fact]
	public void ParallelProcessing_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();

		// Act
		features.ParallelProcessing = false;

		// Assert
		Assert.False(features.ParallelProcessing);
	}

	[Fact]
	public void ConversationCacheExpirationMinutes_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();
		var testValue = 120;

		// Act
		features.ConversationCacheExpirationMinutes = testValue;

		// Assert
		Assert.Equal(testValue, features.ConversationCacheExpirationMinutes);
	}

	[Fact]
	public void UseStoredWeekLetters_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();

		// Act
		features.UseStoredWeekLetters = true;

		// Assert
		Assert.True(features.UseStoredWeekLetters);
	}

	[Fact]
	public void TestWeekNumber_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();
		var testValue = 25;

		// Act
		features.TestWeekNumber = testValue;

		// Assert
		Assert.Equal(testValue, features.TestWeekNumber);
	}

	[Fact]
	public void TestYear_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();
		var testValue = 2024;

		// Act
		features.TestYear = testValue;

		// Assert
		Assert.Equal(testValue, features.TestYear);
	}

	[Fact]
	public void SeedHistoricalData_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();

		// Act
		features.SeedHistoricalData = true;

		// Assert
		Assert.True(features.SeedHistoricalData);
	}

	[Fact]
	public void UseMockData_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();

		// Act
		features.UseMockData = true;

		// Assert
		Assert.True(features.UseMockData);
	}

	[Fact]
	public void MockCurrentWeek_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();
		var testValue = 30;

		// Act
		features.MockCurrentWeek = testValue;

		// Assert
		Assert.Equal(testValue, features.MockCurrentWeek);
	}

	[Fact]
	public void MockCurrentYear_CanSetAndGetValue()
	{
		// Arrange
		var features = new Features();
		var testValue = 2026;

		// Act
		features.MockCurrentYear = testValue;

		// Assert
		Assert.Equal(testValue, features.MockCurrentYear);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(120)]
	[InlineData(1440)]
	public void ConversationCacheExpirationMinutes_AcceptsVariousValues(int minutes)
	{
		// Arrange
		var features = new Features();

		// Act
		features.ConversationCacheExpirationMinutes = minutes;

		// Assert
		Assert.Equal(minutes, features.ConversationCacheExpirationMinutes);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(1)]
	[InlineData(25)]
	[InlineData(52)]
	public void TestWeekNumber_AcceptsNullAndValidWeeks(int? weekNumber)
	{
		// Arrange
		var features = new Features();

		// Act
		features.TestWeekNumber = weekNumber;

		// Assert
		Assert.Equal(weekNumber, features.TestWeekNumber);
	}

	[Theory]
	[InlineData(null)]
	[InlineData(2020)]
	[InlineData(2024)]
	[InlineData(2025)]
	[InlineData(2030)]
	public void TestYear_AcceptsNullAndValidYears(int? year)
	{
		// Arrange
		var features = new Features();

		// Act
		features.TestYear = year;

		// Assert
		Assert.Equal(year, features.TestYear);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(15)]
	[InlineData(25)]
	[InlineData(52)]
	public void MockCurrentWeek_AcceptsValidWeekNumbers(int weekNumber)
	{
		// Arrange
		var features = new Features();

		// Act
		features.MockCurrentWeek = weekNumber;

		// Assert
		Assert.Equal(weekNumber, features.MockCurrentWeek);
	}

	[Theory]
	[InlineData(2020)]
	[InlineData(2024)]
	[InlineData(2025)]
	[InlineData(2030)]
	public void MockCurrentYear_AcceptsValidYears(int year)
	{
		// Arrange
		var features = new Features();

		// Act
		features.MockCurrentYear = year;

		// Assert
		Assert.Equal(year, features.MockCurrentYear);
	}

	[Fact]
	public void AllProperties_CanBeSetSimultaneously()
	{
		// Arrange
		var features = new Features();

		// Act
		features.WeekLetterPreloading = false;
		features.ParallelProcessing = false;
		features.ConversationCacheExpirationMinutes = 90;
		features.UseStoredWeekLetters = true;
		features.TestWeekNumber = 20;
		features.TestYear = 2024;
		features.SeedHistoricalData = true;
		features.UseMockData = true;
		features.MockCurrentWeek = 22;
		features.MockCurrentYear = 2024;

		// Assert
		Assert.False(features.WeekLetterPreloading);
		Assert.False(features.ParallelProcessing);
		Assert.Equal(90, features.ConversationCacheExpirationMinutes);
		Assert.True(features.UseStoredWeekLetters);
		Assert.Equal(20, features.TestWeekNumber);
		Assert.Equal(2024, features.TestYear);
		Assert.True(features.SeedHistoricalData);
		Assert.True(features.UseMockData);
		Assert.Equal(22, features.MockCurrentWeek);
		Assert.Equal(2024, features.MockCurrentYear);
	}

	[Fact]
	public void Features_ObjectInitializerSyntaxWorks()
	{
		// Arrange & Act
		var features = new Features
		{
			WeekLetterPreloading = false,
			ParallelProcessing = false,
			ConversationCacheExpirationMinutes = 180,
			UseStoredWeekLetters = true,
			TestWeekNumber = 35,
			TestYear = 2023,
			SeedHistoricalData = true,
			UseMockData = true,
			MockCurrentWeek = 40,
			MockCurrentYear = 2023
		};

		// Assert
		Assert.False(features.WeekLetterPreloading);
		Assert.False(features.ParallelProcessing);
		Assert.Equal(180, features.ConversationCacheExpirationMinutes);
		Assert.True(features.UseStoredWeekLetters);
		Assert.Equal(35, features.TestWeekNumber);
		Assert.Equal(2023, features.TestYear);
		Assert.True(features.SeedHistoricalData);
		Assert.True(features.UseMockData);
		Assert.Equal(40, features.MockCurrentWeek);
		Assert.Equal(2023, features.MockCurrentYear);
	}

	[Fact]
	public void Features_HasCorrectNamespace()
	{
		// Arrange
		var type = typeof(Features);

		// Act & Assert
		Assert.Equal("Aula.Configuration", type.Namespace);
	}

	[Fact]
	public void Features_IsPublicClass()
	{
		// Arrange
		var type = typeof(Features);

		// Act & Assert
		Assert.True(type.IsPublic);
		Assert.False(type.IsAbstract);
		Assert.False(type.IsSealed);
	}

	[Fact]
	public void Features_HasParameterlessConstructor()
	{
		// Arrange
		var type = typeof(Features);

		// Act
		var constructor = type.GetConstructor(System.Type.EmptyTypes);

		// Assert
		Assert.NotNull(constructor);
		Assert.True(constructor.IsPublic);
	}

	[Fact]
	public void Features_PropertiesAreIndependent()
	{
		// Arrange
		var features = new Features();

		// Act
		features.WeekLetterPreloading = false;
		// Other properties should remain at their defaults

		// Assert
		Assert.False(features.WeekLetterPreloading);
		Assert.True(features.ParallelProcessing); // Should remain default
		Assert.Equal(60, features.ConversationCacheExpirationMinutes); // Should remain default
		Assert.False(features.UseStoredWeekLetters); // Should remain default
		Assert.Null(features.TestWeekNumber); // Should remain default
		Assert.Null(features.TestYear); // Should remain default
		Assert.False(features.SeedHistoricalData); // Should remain default
		Assert.False(features.UseMockData); // Should remain default
		Assert.Equal(15, features.MockCurrentWeek); // Should remain default
		Assert.Equal(2025, features.MockCurrentYear); // Should remain default
	}

	[Fact]
	public void Features_MockDataPropertiesWorkTogether()
	{
		// Arrange
		var features = new Features();

		// Act
		features.UseMockData = true;
		features.MockCurrentWeek = 25;
		features.MockCurrentYear = 2024;

		// Assert
		Assert.True(features.UseMockData);
		Assert.Equal(25, features.MockCurrentWeek);
		Assert.Equal(2024, features.MockCurrentYear);
	}

	[Fact]
	public void Features_TestDataPropertiesWorkTogether()
	{
		// Arrange
		var features = new Features();

		// Act
		features.UseStoredWeekLetters = true;
		features.TestWeekNumber = 30;
		features.TestYear = 2023;

		// Assert
		Assert.True(features.UseStoredWeekLetters);
		Assert.Equal(30, features.TestWeekNumber);
		Assert.Equal(2023, features.TestYear);
	}

	[Fact]
	public void Features_CanResetNullablePropertiesToNull()
	{
		// Arrange
		var features = new Features
		{
			TestWeekNumber = 20,
			TestYear = 2024
		};

		// Act
		features.TestWeekNumber = null;
		features.TestYear = null;

		// Assert
		Assert.Null(features.TestWeekNumber);
		Assert.Null(features.TestYear);
	}
}
