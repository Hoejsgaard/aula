using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Aula;

public class TelegramInteractiveBot
{
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly ITelegramBotClient _telegramClient;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly HashSet<string> _postedWeekLetterHashes = new HashSet<string>();
    private readonly HashSet<string> _englishWords = new HashSet<string> { "what", "when", "how", "is", "does", "do", "can", "will", "has", "have", "had", "show", "get", "tell", "please", "thanks", "thank", "you", "hello", "hi" };
    private readonly HashSet<string> _danishWords = new HashSet<string> { "hvad", "hvornår", "hvordan", "er", "gør", "kan", "vil", "har", "havde", "vis", "få", "fortæl", "venligst", "tak", "du", "dig", "hej", "hallo", "goddag" };
    
    // Conversation context tracking
    private class ConversationContext
    {
        public string? LastChildName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public bool IsStillValid => (DateTime.Now - Timestamp).TotalMinutes < 10; // Context expires after 10 minutes
        
        public override string ToString()
        {
            return $"Child: {LastChildName ?? "none"}, Age: {(DateTime.Now - Timestamp).TotalMinutes:F1} minutes";
        }
    }
    
    // Track conversation contexts by chat ID
    private readonly Dictionary<long, ConversationContext> _conversationContexts = new Dictionary<long, ConversationContext>();
    
    private void UpdateConversationContext(long chatId, string? childName)
    {
        _conversationContexts[chatId] = new ConversationContext
        {
            LastChildName = childName,
            Timestamp = DateTime.Now
        };
        
        _logger.LogInformation("Updated conversation context for chat {ChatId}: {Context}", chatId, _conversationContexts[chatId]);
    }

    public TelegramInteractiveBot(
        IAgentService agentService,
        Config config,
        ILoggerFactory loggerFactory)
    {
        _agentService = agentService;
        _config = config;
        _logger = loggerFactory.CreateLogger<TelegramInteractiveBot>();
        
        if (_config.Telegram.Enabled && !string.IsNullOrEmpty(_config.Telegram.Token))
        {
            _telegramClient = new TelegramBotClient(_config.Telegram.Token);
        }
        else
        {
            throw new InvalidOperationException("Telegram bot is not enabled or token is missing");
        }
        
        _childrenByName = _config.Children.ToDictionary(
            c => c.FirstName.ToLowerInvariant(),
            c => c);
    }

    public async Task Start()
    {
        if (!_config.Telegram.Enabled)
        {
            _logger.LogError("Cannot start Telegram bot: Telegram integration is not enabled");
            return;
        }

        if (string.IsNullOrEmpty(_config.Telegram.Token))
        {
            _logger.LogError("Cannot start Telegram bot: Token is missing");
            return;
        }

        _logger.LogInformation("Starting Telegram interactive bot");
        
        // Start receiving updates
        using var cts = new CancellationTokenSource();
        
        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };
        
        // Register handlers for updates
        _telegramClient.StartReceiving(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            receiverOptions,
            cts.Token
        );
        
        // Build a list of available children (first names only)
        string childrenList = string.Join(" og ", _childrenByName.Values.Select(c => c.FirstName.Split(' ')[0]));
        
