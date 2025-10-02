namespace MinUddannelse.Communication.Channels;

public interface IMessageSender
{
    Task SendMessageAsync(string message);
    Task SendMessageAsync(string chatId, string message);
}

/// <summary>
/// Abstract base class for message senders that provides common functionality
/// for validating and delegating to platform-specific channel messengers.
/// </summary>
public abstract class MessageSenderBase : IMessageSender
{
    protected IChannelMessenger Messenger { get; }

    protected MessageSenderBase(IChannelMessenger messenger, string expectedPlatformType)
    {
        Messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

        if (messenger.PlatformType != expectedPlatformType)
        {
            throw new ArgumentException($"Expected {expectedPlatformType} messenger", nameof(messenger));
        }
    }

    public virtual async Task SendMessageAsync(string message)
    {
        await Messenger.SendMessageAsync(message);
    }

    public virtual async Task SendMessageAsync(string chatId, string message)
    {
        await Messenger.SendMessageAsync(chatId, message);
    }
}

public class SlackMessageSender : MessageSenderBase
{
    public SlackMessageSender(IChannelMessenger messenger) : base(messenger, "Slack")
    {
    }
}

public class TelegramMessageSender : MessageSenderBase
{
    public TelegramMessageSender(IChannelMessenger messenger) : base(messenger, "Telegram")
    {
    }
}
