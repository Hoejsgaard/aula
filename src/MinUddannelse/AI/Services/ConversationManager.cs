using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MinUddannelse.AI.Prompts;

namespace MinUddannelse.AI.Services;

public class ConversationManager : IConversationManager
{
    private readonly ILogger _logger;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversationHistory = new();
    private readonly ConcurrentDictionary<string, string> _currentChildContext = new();

    // Constants for conversation history management
    private const int MaxConversationHistoryRegular = 12;
    private const int MaxConversationHistoryWeekLetter = 20;
    private const int ConversationTrimAmount = 4;
    private const int ConversationContextLimit = 10;
    private const int ConversationStartMessages = 2;

    public ConversationManager(ILoggerFactory loggerFactory, IPromptBuilder promptBuilder)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(promptBuilder);

        _logger = loggerFactory.CreateLogger(nameof(ConversationManager));
        _promptBuilder = promptBuilder;
    }

    public string EnsureContextKey(string? contextKey, string childName)
    {
        if (string.IsNullOrEmpty(contextKey))
        {
            contextKey = childName.ToLowerInvariant();
        }
        _currentChildContext[contextKey] = childName;
        return contextKey;
    }

    public void EnsureConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            InitializeNewConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
        else
        {
            UpdateExistingConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
    }

    public void AddUserQuestionToHistory(string contextKey, string question)
    {
        _logger.LogInformation("🔎 TRACE: Adding user question to conversation history: {Question}", question);

        // Ensure conversation history exists for this context
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            _logger.LogWarning("Conversation history not found for context {ContextKey}, initializing empty history", contextKey);
            _conversationHistory[contextKey] = new List<ChatMessage>();
        }

        _conversationHistory[contextKey].Add(ChatMessage.FromUser(question));
    }

    public void AddAssistantResponseToHistory(string contextKey, string response)
    {
        // Ensure conversation history exists for this context
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            _logger.LogWarning("Conversation history not found for context {ContextKey}, initializing empty history", contextKey);
            _conversationHistory[contextKey] = new List<ChatMessage>();
        }

        _conversationHistory[contextKey].Add(ChatMessage.FromAssistant(response));
    }

    public void TrimConversationHistoryIfNeeded(string contextKey)
    {
        if (_conversationHistory[contextKey].Count > MaxConversationHistoryRegular)
        {
            _conversationHistory[contextKey] = _conversationHistory[contextKey].Take(ConversationStartMessages)
                .Concat(_conversationHistory[contextKey].Skip(_conversationHistory[contextKey].Count - ConversationContextLimit))
                .ToList();

            _logger.LogInformation("🔎 TRACE: Trimmed conversation history to prevent token overflow");
        }
    }

    public void TrimMultiChildConversationIfNeeded(string contextKey)
    {
        if (!_conversationHistory.ContainsKey(contextKey))
        {
            _conversationHistory[contextKey] = new List<ChatMessage>();
            return;
        }

        if (_conversationHistory[contextKey].Count > MaxConversationHistoryWeekLetter)
        {
            _conversationHistory[contextKey] = _conversationHistory[contextKey].Skip(ConversationTrimAmount).ToList();
            _logger.LogInformation("🔎 TRACE: Trimmed multi-child conversation history to prevent token overflow");
        }
    }

    public List<ChatMessage> GetConversationHistory(string contextKey)
    {
        return _conversationHistory.GetValueOrDefault(contextKey, new List<ChatMessage>());
    }

    public void ClearConversationHistory(string? contextKey = null)
    {
        if (string.IsNullOrEmpty(contextKey))
        {
            _conversationHistory.Clear();
            _currentChildContext.Clear();
            _logger.LogInformation("Cleared all conversation history and child contexts");
        }
        else if (_conversationHistory.ContainsKey(contextKey))
        {
            _conversationHistory.TryRemove(contextKey, out _);
            _currentChildContext.TryRemove(contextKey, out _);
            _logger.LogInformation("Cleared conversation history and child context for {ContextKey}", contextKey);
        }
    }

    private void InitializeNewConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        _conversationHistory[contextKey] = new List<ChatMessage>
        {
            _promptBuilder.CreateSystemInstructionsMessage(childName, chatInterface),
            _promptBuilder.CreateWeekLetterContentMessage(childName, weekLetterContent)
        };
    }

    private void UpdateExistingConversationHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        if (ShouldResetHistoryForNewChild(contextKey, childName))
        {
            InitializeNewConversationHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
        else
        {
            RefreshWeekLetterContentInHistory(contextKey, childName, weekLetterContent, chatInterface);
        }
    }

    private bool ShouldResetHistoryForNewChild(string contextKey, string childName)
    {
        return _currentChildContext.TryGetValue(contextKey, out var previousChildName) &&
               !string.Equals(previousChildName, childName, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshWeekLetterContentInHistory(string contextKey, string childName, string weekLetterContent, ChatInterface chatInterface)
    {
        _logger.LogInformation("🔎 TRACE: Updating existing conversation context for {ContextKey}", contextKey);

        int contentIndex = FindWeekLetterContentIndex(contextKey);

        if (contentIndex >= 0)
        {
            UpdateExistingWeekLetterContent(contextKey, childName, weekLetterContent, contentIndex, chatInterface);
        }
        else
        {
            InsertWeekLetterContent(contextKey, childName, weekLetterContent);
        }
    }

    private int FindWeekLetterContentIndex(string contextKey)
    {
        for (int i = 0; i < _conversationHistory[contextKey].Count; i++)
        {
            var message = _conversationHistory[contextKey][i];
            if (message?.Role == "system" &&
                message.Content?.StartsWith("Here's the weekly letter content") == true)
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateExistingWeekLetterContent(string contextKey, string childName, string weekLetterContent, int contentIndex, ChatInterface chatInterface)
    {
        _logger.LogInformation("🔎 TRACE: Found existing week letter content at index {Index}, updating", contentIndex);
        _conversationHistory[contextKey][contentIndex] = _promptBuilder.CreateWeekLetterContentMessage(childName, weekLetterContent);
        _logger.LogInformation("🔎 TRACE: Updated existing week letter content in context: {Length} characters", weekLetterContent.Length);

        if (contentIndex > 0)
        {
            _conversationHistory[contextKey][0] = _promptBuilder.CreateSystemInstructionsMessage(childName, chatInterface);
            _logger.LogInformation("🔎 TRACE: Updated system instructions in context");
        }
    }

    private void InsertWeekLetterContent(string contextKey, string childName, string weekLetterContent)
    {
        _logger.LogInformation("🔎 TRACE: No existing week letter content found, inserting after first system message");
        _conversationHistory[contextKey].Insert(1, _promptBuilder.CreateWeekLetterContentMessage(childName, weekLetterContent));
        _logger.LogInformation("🔎 TRACE: Added week letter content to existing context: {Length} characters", weekLetterContent.Length);
    }
}