        // Get the current week number
        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);
        
        // Send welcome message in Danish with children info to the configured channel
        if (!string.IsNullOrEmpty(_config.Telegram.ChannelId))
        {
            await SendMessageToChannel($"Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}");
        }
        
        _logger.LogInformation("Telegram interactive bot started");
        
        // Keep the application running
        await Task.Delay(Timeout.Infinite, cts.Token);
    }

    public void Stop()
    {
        _logger.LogInformation("Telegram interactive bot stopped");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received update: {UpdateType}", update.Type);
        
        // Only process message updates
        if (update.Message is not { } message)
        {
            _logger.LogWarning("Update does not contain a message");
            return;
        }
        
        // Only process text messages
        if (message.Text is not { } messageText)
        {
            _logger.LogWarning("Message does not contain text");
            return;
        }
        
        var chatId = message.Chat.Id;
        
        _logger.LogInformation("Received message from {ChatId}: {Message}", chatId, messageText);
        _logger.LogInformation("Message from user: {FirstName} {LastName} (@{Username})", 
            message.From?.FirstName ?? "Unknown", 
            message.From?.LastName ?? "", 
            message.From?.Username ?? "Unknown");
        _logger.LogInformation("Chat type: {ChatType}, Title: {Title}", 
            message.Chat.Type, 
            message.Chat.Title ?? "N/A");
        
        // Process the message
        await ProcessMessage(chatId, messageText);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(errorMessage);
        return Task.CompletedTask;
    }
    
    private async Task ProcessMessage(long chatId, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Empty message received, skipping processing");
            return;
        }

        _logger.LogInformation("Processing message from {ChatId}: {Text}", chatId, text);
        
        try
        {
            // Detect language (Danish or English)
            bool isEnglish = DetectLanguage(text) == "en";
            _logger.LogInformation("Detected language: {Language}", isEnglish ? "English" : "Danish");
            
            // Handle Aula questions
            _logger.LogInformation("Forwarding to HandleAulaQuestion");
            await HandleAulaQuestion(chatId, text, isEnglish);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", text);
        }
    }

    private bool IsFollowUpQuestion(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        text = text.ToLowerInvariant();
        
        // Check for explicit follow-up phrases
        bool hasFollowUpPhrase = text.Contains("hvad med") || 
                               text.Contains("what about") || 
                               text.Contains("how about") || 
                               text.Contains("hvordan med") ||
                               text.StartsWith("og ") || 
                               text.StartsWith("and ") ||
                               text.Contains("og hvad") ||
                               text.Contains("and what") ||
                               text.Contains("også") ||
                               text.Contains("also") ||
                               text.Contains("likewise") ||
                               text == "og?" ||
                               text == "and?";
        
        // Check if this is a short message
        bool isShortMessage = text.Length < 15;
        
        // Check if the message contains a child name
        bool hasChildName = false;
        foreach (var childName in _childrenByName.Keys)
        {
            if (text.Contains(childName.ToLowerInvariant()))
            {
                hasChildName = true;
                break;
            }
        }
        
        // Also check for first names
        if (!hasChildName)
        {
            foreach (var child in _childrenByName.Values)
            {
                string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
                if (text.Contains(firstName.ToLowerInvariant()))
                {
                    hasChildName = true;
                    break;
                }
            }
        }
        
        // Check if the message contains time references
        bool hasTimeReference = text.Contains("today") || text.Contains("tomorrow") || 
                              text.Contains("i dag") || text.Contains("i morgen");
                              
        // Special case for very short messages that are likely follow-ups
        if (isShortMessage && (text.Contains("?") || text == "ok" || text == "okay"))
        {
            _logger.LogInformation("Detected likely follow-up based on short message: {Text}", text);
            return true;
        }
        
        bool result = hasFollowUpPhrase || (isShortMessage && hasChildName && !hasTimeReference);
        
        if (result)
        {
            _logger.LogInformation("Detected follow-up question: {Text}", text);
        }
        
        return result;
    }

    private string DetectLanguage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "da"; // Default to Danish if no text
        }
        
        string lowerText = text.ToLowerInvariant();
        
        // Count English and Danish words
        int englishWordCount = 0;
        int danishWordCount = 0;
        
        foreach (var word in lowerText.Split(' ', ',', '.', '!', '?', ':', ';', '-', '(', ')', '[', ']', '{', '}'))
        {
            string cleanWord = word.Trim();
            if (string.IsNullOrEmpty(cleanWord))
            {
                continue;
            }
            
            if (_englishWords.Contains(cleanWord))
            {
                englishWordCount++;
            }
            
            if (_danishWords.Contains(cleanWord))
            {
                danishWordCount++;
            }
        }
        
        _logger.LogInformation("Language detection - English words: {EnglishCount}, Danish words: {DanishCount}", 
            englishWordCount, danishWordCount);
        
        // If we have more Danish words, or equal but the text contains Danish-specific characters, use Danish
        if (danishWordCount > englishWordCount || 
            (danishWordCount == englishWordCount && 
             (lowerText.Contains('æ') || lowerText.Contains('ø') || lowerText.Contains('å'))))
        {
            return "da";
        }
        
        return "en";
    }

    private string? ExtractChildName(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        
        text = text.ToLowerInvariant();
        
        // Check for full child names
        foreach (var childName in _childrenByName.Keys)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
            {
                _logger.LogInformation("Found child name: {ChildName}", childName);
                return childName;
            }
        }
        
        // Check for first names
        foreach (var child in _childrenByName.Values)
        {
            string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
            {
                string matchedKey = _childrenByName.Keys.FirstOrDefault(k => 
                    k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase)) ?? "";
                _logger.LogInformation("Found first name: {FirstName} -> {ChildName}", firstName, matchedKey);
                return matchedKey;
            }
        }
        
        // Check for follow-up phrases
        string[] followUpPhrases = { "hvad med", "what about", "how about", "hvordan med", "og hvad", "and what" };
        foreach (var phrase in followUpPhrases)
        {
            int index = text.IndexOf(phrase);
            if (index >= 0)
            {
                string afterPhrase = text.Substring(index + phrase.Length).Trim();
                
                // Check for full names after the phrase
                foreach (var childName in _childrenByName.Keys)
                {
                    if (Regex.IsMatch(afterPhrase, $@"\b{Regex.Escape(childName)}\b", RegexOptions.IgnoreCase))
                    {
                        return childName;
                    }
                }
                
                // Check for first names after the phrase
                foreach (var child in _childrenByName.Values)
                {
                    string firstName = child.FirstName.Split(' ')[0].ToLowerInvariant();
                    if (Regex.IsMatch(afterPhrase, $@"\b{Regex.Escape(firstName)}\b", RegexOptions.IgnoreCase))
                    {
                        return _childrenByName.Keys.FirstOrDefault(k => 
                            k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }
        }
        
        return null;
    }

    private async Task HandleAulaQuestion(long chatId, string text, bool isEnglish)
    {
        try
        {
            _logger.LogInformation("HandleAulaQuestion called with text: {Text}", text);
            
            // Extract child name from the question
            string? childName = ExtractChildName(text);
            _logger.LogInformation("Extracted child name: {ChildName}", childName ?? "null");
            
            // If no child name found and we have context, use the last child
            if (childName == null && 
                _conversationContexts.TryGetValue(chatId, out var context) && 
                context.IsStillValid && 
                context.LastChildName != null)
            {
                childName = context.LastChildName;
                _logger.LogInformation("Using child from context: {ChildName}", childName);
            }
            
            // If we have a child name, handle it as a question about that child
            if (childName != null)
            {
                // Find the child by name using the AgentService for consistent lookup
                var child = await _agentService.GetChildByNameAsync(childName);
                if (child == null)
                {
                    _logger.LogWarning("Child not found: {ChildName}", childName);
                    var allChildren = await _agentService.GetAllChildrenAsync();
                    var childNames = string.Join(", ", allChildren.Select(c => c.FirstName));
                    string notFoundMessage = isEnglish
                        ? $"I don't know a child named {childName}. Available children are: {childNames}"
                        : $"Jeg kender ikke et barn ved navn {childName}. Tilgængelige børn er: {childNames}";
                    
                    await SendMessage(chatId, notFoundMessage);
                    return;
                }

                _logger.LogInformation("Found child: {ChildName}", childName);
                
                // Get the week letter for the child
                _logger.LogInformation("Getting week letter for {ChildName}", childName);
                var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                
                if (weekLetter == null)
                {
                    _logger.LogWarning("Week letter is null for {ChildName}", childName);
                    string noLetterMessage = isEnglish
                        ? $"I don't have a week letter for {childName} yet."
                        : $"Jeg har ikke et ugebrev for {childName} endnu.";
                    
                    await SendMessage(chatId, noLetterMessage);
                    return;
                }

                // Log the week letter content to verify it's being extracted
                var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
                _logger.LogInformation("Week letter content for {ChildName}: {Length} characters", childName, weekLetterContent.Length);
                
                // Use a chat-based context key that allows child switching
                string contextKey = $"telegram-{chatId}";
                _logger.LogInformation("Using context key: {ContextKey}", contextKey);
                
                // Enhance the question with day of week context if it contains relative time references
                string enhancedQuestion = text.Trim();
                if (ContainsRelativeTimeReference(enhancedQuestion))
                {
                    string dayContext = $"Today is {DateTime.Now.DayOfWeek}. ";
                    enhancedQuestion = dayContext + enhancedQuestion;
                    _logger.LogInformation("Enhanced question with day context: {Question}", enhancedQuestion);
                }
                
                // Add language context to ensure the LLM responds in the correct language
                string language = isEnglish ? "English" : "Danish";
                enhancedQuestion = $"[Please respond in {language}] " + enhancedQuestion;
                _logger.LogInformation("Added language context: {Language}", language);
                
                // Let the LLM handle the question with context
                string answer = await _agentService.AskQuestionAboutWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), enhancedQuestion, contextKey, ChatInterface.Telegram);
                _logger.LogInformation("Got answer: {Length} characters", answer.Length);
                
                await SendMessage(chatId, answer);
                
                // Update conversation context with the child name
                UpdateConversationContext(chatId, child.FirstName);
                return;
            }
            
            // Check if this is a question about all children
            if (text.ToLowerInvariant().Contains("alle børn") || 
                text.ToLowerInvariant().Contains("all children") ||
                text.ToLowerInvariant().Contains("alle børnene") || 
                text.ToLowerInvariant().Contains("all the children"))
            {
                await HandleAllChildrenQuestion(chatId, text, isEnglish);
                return;
            }
            
            // If we get here, it's a generic question with no specific child
            if (isEnglish)
            {
                await SendMessage(chatId, "I'm not sure how to help with that. You can ask me about a child's activities like 'What is Søren doing tomorrow?' or 'Does Hans have homework for Tuesday?'");
            }
            else
            {
                await SendMessage(chatId, "Jeg er ikke sikker på, hvordan jeg kan hjælpe med det. Du kan spørge mig om et barns aktiviteter som 'Hvad skal Søren lave i morgen?' eller 'Har Hans lektier for til tirsdag?'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Aula question");
            string errorMessage = isEnglish
                ? "Sorry, I couldn't process your question at the moment."
                : "Beklager, jeg kunne ikke behandle dit spørgsmål i øjeblikket.";
            
            await SendMessage(chatId, errorMessage);
        }
    }

    private async Task HandleAllChildrenQuestion(long chatId, string text, bool isEnglish)
    {
        try
        {
            // Get all children using the AgentService
            var allChildren = await _agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                string noChildrenMessage = isEnglish
                    ? "I don't have any children configured."
                    : "Jeg har ingen børn konfigureret.";
                
                await SendMessage(chatId, noChildrenMessage);
                return;
            }

            // Create a response builder
            var responseBuilder = new StringBuilder();
            
            // Add a header
            responseBuilder.AppendLine(isEnglish 
                ? "Here's information for all children:"
                : "Her er information for alle børn:");
            
            // User's original question
            string userQuestion = text.Trim();
            
            foreach (var child in allChildren)
            {

                // Get the week letter for the child
                var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                if (weekLetter == null)
                {
                    string noLetterMessage = isEnglish
                        ? $"- {child.FirstName}: No week letter available."
                        : $"- {child.FirstName}: Intet ugebrev tilgængeligt.";
                    
                    responseBuilder.AppendLine(noLetterMessage);
                    continue;
                }

                // Create a context key for all-children query  
                string contextKey = $"telegram-{chatId}-all-{child.FirstName.ToLowerInvariant()}";
                
                // Formulate a brief question for this child
                string question = $"{userQuestion} (About {child.FirstName}. Give a brief answer.)";
                
                // Add language context
                string language = isEnglish ? "English" : "Danish";
                question = $"[Please respond in {language}] " + question;
                
                // Ask OpenAI about the child's activities
                string answer = await _agentService.AskQuestionAboutWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), question, contextKey, ChatInterface.Telegram);
                
                // Add to the response
                responseBuilder.AppendLine($"- {child.FirstName}: {answer}");
            }
            
            // Send the combined response
            await SendMessage(chatId, responseBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling question about all children");
            string errorMessage = isEnglish
                ? "Sorry, I couldn't process information about all children at the moment."
                : "Beklager, jeg kunne ikke behandle information om alle børn i øjeblikket.";
            
            await SendMessage(chatId, errorMessage);
        }
    }

    private async Task SendMessage(long chatId, string text)
    {
        try
        {
            _logger.LogInformation("Sending message to chat {ChatId}: {TextLength} characters", chatId, text.Length);
            
            await _telegramClient.SendTextMessageAsync(
                chatId: new ChatId(chatId),
                text: text,
                parseMode: ParseMode.Html
            );
            
            _logger.LogInformation("Message sent successfully to chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
        }
    }

    private async Task<bool> SendMessageToChannel(string message)
    {
        if (!_config.Telegram.Enabled || string.IsNullOrEmpty(_config.Telegram.ChannelId))
        {
            _logger.LogWarning("Telegram integration is disabled or channel ID is missing. Message not sent.");
            return false;
        }

        try
        {
            await _telegramClient.SendTextMessageAsync(
                chatId: new ChatId(_config.Telegram.ChannelId),
                text: message,
                parseMode: ParseMode.Html
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to channel");
        }

        return false;
    }

    public async Task PostWeekLetter(string childName, JObject weekLetter)
    {
        if (!_config.Telegram.Enabled || string.IsNullOrEmpty(_config.Telegram.ChannelId))
        {
            _logger.LogWarning("Telegram integration is disabled or channel ID is missing. Week letter not posted.");
            return;
        }

        try
        {
            // Find the child
            var child = _childrenByName.Values.FirstOrDefault(c => 
                c.FirstName.Equals(childName, StringComparison.OrdinalIgnoreCase));
            
            if (child == null)
            {
                _logger.LogWarning("Child not found for week letter posting: {ChildName}", childName);
                return;
            }
            
            // Create a hash of the week letter to avoid duplicates
            var weekLetterContent = weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "";
            var hash = ComputeHash(weekLetterContent);
            
            // Check if we've already posted this week letter
            if (_postedWeekLetterHashes.Contains(hash))
            {
                _logger.LogInformation("Week letter for {ChildName} already posted (hash: {Hash})", childName, hash);
                return;
            }
            
            // Post the week letter using the existing TelegramClient functionality
            var telegramClient = new TelegramClient(_config);
            bool success = await telegramClient.PostWeekLetter(_config.Telegram.ChannelId, weekLetter, child);
            
            if (success)
            {
                // Add the hash to avoid duplicates
                _postedWeekLetterHashes.Add(hash);
                _logger.LogInformation("Week letter for {ChildName} posted successfully", childName);
            }
            else
            {
                _logger.LogWarning("Failed to post week letter for {ChildName}", childName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting week letter for {ChildName}", childName);
        }
    }

    private string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private bool ContainsRelativeTimeReference(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        text = text.ToLowerInvariant();
        
        // Check for common time references in Danish and English
        return text.Contains("i dag") || text.Contains("i morgen") || text.Contains("i går") ||
               text.Contains("today") || text.Contains("tomorrow") || text.Contains("yesterday") ||
               text.Contains("denne uge") || text.Contains("this week") ||
               text.Contains("næste uge") || text.Contains("next week") ||
               text.Contains("mandag") || text.Contains("monday") ||
               text.Contains("tirsdag") || text.Contains("tuesday") ||
               text.Contains("onsdag") || text.Contains("wednesday") ||
               text.Contains("torsdag") || text.Contains("thursday") ||
               text.Contains("fredag") || text.Contains("friday") ||
               text.Contains("lørdag") || text.Contains("saturday") ||
               text.Contains("søndag") || text.Contains("sunday");
    }
} 