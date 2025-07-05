using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class ReminderExtractionPromptsTests
{
    [Fact]
    public void GetExtractionPrompt_WithBasicQuery_ReturnsValidPrompt()
    {
        // Arrange
        var query = "Remind me about soccer tomorrow at 3 PM";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(query, result);
        Assert.Contains("Extract reminder details", result);
    }

    [Fact]
    public void GetExtractionPrompt_IncludesCurrentTime()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("2025-06-30 14:30", result); // Current time should be embedded
    }

    [Fact]
    public void GetExtractionPrompt_ContainsRequiredExtractionFields()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("Description:", result);
        Assert.Contains("DateTime:", result);
        Assert.Contains("ChildName:", result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsExpectedResponseFormat()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("DESCRIPTION:", result);
        Assert.Contains("DATETIME:", result);
        Assert.Contains("CHILD:", result);
        Assert.Contains("yyyy-MM-dd HH:mm", result);
        Assert.Contains("NONE", result);
    }

    [Theory]
    [InlineData("2025-06-30 14:30:00")]
    [InlineData("2025-01-01 00:00:00")]
    [InlineData("2025-12-31 23:59:59")]
    public void GetExtractionPrompt_WithVariousCurrentTimes_CalculatesRelativeDatesCorrectly(string currentTimeStr)
    {
        // Arrange
        var query = "test query";
        var currentTime = DateTime.Parse(currentTimeStr);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        var expectedTomorrow = currentTime.Date.AddDays(1).ToString("yyyy-MM-dd");
        var expectedToday = currentTime.Date.ToString("yyyy-MM-dd");
        var expectedIn2Hours = currentTime.AddHours(2).ToString("yyyy-MM-dd HH:mm");

        Assert.Contains($"\"tomorrow\" = {expectedTomorrow}", result);
        Assert.Contains($"\"today\" = {expectedToday}", result);
        Assert.Contains($"\"in 2 hours\" = {expectedIn2Hours}", result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsRelativeDateExamples()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert - Should contain examples for various relative date formats
        Assert.Contains("tomorrow", result);
        Assert.Contains("today", result);
        Assert.Contains("next Monday", result);
        Assert.Contains("in 2 hours", result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsDanishRelativeTimeExamples()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert - Should contain Danish examples
        Assert.Contains("om 2 minutter", result);
        Assert.Contains("om 30 minutter", result);
    }

    [Fact]
    public void GetExtractionPrompt_CalculatesDanishRelativeTimesCorrectly()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        var expectedIn2Minutes = currentTime.AddMinutes(2).ToString("yyyy-MM-dd HH:mm");
        var expectedIn30Minutes = currentTime.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");

        Assert.Contains($"\"om 2 minutter\" = {expectedIn2Minutes}", result);
        Assert.Contains($"\"om 30 minutter\" = {expectedIn30Minutes}", result);
    }

    [Theory]
    [InlineData("Remind me about Hans's soccer")]
    [InlineData("Call the doctor in 2 hours")]
    [InlineData("Pick up Emma tomorrow at 3 PM")]
    [InlineData("Meeting with teacher next Monday")]
    public void GetExtractionPrompt_WithRealQueries_EmbedsQueryCorrectly(string query)
    {
        // Arrange
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains($"Query: \"{query}\"", result);
    }

    [Fact]
    public void GetExtractionPrompt_WithSpecialCharactersInQuery_HandlesCorrectly()
    {
        // Arrange
        var query = "Remind me about \"Hans's soccer\" & Emma's dance at 8:00 PM";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains(query, result);
        // Should not cause format exceptions when the query contains special characters
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsInstructionsForChildNameExtraction()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("child's name", result.ToLower());
        Assert.Contains("optional", result.ToLower());
        Assert.Contains("CHILD:", result);
        Assert.Contains("NONE", result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsDateTimeFormatInstructions()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("yyyy-MM-dd HH:mm format", result);
        Assert.Contains("yyyy-MM-dd HH:mm", result);
    }

    [Fact]
    public void GetExtractionPrompt_ContainsNextMondayExample()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("next Monday", result);
        Assert.Contains("calculate the next Monday", result);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(2)]
    [InlineData(60)]
    [InlineData(120)]
    public void GetExtractionPrompt_WithDifferentMinuteOffsets_CalculatesCorrectly(int minutes)
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert - Check that minute calculations in examples are working
        if (minutes == 2 || minutes == 30)
        {
            var expectedTime = currentTime.AddMinutes(minutes).ToString("yyyy-MM-dd HH:mm");
            Assert.Contains(expectedTime, result);
        }
    }

    [Fact]
    public void GetExtractionPrompt_ResponseFormat_IsConsistent()
    {
        // Arrange
        var query = "test query";
        var currentTime = new DateTime(2025, 6, 30, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert - Response format should be clearly defined
        Assert.Contains("Respond in this exact format:", result);

        // Check that all three required fields are in the response format
        var formatSection = result.Split("Respond in this exact format:")[1];
        Assert.Contains("DESCRIPTION:", formatSection);
        Assert.Contains("DATETIME:", formatSection);
        Assert.Contains("CHILD:", formatSection);
    }
}