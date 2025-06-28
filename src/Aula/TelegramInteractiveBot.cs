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
    private readonly ISupabaseService _supabaseService;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly HashSet<string> _postedWeekLetterHashes = new HashSet<string>();
    private CancellationTokenSource? _cancellationTokenSource;
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
            // Detect language (Danish or English)
            bool isEnglish = DetectLanguage(text) == "en";
            _logger.LogInformation("Detected language: {Language}", isEnglish ? "English" : "Danish");

            // Check for help command first
            if (await TryHandleHelpCommand(chatId, text, isEnglish))
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

    private async Task<bool> TryHandleHelpCommand(long chatId, string text, bool isEnglish)
    {
        var helpPatterns = new[]
        {
            @"^(help|--help|\?|commands|/help|/start)$",
            @"^(hjælp|kommandoer|/hjælp)$"
        };

        foreach (var pattern in helpPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                string helpMessage = isEnglish ? GetEnglishHelpMessage() : GetDanishHelpMessage();
                await SendMessageInternal(chatId, helpMessage);
                return true;
            }
        }

        return false;
    }

    private string GetEnglishHelpMessage()
    {
        return """
📚 <b>AulaBot Commands &amp; Usage</b>

<b>🤖 Interactive Questions:</b>
Ask me anything about your children's school activities in natural language:
• "What does TestChild2 have today?"
• "Does TestChild1 have homework tomorrow?"
• "What activities are planned this week?"

<b>⏰ Reminder Commands:</b>
• Send "remind me tomorrow at 8:00 that TestChild1 has Haver til maver"
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
• "Hvad skal TestChild2 i dag?"
• "Har TestChild1 lektier i morgen?"
• "Hvilke aktiviteter er planlagt denne uge?"

<b>⏰ Påmindelseskommandoer:</b>
• Send "husk mig i morgen kl 8:00 at TestChild1 har Haver til maver"
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
        text = text.Trim();

        // Check for various reminder command patterns
        if (await TryHandleAddReminder(chatId, text, isEnglish)) return true;
        if (await TryHandleListReminders(chatId, text, isEnglish)) return true;
        if (await TryHandleDeleteReminder(chatId, text, isEnglish)) return true;

        return false;
    }

    private async Task<bool> TryHandleAddReminder(long chatId, string text, bool isEnglish)
    {
        // Patterns: "remind me tomorrow at 8:00 that TestChild1 has Haver til maver"
        //           "husk mig i morgen kl 8:00 at TestChild1 har Haver til maver"

        var reminderPatterns = new[]
        {
            @"remind me (tomorrow|today|\d{4}-\d{2}-\d{2}|\d{1,2}\/\d{1,2}) at (\d{1,2}:\d{2}) that (.+)",
            @"husk mig (i morgen|i dag|\d{4}-\d{2}-\d{2}|\d{1,2}\/\d{1,2}) kl (\d{1,2}:\d{2}) at (.+)"
        };

        foreach (var pattern in reminderPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    var dateStr = match.Groups[1].Value.ToLowerInvariant();
                    var timeStr = match.Groups[2].Value;
                    var reminderText = match.Groups[3].Value;

                    // Parse date
                    DateOnly date;
                    if (dateStr == "tomorrow" || dateStr == "i morgen")
                    {
                        date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
                    }
                    else if (dateStr == "today" || dateStr == "i dag")
                    {
                        date = DateOnly.FromDateTime(DateTime.Today);
                    }
                    else if (DateOnly.TryParse(dateStr, out var parsedDate))
                    {
                        date = parsedDate;
                    }
                    else
                    {
                        // Try parsing DD/MM format
                        var dateParts = dateStr.Split('/');
                        if (dateParts.Length == 2 &&
                            int.TryParse(dateParts[0], out var day) &&
                            int.TryParse(dateParts[1], out var month))
                        {
                            var year = DateTime.Now.Year;
                            if (month < DateTime.Now.Month || (month == DateTime.Now.Month && day < DateTime.Now.Day))
                            {
                                year++; // Next year if date has passed
                            }
                            date = new DateOnly(year, month, day);
                        }
                        else
                        {
                            throw new FormatException("Invalid date format");
                        }
                    }

                    // Parse time
                    if (!TimeOnly.TryParse(timeStr, out var time))
                    {
                        throw new FormatException("Invalid time format");
                    }

                    // Extract child name if mentioned
                    string? childName = null;
                    foreach (var child in _childrenByName.Values)
                    {
                        string firstName = child.FirstName.Split(' ')[0];
                        if (reminderText.Contains(firstName, StringComparison.OrdinalIgnoreCase))
                        {
                            childName = child.FirstName;
                            break;
                        }
                    }

                    // Add reminder to database
                    var reminderId = await _supabaseService.AddReminderAsync(reminderText, date, time, childName);

                    string successMessage = isEnglish
                        ? $"✅ Reminder added (ID: {reminderId}) for {date:dd/MM} at {time:HH:mm}: {reminderText}"
                        : $"✅ Påmindelse tilføjet (ID: {reminderId}) for {date:dd/MM} kl {time:HH:mm}: {reminderText}";

                    await SendMessageInternal(chatId, successMessage);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding reminder");

                    string errorMessage = isEnglish
                        ? "❌ Failed to add reminder. Please check the date and time format."
                        : "❌ Kunne ikke tilføje påmindelse. Tjek venligst dato- og tidsformat.";

                    await SendMessageInternal(chatId, errorMessage);
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> TryHandleListReminders(long chatId, string text, bool isEnglish)
    {
        var listPatterns = new[]
        {
            @"^(list reminders|show reminders)$",
            @"^(vis påmindelser|liste påmindelser)$"
        };

        foreach (var pattern in listPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                try
                {
                    var reminders = await _supabaseService.GetAllRemindersAsync();

                    if (!reminders.Any())
                    {
                        string noRemindersMessage = isEnglish
                            ? "📝 No reminders found."
                            : "📝 Ingen påmindelser fundet.";

                        await SendMessageInternal(chatId, noRemindersMessage);
                        return true;
                    }

                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine(isEnglish ? "📝 <b>Your Reminders:</b>" : "📝 <b>Dine Påmindelser:</b>");
                    messageBuilder.AppendLine();

                    foreach (var reminder in reminders.OrderBy(r => r.RemindDate).ThenBy(r => r.RemindTime))
                    {
                        string status = reminder.IsSent ?
                            (isEnglish ? "✅ Sent" : "✅ Sendt") :
                            (isEnglish ? "⏳ Pending" : "⏳ Afventer");

                        string childInfo = !string.IsNullOrEmpty(reminder.ChildName) ? $" ({reminder.ChildName})" : "";

                        messageBuilder.AppendLine($"<b>ID {reminder.Id}:</b> {reminder.Text}{childInfo}");
                        messageBuilder.AppendLine($"📅 {reminder.RemindDate:dd/MM/yyyy} ⏰ {reminder.RemindTime:HH:mm} - {status}");
                        messageBuilder.AppendLine();
                    }

                    await SendMessageInternal(chatId, messageBuilder.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing reminders");

                    string errorMessage = isEnglish
                        ? "❌ Failed to retrieve reminders."
                        : "❌ Kunne ikke hente påmindelser.";

                    await SendMessageInternal(chatId, errorMessage);
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> TryHandleDeleteReminder(long chatId, string text, bool isEnglish)
    {
        var deletePatterns = new[]
        {
            @"^delete reminder (\d+)$",
            @"^slet påmindelse (\d+)$"
        };

        foreach (var pattern in deletePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                try
                {
                    var reminderId = int.Parse(match.Groups[1].Value);

                    await _supabaseService.DeleteReminderAsync(reminderId);

                    string successMessage = isEnglish
                        ? $"✅ Reminder {reminderId} deleted."
                        : $"✅ Påmindelse {reminderId} slettet.";

                    await SendMessageInternal(chatId, successMessage);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting reminder");

                    string errorMessage = isEnglish
                        ? "❌ Failed to delete reminder. Please check the ID."
                        : "❌ Kunne ikke slette påmindelse. Tjek venligst ID'et.";

                    await SendMessageInternal(chatId, errorMessage);
                    return true;
                }
            }
        }

        return false;
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

            // Get all children and their week letters
            var allChildren = await _agentService.GetAllChildrenAsync();
            if (!allChildren.Any())
            {
                string noChildrenMessage = isEnglish
                    ? "I don't have any children configured."
                    : "Jeg har ingen børn konfigureret.";

                await SendMessageInternal(chatId, noChildrenMessage);
                return;
            }

            // Collect week letters for all children
            var childrenWeekLetters = new Dictionary<string, JObject>();
            foreach (var child in allChildren)
            {
                var weekLetter = await _agentService.GetWeekLetterAsync(child, DateOnly.FromDateTime(DateTime.Today), true);
                if (weekLetter != null)
                {
                    childrenWeekLetters[child.FirstName] = weekLetter;
                }
            }

            if (!childrenWeekLetters.Any())
            {
                string noLettersMessage = isEnglish
                    ? "I don't have any week letters available at the moment."
                    : "Jeg har ingen ugebreve tilgængelige i øjeblikket.";

                await SendMessageInternal(chatId, noLettersMessage);
                return;
            }

            // Use a single context key for the chat
            string contextKey = $"telegram-{chatId}";

            // Add day context if needed
            string enhancedQuestion = text;
            if (text.ToLowerInvariant().Contains("i dag") || text.ToLowerInvariant().Contains("today"))
            {
                string dayOfWeek = isEnglish ? DateTime.Now.DayOfWeek.ToString() : GetDanishDayName(DateTime.Now.DayOfWeek);
                enhancedQuestion = $"{text} (Today is {dayOfWeek})";
            }
            else if (text.ToLowerInvariant().Contains("i morgen") || text.ToLowerInvariant().Contains("tomorrow"))
            {
                string dayOfWeek = isEnglish ? DateTime.Now.AddDays(1).DayOfWeek.ToString() : GetDanishDayName(DateTime.Now.AddDays(1).DayOfWeek);
                enhancedQuestion = $"{text} (Tomorrow is {dayOfWeek})";
            }

            // Use the new combined method
            string answer = await _agentService.AskQuestionAboutChildrenAsync(childrenWeekLetters, enhancedQuestion, contextKey, ChatInterface.Telegram);

            await SendMessageInternal(chatId, answer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Aula question");
            string errorMessage = isEnglish
                ? "Sorry, I couldn't process your question at the moment."
                : "Beklager, jeg kunne ikke behandle dit spørgsmål i øjeblikket.";

            await SendMessageInternal(chatId, errorMessage);
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

                await SendMessageInternal(chatId, noChildrenMessage);
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
            await SendMessageInternal(chatId, responseBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling question about all children");
            string errorMessage = isEnglish
                ? "Sorry, I couldn't process information about all children at the moment."
                : "Beklager, jeg kunne ikke behandle information om alle børn i øjeblikket.";

            await SendMessageInternal(chatId, errorMessage);
        }
    }

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