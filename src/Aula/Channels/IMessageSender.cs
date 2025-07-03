namespace Aula.Channels;

public interface IMessageSender
{
    Task SendMessageAsync(string message);
    Task SendMessageAsync(string chatId, string message);
}

public class SlackMessageSender : IMessageSender
{
    private readonly IChannelMessenger _messenger;

    public SlackMessageSender(IChannelMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        if (messenger.PlatformType != "Slack")
        {
            throw new ArgumentException("Expected Slack messenger", nameof(messenger));
        }
    }

    public async Task SendMessageAsync(string message)
    {
        await _messenger.SendMessageAsync(message);
    }

    public async Task SendMessageAsync(string chatId, string message)
    {
        await _messenger.SendMessageAsync(chatId, message);
    }
}

public class TelegramMessageSender : IMessageSender
{
    private readonly IChannelMessenger _messenger;

    public TelegramMessageSender(IChannelMessenger messenger)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        if (messenger.PlatformType != "Telegram")
        {
            throw new ArgumentException("Expected Telegram messenger", nameof(messenger));
        }
    }

    public async Task SendMessageAsync(string message)
    {
        await _messenger.SendMessageAsync(message);
    }

    public async Task SendMessageAsync(string chatId, string message)
    {
        await _messenger.SendMessageAsync(chatId, message);
    }
}