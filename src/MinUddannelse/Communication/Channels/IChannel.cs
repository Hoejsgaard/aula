using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinUddannelse.Communication.Channels;

/// <summary>
/// Represents a communication channel platform (Slack, Telegram, Discord, etc.)
/// with standardized messaging capabilities and platform-specific features.
/// </summary>
public interface IChannel
{
    /// <summary>
    /// Unique identifier for the channel platform (e.g., "slack", "telegram", "discord").
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// Human-readable display name for the platform.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Indicates if this channel is currently enabled and available for use.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Indicates if this channel supports interactive features (bots, buttons, etc.).
    /// </summary>
    bool SupportsInteractivity { get; }

    /// <summary>
    /// Message formatting capabilities supported by this channel.
    /// </summary>
    ChannelCapabilities Capabilities { get; }

    /// <summary>
    /// Sends a message to the default channel/chat for this platform.
    /// </summary>
    Task SendMessageAsync(string message);

    /// <summary>
    /// Sends a message to a specific channel/chat on this platform.
    /// </summary>
    Task SendMessageAsync(string channelId, string message);

    /// <summary>
    /// Formats a message according to this platform's markdown/formatting rules.
    /// </summary>
    string FormatMessage(string message, MessageFormat format = MessageFormat.Auto);

    /// <summary>
    /// Gets the default channel/chat ID for this platform, if configured.
    /// </summary>
    string? GetDefaultChannelId();

    /// <summary>
    /// Tests if the channel connection is working properly.
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Initializes the channel connection (if needed).
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Starts any background services for this channel (e.g., bot polling).
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops any background services for this channel.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Describes the messaging capabilities of a channel platform.
/// </summary>
public class ChannelCapabilities
{
    public bool SupportsBold { get; set; }
    public bool SupportsItalic { get; set; }
    public bool SupportsCode { get; set; }
    public bool SupportsCodeBlocks { get; set; }
    public bool SupportsLinks { get; set; }
    public bool SupportsButtons { get; set; }
    public bool SupportsImages { get; set; }
    public bool SupportsFiles { get; set; }
    public bool SupportsThreads { get; set; }
    public bool SupportsEmojis { get; set; }
    public int MaxMessageLength { get; set; } = 4000;
    public string[] SupportedFormatTags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Standard message formatting options that channels can interpret.
/// </summary>
public enum MessageFormat
{
    Auto,      // Detect format from message content
    Plain,    // Plain text, no formatting
    Markdown,   // Standard markdown
    Html,      // HTML formatting
    Platform	// Use platform-specific formatting
}
