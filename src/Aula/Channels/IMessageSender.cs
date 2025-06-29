using Aula.Bots;

namespace Aula.Channels;

public interface IMessageSender
{
    Task SendMessageAsync(string message);
    Task SendMessageAsync(string chatId, string message);
}

public class SlackMessageSender : IMessageSender
{
    private readonly SlackInteractiveBot _slackBot;

    public SlackMessageSender(SlackInteractiveBot slackBot)
    {
        _slackBot = slackBot ?? throw new ArgumentNullException(nameof(slackBot));
    }

    public async Task SendMessageAsync(string message)
    {
        await _slackBot.SendMessage(message);
    }

    public async Task SendMessageAsync(string chatId, string message)
    {
        await _slackBot.SendMessage(message); // Slack bot doesn't use chatId in the same way
    }
}

public class TelegramMessageSender : IMessageSender
{
    private readonly TelegramInteractiveBot _telegramBot;
    private readonly string _defaultChatId;

    public TelegramMessageSender(TelegramInteractiveBot telegramBot, string defaultChatId)
    {
        _telegramBot = telegramBot ?? throw new ArgumentNullException(nameof(telegramBot));
        _defaultChatId = defaultChatId ?? throw new ArgumentNullException(nameof(defaultChatId));
    }

    public async Task SendMessageAsync(string message)
    {
        await _telegramBot.SendMessage(_defaultChatId, message);
    }

    public async Task SendMessageAsync(string chatId, string message)
    {
        await _telegramBot.SendMessage(chatId, message);
    }
}