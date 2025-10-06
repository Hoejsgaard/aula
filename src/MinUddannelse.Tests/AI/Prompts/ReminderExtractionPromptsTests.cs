using MinUddannelse.AI.Prompts;
using Xunit;

namespace MinUddannelse.Tests.AI.Prompts;

public class ReminderExtractionPromptsTests
{
    [Fact]
    public void GetExtractionPrompt_WithValidInput_ReturnsFormattedPrompt()
    {
        // Arrange
        var query = "Remind me tomorrow at 8am to pack lunch";
        var currentTime = new DateTime(2025, 10, 15, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("Extract reminder details from this natural language request:", result);
        Assert.Contains(query, result);
        Assert.Contains("2025-10-15 14:30", result);
        Assert.Contains("2025-10-16", result); // tomorrow
        Assert.Contains("DESCRIPTION: [extracted description]", result);
        Assert.Contains("DATETIME: [yyyy-MM-dd HH:mm]", result);
        Assert.Contains("CHILD: [child name or NONE]", result);
    }

    [Fact]
    public void GetExtractionPrompt_WithDanishRelativeTime_IncludesDanishExamples()
    {
        // Arrange
        var query = "Husk mig om 30 minutter";
        var currentTime = new DateTime(2025, 10, 15, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains("om 2 minutter", result);
        Assert.Contains("om 30 minutter", result);
        Assert.Contains("2025-10-15 15:00", result); // 30 minutes later
    }

    [Fact]
    public void GetWeekLetterEventExtractionPrompt_WithValidInput_ReturnsFormattedPrompt()
    {
        // Arrange
        var weekLetterContent = "Kære forældre, I morgen har vi udflugt til zoo. Husk madpakke og lette sko.";
        var currentTime = new DateTime(2025, 10, 15, 14, 30, 0);

        // Act
        var result = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(weekLetterContent, currentTime);

        // Assert
        Assert.Contains("You must respond with ONLY valid JSON", result);
        Assert.Contains("Extract ONLY actionable events that require parent/student preparation from this Danish school week letter", result);
        Assert.Contains(weekLetterContent, result);
        Assert.Contains("2025-10-15", result);
        Assert.Contains("JSON format:", result);
        Assert.Contains("Event types: deadline, permission_form, event, supply_needed", result);
        Assert.Contains("Return a JSON array of events", result);
    }

    [Fact]
    public void GetWeekLetterEventExtractionPrompt_ContainsCorrectResponseFormat()
    {
        // Arrange
        var weekLetterContent = "Test content";
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(weekLetterContent, currentTime);

        // Assert
        Assert.Contains("JSON format:", result);
        Assert.Contains("\"type\":", result);
        Assert.Contains("\"title\":", result);
        Assert.Contains("\"description\":", result);
        Assert.Contains("\"date\":", result);
        Assert.Contains("\"confidence\":", result);
        Assert.Contains("If no events found, return: []", result);
        Assert.Contains("Response must be valid JSON only", result);
    }

    [Fact]
    public void GetWeekLetterEventExtractionPrompt_ContainsInstructions()
    {
        // Arrange
        var weekLetterContent = "Test content";
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(weekLetterContent, currentTime);

        // Assert
        Assert.Contains("Extract ONLY actionable events", result);
        Assert.Contains("Only include events with confidence >= 0.8", result);
        Assert.Contains("Event types: deadline, permission_form, event, supply_needed", result);
        Assert.Contains("You must respond with ONLY valid JSON", result);
        Assert.Contains("No explanations, no markdown", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetExtractionPrompt_WithInvalidQuery_StillReturnsValidPrompt(string? query)
    {
        // Arrange
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query ?? string.Empty, currentTime);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Extract reminder details", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetWeekLetterEventExtractionPrompt_WithInvalidContent_StillReturnsValidPrompt(string? content)
    {
        // Arrange
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(content ?? string.Empty, currentTime);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("You must respond with ONLY valid JSON", result);
    }

    [Fact]
    public void GetExtractionPrompt_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var query = "Remind me about Emma's homework \"Math & Science\" at 6:30 PM";
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetExtractionPrompt(query, currentTime);

        // Assert
        Assert.Contains(query, result);
        Assert.Contains("Math & Science", result);
        Assert.DoesNotContain("{{", result); // No unresolved template variables
    }

    [Fact]
    public void GetWeekLetterEventExtractionPrompt_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var content = "Kære forældre, børnene skal have \"sportsudstyr\" & madpakke i morgen.";
        var currentTime = DateTime.Now;

        // Act
        var result = ReminderExtractionPrompts.GetWeekLetterEventExtractionPrompt(content, currentTime);

        // Assert
        Assert.Contains(content, result);
        Assert.Contains("sportsudstyr", result);
        Assert.DoesNotContain("{{", result); // No unresolved template variables
    }
}