using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.AI.Services;
using Aula.AI.Prompts;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;
using OpenAI.ObjectModels.RequestModels;
using System.Collections.Generic;

namespace Aula.Tests.AI.Services;

public class ConversationManagerTests
{
    private static ConversationManager CreateTestConversationManager()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        var mockPromptBuilder = new Mock<IPromptBuilder>();
        mockPromptBuilder.Setup(x => x.CreateSystemInstructionsMessage(It.IsAny<string>(), It.IsAny<ChatInterface>()))
            .Returns(ChatMessage.FromSystem("Test system message"));
        mockPromptBuilder.Setup(x => x.CreateWeekLetterContentMessage(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(ChatMessage.FromSystem("Here's the weekly letter content"));

        return new ConversationManager(mockLoggerFactory.Object, mockPromptBuilder.Object);
    }

    [Fact]
    public void ConversationManager_Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var manager = CreateTestConversationManager();

        // Assert
        Assert.NotNull(manager);
    }

    [Fact]
    public void ConversationManager_Constructor_WithNullLoggerFactory_ThrowsException()
    {
        // Arrange
        var mockPromptBuilder = new Mock<IPromptBuilder>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConversationManager(null!, mockPromptBuilder.Object));
    }

    [Fact]
    public void ConversationManager_Constructor_WithNullPromptBuilder_ThrowsException()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConversationManager(mockLoggerFactory.Object, null!));
    }

    [Fact]
    public void EnsureContextKey_WithNullContextKey_GeneratesFromChildName()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var childName = "TestChild";

        // Act
        var result = manager.EnsureContextKey(null, childName);

        // Assert
        Assert.Equal("testchild", result);
    }

    [Fact]
    public void EnsureContextKey_WithEmptyContextKey_GeneratesFromChildName()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var childName = "TestChild";

        // Act
        var result = manager.EnsureContextKey("", childName);

        // Assert
        Assert.Equal("testchild", result);
    }

    [Fact]
    public void EnsureContextKey_WithProvidedContextKey_ReturnsProvidedKey()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "custom-context";
        var childName = "TestChild";

        // Act
        var result = manager.EnsureContextKey(contextKey, childName);

        // Assert
        Assert.Equal("custom-context", result);
    }

    [Fact]
    public void GetConversationHistory_WithNewContextKey_ReturnsEmptyList()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "new-context";

        // Act
        var result = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void EnsureConversationHistory_WithNewContext_InitializesHistory()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        // Act
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.NotNull(history);
        Assert.Equal(2, history.Count); // System instructions + week letter content
        Assert.Equal("system", history[0].Role);
        Assert.Equal("system", history[1].Role);
    }

    [Fact]
    public void AddUserQuestionToHistory_AddsMessageToHistory()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";
        var question = "What activities are planned?";

        // Act
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        manager.AddUserQuestionToHistory(contextKey, question);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal("user", history[2].Role);
        Assert.Equal(question, history[2].Content);
    }

    [Fact]
    public void AddAssistantResponseToHistory_AddsMessageToHistory()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";
        var response = "Here are the planned activities...";

        // Act
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        manager.AddAssistantResponseToHistory(contextKey, response);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(3, history.Count);
        Assert.Equal("assistant", history[2].Role);
        Assert.Equal(response, history[2].Content);
    }

    [Fact]
    public void ClearConversationHistory_WithSpecificContextKey_RemovesOnlyThatContext()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey1 = "context1";
        var contextKey2 = "context2";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        // Act
        manager.EnsureConversationHistory(contextKey1, childName, weekLetterContent, ChatInterface.Slack);
        manager.EnsureConversationHistory(contextKey2, childName, weekLetterContent, ChatInterface.Slack);
        manager.ClearConversationHistory(contextKey1);

        var history1 = manager.GetConversationHistory(contextKey1);
        var history2 = manager.GetConversationHistory(contextKey2);

        // Assert
        Assert.Empty(history1);
        Assert.Equal(2, history2.Count);
    }

    [Fact]
    public void ClearConversationHistory_WithNullContextKey_RemovesAllContexts()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey1 = "context1";
        var contextKey2 = "context2";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        // Act
        manager.EnsureConversationHistory(contextKey1, childName, weekLetterContent, ChatInterface.Slack);
        manager.EnsureConversationHistory(contextKey2, childName, weekLetterContent, ChatInterface.Slack);
        manager.ClearConversationHistory(null);

        var history1 = manager.GetConversationHistory(contextKey1);
        var history2 = manager.GetConversationHistory(contextKey2);

        // Assert
        Assert.Empty(history1);
        Assert.Empty(history2);
    }

    [Fact]
    public void TrimConversationHistoryIfNeeded_WithLongHistory_TrimsToCorrectSize()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);

        // Add more than MaxConversationHistoryRegular (12) messages
        for (int i = 0; i < 15; i++)
        {
            manager.AddUserQuestionToHistory(contextKey, $"Question {i}");
            manager.AddAssistantResponseToHistory(contextKey, $"Response {i}");
        }

        // Act
        manager.TrimConversationHistoryIfNeeded(contextKey);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.True(history.Count <= 12); // Should be trimmed to max limit
        Assert.Equal("system", history[0].Role); // First system message should be preserved
        Assert.Equal("system", history[1].Role); // Second system message should be preserved
    }

    [Fact]
    public void TrimConversationHistoryIfNeeded_WithShortHistory_DoesNotTrim()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        manager.AddUserQuestionToHistory(contextKey, "Question 1");
        manager.AddAssistantResponseToHistory(contextKey, "Response 1");

        var originalCount = manager.GetConversationHistory(contextKey).Count;

        // Act
        manager.TrimConversationHistoryIfNeeded(contextKey);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(originalCount, history.Count); // Should not be trimmed
    }

    [Fact]
    public void TrimMultiChildConversationIfNeeded_WithNonExistentContext_CreatesEmptyHistory()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "non-existent-context";

        // Act
        manager.TrimMultiChildConversationIfNeeded(contextKey);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public void TrimMultiChildConversationIfNeeded_WithLongHistory_TrimsCorrectly()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);

        // Add more than MaxConversationHistoryWeekLetter (20) messages
        // Starting with 2 system messages, need to add 19 more to exceed 20
        for (int i = 0; i < 20; i++)
        {
            manager.AddUserQuestionToHistory(contextKey, $"Question {i}");
        }

        var originalCount = manager.GetConversationHistory(contextKey).Count;
        Assert.True(originalCount > 20); // Verify we exceed the limit

        // Act
        manager.TrimMultiChildConversationIfNeeded(contextKey);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.True(history.Count < originalCount); // Should be trimmed
                                                    // The method skips the first 4 messages when trimming
        Assert.Equal(originalCount - 4, history.Count);
    }

    [Fact]
    public void TrimMultiChildConversationIfNeeded_WithShortHistory_DoesNotTrim()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        manager.AddUserQuestionToHistory(contextKey, "Question 1");

        var originalCount = manager.GetConversationHistory(contextKey).Count;

        // Act
        manager.TrimMultiChildConversationIfNeeded(contextKey);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(originalCount, history.Count); // Should not be trimmed
    }

    [Fact]
    public void EnsureConversationHistory_WithExistingSameChild_UpdatesContent()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var originalContent = "Original content";
        var updatedContent = "Updated content";

        // Initialize with original content
        manager.EnsureConversationHistory(contextKey, childName, originalContent, ChatInterface.Slack);

        // Add some conversation history
        manager.AddUserQuestionToHistory(contextKey, "Test question");
        manager.AddAssistantResponseToHistory(contextKey, "Test response");

        var historyCountBeforeUpdate = manager.GetConversationHistory(contextKey).Count;

        // Act - Update with new content for same child
        manager.EnsureConversationHistory(contextKey, childName, updatedContent, ChatInterface.Slack);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(historyCountBeforeUpdate, history.Count); // Count should remain same
                                                               // The week letter content should be updated
        Assert.Contains(history, msg => msg.Role == "system" && msg.Content != null);
    }

    [Fact]
    public void EnsureConversationHistory_WithExistingDifferentChild_ResetsHistory()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName1 = "Child1";
        var childName2 = "Child2";
        var weekLetterContent = "Test content";

        // Initialize with first child and set context
        manager.EnsureContextKey(contextKey, childName1);
        manager.EnsureConversationHistory(contextKey, childName1, weekLetterContent, ChatInterface.Slack);
        manager.AddUserQuestionToHistory(contextKey, "Question for child 1");

        // Act - Switch to different child
        manager.EnsureConversationHistory(contextKey, childName2, weekLetterContent, ChatInterface.Slack);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(2, history.Count); // Should be reset to initial state (system + week letter)
        Assert.All(history, msg => Assert.Equal("system", msg.Role));
    }

    [Fact]
    public void ClearConversationHistory_WithEmptyContextKey_RemovesAllContexts()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey1 = "context1";
        var contextKey2 = "context2";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        manager.EnsureConversationHistory(contextKey1, childName, weekLetterContent, ChatInterface.Slack);
        manager.EnsureConversationHistory(contextKey2, childName, weekLetterContent, ChatInterface.Slack);

        // Act
        manager.ClearConversationHistory("");

        var history1 = manager.GetConversationHistory(contextKey1);
        var history2 = manager.GetConversationHistory(contextKey2);

        // Assert
        Assert.Empty(history1);
        Assert.Empty(history2);
    }

    [Fact]
    public void ClearConversationHistory_WithNonExistentContextKey_DoesNotThrow()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var nonExistentKey = "non-existent";

        // Act & Assert
        manager.ClearConversationHistory(nonExistentKey); // Should not throw
    }

    [Fact]
    public void EnsureContextKey_StoresChildNameCorrectly()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";

        // Act
        manager.EnsureContextKey(contextKey, childName);

        // Verify by switching to different child - should reset conversation
        manager.EnsureConversationHistory(contextKey, childName, "content1", ChatInterface.Slack);
        manager.AddUserQuestionToHistory(contextKey, "Question 1");

        manager.EnsureConversationHistory(contextKey, "DifferentChild", "content2", ChatInterface.Slack);
        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(2, history.Count); // Should be reset due to child change
    }

    [Fact]
    public void ConversationManager_WithDifferentChatInterfaces_WorksCorrectly()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        // Act & Assert for Slack
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);
        var slackHistory = manager.GetConversationHistory(contextKey);
        Assert.Equal(2, slackHistory.Count);

        // Act & Assert for Telegram  
        manager.ClearConversationHistory(contextKey);
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Telegram);
        var telegramHistory = manager.GetConversationHistory(contextKey);
        Assert.Equal(2, telegramHistory.Count);
    }

    [Fact]
    public void ConversationManager_HandlesConcurrentAccess()
    {
        // Arrange
        var manager = CreateTestConversationManager();
        var contextKey = "test-context";
        var childName = "TestChild";
        var weekLetterContent = "Test content";

        // Act - Simulate concurrent access
        manager.EnsureConversationHistory(contextKey, childName, weekLetterContent, ChatInterface.Slack);

        // Multiple operations on same context
        manager.AddUserQuestionToHistory(contextKey, "Question 1");
        manager.AddAssistantResponseToHistory(contextKey, "Response 1");
        manager.AddUserQuestionToHistory(contextKey, "Question 2");
        manager.AddAssistantResponseToHistory(contextKey, "Response 2");

        var history = manager.GetConversationHistory(contextKey);

        // Assert
        Assert.Equal(6, history.Count); // 2 system + 4 conversation messages
        Assert.Equal("user", history[2].Role);
        Assert.Equal("assistant", history[3].Role);
        Assert.Equal("user", history[4].Role);
        Assert.Equal("assistant", history[5].Role);
    }
}
