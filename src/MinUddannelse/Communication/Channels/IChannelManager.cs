using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinUddannelse.Communication.Channels;

/// <summary>
/// Manages multiple communication channels and provides unified messaging capabilities.
/// Supports dynamic channel registration, multi-channel broadcasting, and channel coordination.
/// </summary>
public interface IChannelManager : IDisposable
{
    /// <summary>
    /// Gets all registered channels.
    /// </summary>
    IReadOnlyList<IChannel> GetAllChannels();

    /// <summary>
    /// Gets all enabled channels.
    /// </summary>
    IReadOnlyList<IChannel> GetEnabledChannels();

    /// <summary>
    /// Gets a specific channel by platform ID.
    /// </summary>
    IChannel? GetChannel(string platformId);

    /// <summary>
    /// Registers a new channel with the manager.
    /// </summary>
    void RegisterChannel(IChannel channel);

    /// <summary>
    /// Unregisters a channel from the manager.
    /// </summary>
    bool UnregisterChannel(string platformId);

    /// <summary>
    /// Sends a message to all enabled channels.
    /// </summary>
    Task BroadcastMessageAsync(string message);

    /// <summary>
    /// Sends a message to all channels configured for a specific child.
    /// </summary>
    Task SendMessageToChildChannelsAsync(string childName, string message);

    /// <summary>
    /// Sends a message to specific channels by platform ID.
    /// </summary>
    Task SendToChannelsAsync(string message, params string[] platformIds);

    /// <summary>
    /// Sends platform-specific formatted messages to multiple channels.
    /// </summary>
    Task SendFormattedMessageAsync(string message, MessageFormat format = MessageFormat.Auto);

    /// <summary>
    /// Tests connectivity for all enabled channels.
    /// </summary>
    Task<Dictionary<string, bool>> TestAllChannelsAsync();

    /// <summary>
    /// Initializes all registered channels.
    /// </summary>
    Task InitializeAllChannelsAsync();

    /// <summary>
    /// Starts all enabled channels.
    /// </summary>
    Task StartAllChannelsAsync();

    /// <summary>
    /// Stops all channels.
    /// </summary>
    Task StopAllChannelsAsync();

    /// <summary>
    /// Gets channels that support specific capabilities.
    /// </summary>
    IReadOnlyList<IChannel> GetChannelsWithCapability(ChannelCapabilityFilter filter);
}

/// <summary>
/// Filter criteria for finding channels with specific capabilities.
/// </summary>
public class ChannelCapabilityFilter
{
    public bool? RequiresInteractivity { get; set; }
    public bool? RequiresBold { get; set; }
    public bool? RequiresLinks { get; set; }
    public bool? RequiresButtons { get; set; }
    public bool? RequiresImages { get; set; }
    public int? MinMessageLength { get; set; }
}
