using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Communication.Channels;

/// <summary>
/// Manages multiple communication channels and provides unified messaging capabilities.
/// Supports dynamic channel registration, multi-channel broadcasting, and channel coordination.
/// </summary>
public class ChannelManager : IChannelManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, IChannel> _channels = new();

    public ChannelManager(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<ChannelManager>() ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IReadOnlyList<IChannel> GetAllChannels()
    {
        return _channels.Values.ToList();
    }

    public IReadOnlyList<IChannel> GetEnabledChannels()
    {
        return _channels.Values.Where(c => c.IsEnabled).ToList();
    }

    public IChannel? GetChannel(string platformId)
    {
        _channels.TryGetValue(platformId, out var channel);
        return channel;
    }

    public void RegisterChannel(IChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        _channels.AddOrUpdate(channel.PlatformId, channel, (key, existing) =>
        {
            _logger.LogWarning("Replacing existing channel for platform: {PlatformId}", key);
            return channel;
        });

        _logger.LogInformation("Registered channel: {PlatformId} ({DisplayName})",
            channel.PlatformId, channel.DisplayName);
    }

    public bool UnregisterChannel(string platformId)
    {
        if (string.IsNullOrEmpty(platformId))
            return false;

        var removed = _channels.TryRemove(platformId, out var channel);
        if (removed && channel != null)
        {
            _logger.LogInformation("Unregistered channel: {PlatformId} ({DisplayName})",
                channel.PlatformId, channel.DisplayName);
        }

        return removed;
    }

    public async Task BroadcastMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to broadcast empty message");
            return;
        }

        var enabledChannels = GetEnabledChannels();
        if (enabledChannels.Count == 0)
        {
            _logger.LogWarning("No enabled channels available for broadcast");
            return;
        }

        _logger.LogInformation("Broadcasting message to {Count} channels", enabledChannels.Count);

        var tasks = enabledChannels.Select(async channel =>
        {
            try
            {
                var formattedMessage = channel.FormatMessage(message);
                await channel.SendMessageAsync(formattedMessage);
                _logger.LogDebug("Successfully sent message to {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task SendToChannelsAsync(string message, params string[] platformIds)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty message");
            return;
        }

        if (platformIds == null || platformIds.Length == 0)
        {
            _logger.LogWarning("No platform IDs specified for targeted send");
            return;
        }

        var tasks = platformIds.Select(async platformId =>
        {
            var channel = GetChannel(platformId);
            if (channel == null)
            {
                _logger.LogWarning("Channel not found: {PlatformId}", platformId);
                return;
            }

            if (!channel.IsEnabled)
            {
                _logger.LogWarning("Channel is disabled: {PlatformId}", platformId);
                return;
            }

            try
            {
                var formattedMessage = channel.FormatMessage(message);
                await channel.SendMessageAsync(formattedMessage);
                _logger.LogDebug("Successfully sent message to {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task SendFormattedMessageAsync(string message, MessageFormat format = MessageFormat.Auto)
    {
        if (string.IsNullOrEmpty(message))
        {
            _logger.LogWarning("Attempted to send empty formatted message");
            return;
        }

        var enabledChannels = GetEnabledChannels();
        if (enabledChannels.Count == 0)
        {
            _logger.LogWarning("No enabled channels available for formatted message");
            return;
        }

        _logger.LogInformation("Sending formatted message to {Count} channels with format: {Format}",
            enabledChannels.Count, format);

        var tasks = enabledChannels.Select(async channel =>
        {
            try
            {
                var formattedMessage = channel.FormatMessage(message, format);
                await channel.SendMessageAsync(formattedMessage);
                _logger.LogDebug("Successfully sent formatted message to {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send formatted message to {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task<Dictionary<string, bool>> TestAllChannelsAsync()
    {
        var results = new ConcurrentDictionary<string, bool>();
        var allChannels = GetAllChannels();

        _logger.LogInformation("Testing connectivity for {Count} channels", allChannels.Count);

        var tasks = allChannels.Select(async channel =>
        {
            try
            {
                var isConnected = await channel.TestConnectionAsync();
                results[channel.PlatformId] = isConnected;
                _logger.LogDebug("Channel {PlatformId} connection test: {Result}",
                    channel.PlatformId, isConnected ? "PASS" : "FAIL");
            }
            catch (Exception ex)
            {
                results[channel.PlatformId] = false;
                _logger.LogError(ex, "Channel {PlatformId} connection test failed with exception",
                    channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
        return new Dictionary<string, bool>(results);
    }

    public async Task InitializeAllChannelsAsync()
    {
        var allChannels = GetAllChannels();
        _logger.LogInformation("Initializing {Count} channels", allChannels.Count);

        var tasks = allChannels.Select(async channel =>
        {
            try
            {
                await channel.InitializeAsync();
                _logger.LogDebug("Successfully initialized {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task StartAllChannelsAsync()
    {
        var enabledChannels = GetEnabledChannels();
        _logger.LogInformation("Starting {Count} enabled channels", enabledChannels.Count);

        var tasks = enabledChannels.Select(async channel =>
        {
            try
            {
                await channel.StartAsync();
                _logger.LogInformation("Successfully started {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public async Task StopAllChannelsAsync()
    {
        var allChannels = GetAllChannels();
        _logger.LogInformation("Stopping {Count} channels", allChannels.Count);

        var tasks = allChannels.Select(async channel =>
        {
            try
            {
                await channel.StopAsync();
                _logger.LogDebug("Successfully stopped {PlatformId}", channel.PlatformId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop {PlatformId}", channel.PlatformId);
            }
        });

        await Task.WhenAll(tasks);
    }

    public IReadOnlyList<IChannel> GetChannelsWithCapability(ChannelCapabilityFilter filter)
    {
        if (filter == null)
            return GetAllChannels();

        return _channels.Values.Where(channel =>
        {
            var caps = channel.Capabilities;

            if (filter.RequiresInteractivity.HasValue &&
                channel.SupportsInteractivity != filter.RequiresInteractivity.Value)
                return false;

            if (filter.RequiresBold.HasValue &&
                caps.SupportsBold != filter.RequiresBold.Value)
                return false;

            if (filter.RequiresLinks.HasValue &&
                caps.SupportsLinks != filter.RequiresLinks.Value)
                return false;

            if (filter.RequiresButtons.HasValue &&
                caps.SupportsButtons != filter.RequiresButtons.Value)
                return false;

            if (filter.RequiresImages.HasValue &&
                caps.SupportsImages != filter.RequiresImages.Value)
                return false;

            if (filter.MinMessageLength.HasValue &&
                caps.MaxMessageLength < filter.MinMessageLength.Value)
                return false;

            return true;
        }).ToList();
    }
}
