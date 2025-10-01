namespace Aula.Channels;

/// <summary>
/// Platform-agnostic interface for sending messages to communication channels.
/// This abstraction allows messaging without depending on specific bot implementations.
/// </summary>
public interface IChannelMessenger
{
    /// <summary>
    /// Sends a message to the default channel/chat.
    /// </summary>
    Task SendMessageAsync(string message);

    /// <summary>
    /// Sends a message to a specific channel/chat.
    /// </summary>
    Task SendMessageAsync(string channelId, string message);

    /// <summary>
    /// Gets the platform type for this messenger.
    /// </summary>
    string PlatformType { get; }
}
