using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Aula.Tools;
using Aula.Utilities;
using Newtonsoft.Json.Linq;

namespace Aula.Bots;

public class TelegramMessageHandler
{
    private readonly IAgentService _agentService;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly ISupabaseService _supabaseService;
    private readonly Dictionary<string, Child> _childrenByName;
    private readonly ConversationContextManager<long> _conversationContextManager;
    private readonly ReminderCommandHandler _reminderHandler;

    public TelegramMessageHandler(
        IAgentService agentService,
        Config config,
        ILogger logger,
        ISupabaseService supabaseService,
        Dictionary<string, Child> childrenByName,
        ConversationContextManager<long> conversationContextManager,
        ReminderCommandHandler reminderHandler)
    {
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(supabaseService);
        ArgumentNullException.ThrowIfNull(childrenByName);
        ArgumentNullException.ThrowIfNull(conversationContextManager);
        ArgumentNullException.ThrowIfNull(reminderHandler);

        _agentService = agentService;
        _config = config;
        _logger = logger;
        _supabaseService = supabaseService;
        _childrenByName = childrenByName;
        _conversationContextManager = conversationContextManager;
        _reminderHandler = reminderHandler;
    }

    public async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Type != MessageType.Text || string.IsNullOrEmpty(message.Text))
            return;

        var chatId = message.Chat.Id;
        var messageText = message.Text.Trim();

        _logger.LogInformation("Processing message from {ChatId}: {Text}", chatId, messageText);

        try
        {
            // Check for help command first
            if (await TryHandleHelpCommand(botClient, chatId, messageText, cancellationToken))
            {
                return;
            }

            // Extract child from the message or use the first configured child for this telegram bot
            Child? specificChild = null;
            foreach (var kvp in _childrenByName)
            {
                if (messageText.ToLowerInvariant().Contains(kvp.Key.ToLowerInvariant()))
                {
                    specificChild = kvp.Value;
                    break;
                }
            }

            // If no child mentioned, use the first one configured for Telegram
            if (specificChild == null)
            {
                specificChild = _childrenByName.Values.FirstOrDefault();
            }

            // Use the new tool-based processing that can handle both tools and regular questions
            string contextKey = $"telegram-{chatId}";
            string response = await _agentService.ProcessQueryWithToolsAsync(messageText, contextKey, specificChild, ChatInterface.Telegram);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: response,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sent response to Telegram chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", messageText);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Sorry, I encountered an error processing your message. Please try again.",
                cancellationToken: cancellationToken);
        }
    }

    private async Task<bool> TryHandleHelpCommand(ITelegramBotClient botClient, long chatId, string text, CancellationToken cancellationToken)
    {
        var normalizedText = text.Trim().ToLowerInvariant();

        // English help commands
        if (normalizedText == "help" || normalizedText == "--help" || normalizedText == "?" ||
            normalizedText == "commands" || normalizedText == "/help" || normalizedText == "/start")
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: GetEnglishHelpMessage(),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return true;
        }

        // Danish help commands  
        if (normalizedText == "hjælp" || normalizedText == "kommandoer" || normalizedText == "/hjælp")
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: GetDanishHelpMessage(),
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
            return true;
        }

        return false;
    }

    private static string GetEnglishHelpMessage()
    {
        return @"🤖 **Aula Bot Help**

I can help you with information about your children's school activities from Aula.

**Commands:**
• Ask about activities: ""What activities does Emma have this week?""
• Get week letters: ""Show me this week's letter for Oliver""
• Create reminders: ""Remind me to pick up Emma at 3pm tomorrow""
• Get homework info: ""What homework does Oliver have?""

**Languages:** You can ask in both English and Danish.

**Tips:**
• Be specific about which child you're asking about
• I can remember the context of our conversation for a few minutes
• Use natural language - no need for special commands";
    }

    private static string GetDanishHelpMessage()
    {
        return @"🤖 **Aula Bot Hjælp**

Jeg kan hjælpe dig med information om dine børns skoleaktiviteter fra Aula.

**Kommandoer:**
• Spørg om aktiviteter: ""Hvilke aktiviteter har Emma denne uge?""
• Få ugebreve: ""Vis mig denne uges brev til Oliver""
• Opret påmindelser: ""Påmind mig om at hente Emma kl. 15 i morgen""
• Få lektie info: ""Hvilke lektier har Oliver?""

**Sprog:** Du kan spørge på både engelsk og dansk.

**Tips:**
• Vær specifik om hvilket barn du spørger om
• Jeg kan huske konteksten af vores samtale i et par minutter
• Brug naturligt sprog - ingen særlige kommandoer nødvendige";
    }


    private string? ExtractChildNameFromText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        text = text.ToLowerInvariant();

        // Check for full names
        foreach (var childName in _childrenByName.Keys)
        {
            if (text.Contains(childName, StringComparison.OrdinalIgnoreCase))
            {
                return childName;
            }
        }

        // Check for first names
        foreach (var child in _childrenByName.Values)
        {
            string firstName = child.FirstName.ToLowerInvariant();
            if (text.Contains(firstName, StringComparison.OrdinalIgnoreCase))
            {
                return _childrenByName.Keys.FirstOrDefault(k =>
                    k.StartsWith(firstName, StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    private void UpdateConversationContext(long chatId, string? childName)
    {
        _conversationContextManager.UpdateContext(chatId, childName);
        _logger.LogInformation("Updated conversation context for chat {ChatId}", chatId);
    }
}
