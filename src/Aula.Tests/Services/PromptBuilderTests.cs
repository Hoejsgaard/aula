using Xunit;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using System.Collections.Generic;

namespace Aula.Tests.Services;

public class PromptBuilderTests
{
    private static PromptBuilder CreateTestPromptBuilder()
    {
        return new PromptBuilder();
    }

    [Fact]
    public void CreateSystemInstructionsMessage_WithValidParameters_ReturnsSystemMessage()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Emma";
        var chatInterface = ChatInterface.Slack;

        // Act
        var result = builder.CreateSystemInstructionsMessage(childName, chatInterface);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("system", result.Role);
        Assert.Contains("Emma", result.Content);
        Assert.Contains("weekly school letter", result.Content);
        Assert.Contains("Slack", result.Content);
    }

    [Fact]
    public void CreateSystemInstructionsMessage_WithTelegramInterface_IncludesTelegramInstructions()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Søren Johannes";
        var chatInterface = ChatInterface.Telegram;

        // Act
        var result = builder.CreateSystemInstructionsMessage(childName, chatInterface);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Telegram", result.Content);
        Assert.Contains("HTML tags", result.Content);
        Assert.Contains("<b>", result.Content);
        Assert.Contains("<i>", result.Content);
    }

    [Fact]
    public void CreateWeekLetterContentMessage_WithValidParameters_ReturnsSystemMessage()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Emma";
        var weekLetterContent = "This week we will have math on Monday and science on Tuesday.";

        // Act
        var result = builder.CreateWeekLetterContentMessage(childName, weekLetterContent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("system", result.Role);
        Assert.Contains("Emma", result.Content);
        Assert.Contains("weekly letter content", result.Content);
        Assert.Contains(weekLetterContent, result.Content);
    }

    [Fact]
    public void CreateSummarizationMessages_WithValidParameters_ReturnsCorrectMessages()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Emma";
        var className = "2A";
        var weekNumber = "15";
        var weekLetterContent = "Math on Monday, Science on Tuesday";
        var chatInterface = ChatInterface.Slack;

        // Act
        var result = builder.CreateSummarizationMessages(childName, className, weekNumber, weekLetterContent, chatInterface);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // System message
        Assert.Equal("system", result[0].Role);
        Assert.Contains("summarizes weekly school letters", result[0].Content);
        Assert.Contains("Slack", result[0].Content);

        // User message
        Assert.Equal("user", result[1].Role);
        Assert.Contains(className, result[1].Content);
        Assert.Contains(weekNumber, result[1].Content);
        Assert.Contains(weekLetterContent, result[1].Content);
    }

    [Fact]
    public void CreateKeyInformationExtractionMessages_WithValidParameters_ReturnsCorrectMessages()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Emma";
        var className = "2A";
        var weekNumber = "15";
        var weekLetterContent = "Math on Monday, Science on Tuesday";

        // Act
        var result = builder.CreateKeyInformationExtractionMessages(childName, className, weekNumber, weekLetterContent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // System message
        Assert.Equal("system", result[0].Role);
        Assert.Contains("extracts structured information", result[0].Content);
        Assert.Contains("JSON format", result[0].Content);
        Assert.Contains("monday", result[0].Content);

        // User message
        Assert.Equal("user", result[1].Role);
        Assert.Contains(childName, result[1].Content);
        Assert.Contains(className, result[1].Content);
        Assert.Contains(weekNumber, result[1].Content);
        Assert.Contains(weekLetterContent, result[1].Content);
    }

    [Fact]
    public void CreateMultiChildMessages_WithValidParameters_ReturnsCorrectMessages()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childrenContent = new Dictionary<string, string>
        {
            { "Emma", "Math on Monday for Emma's class" },
            { "Søren Johannes", "Science on Tuesday for Søren Johannes's class" }
        };
        var chatInterface = ChatInterface.Telegram;

        // Act
        var result = builder.CreateMultiChildMessages(childrenContent, chatInterface);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        // System message
        Assert.Equal("system", result[0].Role);
        Assert.Contains("multiple children", result[0].Content);
        Assert.Contains("Telegram", result[0].Content);
        Assert.Contains("HTML tags", result[0].Content);

        // User message with combined content
        Assert.Equal("user", result[1].Role);
        Assert.Contains("Emma", result[1].Content);
        Assert.Contains("Søren Johannes", result[1].Content);
        Assert.Contains("Math on Monday", result[1].Content);
        Assert.Contains("Science on Tuesday", result[1].Content);
    }

    [Fact]
    public void GetChatInterfaceInstructions_WithSlack_ReturnsSlackInstructions()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();

        // Act
        var result = builder.GetChatInterfaceInstructions(ChatInterface.Slack);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Slack", result);
        Assert.Contains("markdown", result);
        Assert.Contains("*bold*", result);
        Assert.Contains("_italic_", result);
        Assert.Contains("`code`", result);
        Assert.DoesNotContain("HTML tags", result.Replace("Don't use HTML tags", "")); // Slack says don't use HTML tags
    }

    [Fact]
    public void GetChatInterfaceInstructions_WithTelegram_ReturnsTelegramInstructions()
    {
        // Arrange
        var builder = CreateTestPromptBuilder();

        // Act
        var result = builder.GetChatInterfaceInstructions(ChatInterface.Telegram);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Telegram", result);
        Assert.Contains("HTML tags", result);
        Assert.Contains("<b>bold</b>", result);
        Assert.Contains("<i>italic</i>", result);
        Assert.Contains("<code>code</code>", result);
        Assert.Contains("<br/>", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateWeekLetterContentMessage_WithEmptyContent_HandlesGracefully(string? content)
    {
        // Arrange
        var builder = CreateTestPromptBuilder();
        var childName = "Emma";

        // Act
        var result = builder.CreateWeekLetterContentMessage(childName, content ?? "");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("system", result.Role);
        Assert.Contains("Emma", result.Content);
    }
}
