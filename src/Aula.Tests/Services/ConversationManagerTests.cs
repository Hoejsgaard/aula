using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Services;
using OpenAI.ObjectModels.RequestModels;
using System.Collections.Generic;

namespace Aula.Tests.Services;

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
            .Returns(ChatMessage.FromSystem("Test week letter content"));
        
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
}