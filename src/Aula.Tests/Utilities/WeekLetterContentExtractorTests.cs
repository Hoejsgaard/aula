using Xunit;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class WeekLetterContentExtractorTests
{
	[Fact]
	public void ExtractContent_WithValidJObject_ReturnsContent()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": "Test week letter content"
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("Test week letter content", result);
	}

	[Fact]
	public void ExtractContent_WithEmptyUgebreve_ReturnsEmptyString()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": []
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithNullUgebreve_ReturnsEmptyString()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": null
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithMissingUgebreve_ReturnsEmptyString()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"otherProperty": "value"
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithNullIndhold_ReturnsEmptyString()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": null
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithMissingIndhold_ReturnsEmptyString()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"otherProperty": "value"
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithLogger_LogsWarningForEmptyContent()
	{
		// Arrange
		var mockLogger = new Mock<ILogger>();
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": ""
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter, mockLogger.Object);

		// Assert
		Assert.Equal("", result);
		mockLogger.Verify(
			x => x.Log(
				LogLevel.Warning,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Equals("Week letter content is empty")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public void ExtractContent_WithNullLogger_DoesNotThrow()
	{
		// Arrange
		var weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": ""
					}
				]
			}
			""");

		// Act & Assert - Should not throw
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter, null);
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_WithException_ReturnsEmptyStringAndLogsError()
	{
		// Arrange
		var mockLogger = new Mock<ILogger>();
		// Create an invalid JObject that will cause an exception
		JObject invalidWeekLetter = null!;

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(invalidWeekLetter, mockLogger.Object);

		// Assert
		Assert.Equal("", result);
		mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error extracting week letter content")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public void ExtractContent_Dynamic_WithValidData_ReturnsContent()
	{
		// Arrange
		dynamic weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": "Dynamic test content"
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("Dynamic test content", result);
	}

	[Fact]
	public void ExtractContent_Dynamic_WithNullUgebreve_ReturnsEmptyString()
	{
		// Arrange
		dynamic weekLetter = JObject.Parse("""
			{
				"ugebreve": null
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_Dynamic_WithEmptyArray_ReturnsEmptyString()
	{
		// Arrange
		dynamic weekLetter = JObject.Parse("""
			{
				"ugebreve": []
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("", result);
	}

	[Fact]
	public void ExtractContent_Dynamic_WithException_ReturnsEmptyStringAndLogsError()
	{
		// Arrange
		var mockLogger = new Mock<ILogger>();
		// Create a dynamic object that will cause an exception when accessing ["ugebreve"]
		dynamic invalidWeekLetter = new { someProperty = "value" };
		// Force a reflection exception by trying to access non-existent indexer

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(invalidWeekLetter, mockLogger.Object);

		// Assert
		Assert.Equal("", result);
		mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error extracting week letter content from dynamic object")),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once());
	}

	[Fact]
	public void ExtractContent_Dynamic_WithMultipleUgebreve_ReturnsFirstContent()
	{
		// Arrange
		dynamic weekLetter = JObject.Parse("""
			{
				"ugebreve": [
					{
						"indhold": "First content"
					},
					{
						"indhold": "Second content"
					}
				]
			}
			""");

		// Act
		var result = WeekLetterContentExtractor.ExtractContent(weekLetter);

		// Assert
		Assert.Equal("First content", result);
	}
}
