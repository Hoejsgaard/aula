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
using Aula.Integration;
using Aula.Tools;
using Aula.Channels;
using Aula.Configuration;
using Aula.Services;
using Aula.Utilities;

namespace Aula.Bots;

public class TelegramInteractiveBot
{
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly ITelegramBotClient _telegramClient;
    private readonly ISupabaseService _supabaseService;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly HashSet<string> _postedWeekLetterHashes = new HashSet<string>();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ReminderCommandHandler _reminderHandler;
    // Language detection arrays removed - GPT handles language detection naturally

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
        ILoggerFactory loggerFactory,
        ISupabaseService supabaseService)
    {
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<TelegramInteractiveBot>();
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

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

        _reminderHandler = new ReminderCommandHandler(_logger, _supabaseService, _childrenByName);
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

        // Create cancellation token source for graceful shutdown
        _cancellationTokenSource = new CancellationTokenSource();

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
            _cancellationTokenSource.Token
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

        // Keep the bot running until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telegram bot shutdown requested");
        }
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping Telegram interactive bot");

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

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
            // Check for help command first
            if (await TryHandleHelpCommand(chatId, text))
            {
                return;
            }

            // Use the new tool-based processing that can handle both tools and regular questions
            string contextKey = $"telegram-{chatId}";
            string response = await _agentService.ProcessQueryWithToolsAsync(text, contextKey, ChatInterface.Telegram);
            await SendMessageInternal(chatId, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", text);
        }
    }

    private async Task<bool> TryHandleHelpCommand(long chatId, string text)
    {
        var normalizedText = text.Trim().ToLowerInvariant();

        // English help commands
        if (normalizedText == "help" || normalizedText == "--help" || normalizedText == "?" ||
            normalizedText == "commands" || normalizedText == "/help" || normalizedText == "/start")
        {
            await SendMessageInternal(chatId, GetEnglishHelpMessage());
            return true;
        }

        // Danish help commands  
        if (normalizedText == "hjælp" || normalizedText == "kommandoer" || normalizedText == "/hjælp")
        {
            await SendMessageInternal(chatId, GetDanishHelpMessage());
            return true;
        }

        return false;
    }

    private string GetEnglishHelpMessage()
    {
        return """
📚 <b>AulaBot Commands &amp; Usage</b>

<b>🤖 Interactive Questions:</b>
Ask me anything about your children's school activities in natural language:
• "What does Søren have today?"
• "Does Hans have homework tomorrow?"
• "What activities are planned this week?"

<b>⏰ Reminder Commands:</b>
• Send "remind me tomorrow at 8:00 that Hans has Haver til maver"
• Send "remind me 25/12 at 7:30 that Christmas breakfast"
• Send "list reminders" - Show all reminders
• Send "delete reminder 1" - Delete reminder with ID 1

<b>📅 Automatic Features:</b>
• Weekly letters posted every Sunday at 16:00
• Morning reminders sent when scheduled
• Retry logic for missing content

<b>💬 Language Support:</b>
Ask questions in English or Danish - I'll respond in the same language!

<b>ℹ️ Tips:</b>
• Use "today", "tomorrow", or specific dates
• Mention child names for targeted questions
• Follow-up questions maintain context for 10 minutes
""";
    }

    private string GetDanishHelpMessage()
    {
        return """
📚 <b>AulaBot Kommandoer &amp; Brug</b>

<b>🤖 Interaktive Spørgsmål:</b>
Spørg mig om hvad som helst vedrørende dine børns skoleaktiviteter på naturligt sprog:
• "Hvad skal Søren i dag?"
• "Har Hans lektier i morgen?"
• "Hvilke aktiviteter er planlagt denne uge?"

<b>⏰ Påmindelseskommandoer:</b>
• Send "husk mig i morgen kl 8:00 at Hans har Haver til maver"
• Send "husk mig 25/12 kl 7:30 at julefrokost"
• Send "vis påmindelser" - Vis alle påmindelser
• Send "slet påmindelse 1" - Slet påmindelse med ID 1

<b>📅 Automatiske Funktioner:</b>
• Ugebreve postes hver søndag kl. 16:00
• Morgenpåmindelser sendes når planlagt
• Genforøgelseslogik for manglende indhold

<b>💬 Sprogunderstøttelse:</b>
Stil spørgsmål på engelsk eller dansk - jeg svarer på samme sprog!

<b>ℹ️ Tips:</b>
• Brug "i dag", "i morgen", eller specifikke datoer
• Nævn børnenes navne for målrettede spørgsmål
• Opfølgningsspørgsmål bevarer kontekst i 10 minutter
""";
    }

    private async Task<bool> TryHandleReminderCommand(long chatId, string text, bool isEnglish)
    {
        var (handled, response) = await _reminderHandler.TryHandleReminderCommand(text, isEnglish);

        if (handled && response != null)
        {
            await SendMessageInternal(chatId, response);
        }

        return handled;
    }

    private bool IsFollowUpQuestion(string text)
    {
        return FollowUpQuestionDetector.IsFollowUpQuestion(text, _childrenByName.Values.ToList(), _logger);
    }

    // Dead methods removed:
    // - DetectLanguage() - GPT handles language detection naturally
    // - ExtractChildName() - not used anywhere
    // - HandleAulaQuestion() - not used anywhere

    // HandleAulaQuestion method removed - dead code


    public async Task SendMessage(long chatId, string text)
    {
        await SendMessageInternal(chatId, text);
    }

    public async Task SendMessage(string chatId, string text)
    {
        await SendMessageInternal(chatId, text);
    }

    private async Task SendMessageInternal(string chatId, string text)
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

    private async Task SendMessageInternal(long chatId, string text)
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

    private string GetDanishDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "mandag",
            DayOfWeek.Tuesday => "tirsdag",
            DayOfWeek.Wednesday => "onsdag",
            DayOfWeek.Thursday => "torsdag",
            DayOfWeek.Friday => "fredag",
            DayOfWeek.Saturday => "lørdag",
            DayOfWeek.Sunday => "søndag",
            _ => "ukendt dag"
        };
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