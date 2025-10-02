using Aula.Authentication;
using Aula.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Channels;

public class SecureChildChannelManager : IChildChannelManager
{
    private readonly IChannelManager _channelManager;
    private readonly IMessageContentFilter _contentFilter;
    private readonly ILogger _logger;

    // In-memory channel configurations (in production, this would be in database)
    private readonly Dictionary<string, List<ChildChannelConfig>> _childChannelConfigs = new();
    private readonly object _configLock = new();

    public SecureChildChannelManager(
        IChannelManager channelManager,
        IMessageContentFilter contentFilter,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(channelManager);
        ArgumentNullException.ThrowIfNull(contentFilter);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _channelManager = channelManager;
        _contentFilter = contentFilter;
        _logger = loggerFactory.CreateLogger<SecureChildChannelManager>();
    }

    public async Task<bool> SendMessageAsync(Child child, string message, MessageFormat format = MessageFormat.Auto)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var filteredMessage = _contentFilter.FilterForChild(message, child);
        if (!_contentFilter.ValidateMessageSafety(filteredMessage, child))
        {
            _logger.LogWarning("Message failed safety validation for {ChildName}", child.FirstName);
            return false;
        }

        var childChannels = await GetChildChannelsAsync(child);
        if (childChannels.Count == 0)
        {
            _logger.LogWarning("No channels configured for {ChildName}", child.FirstName);
            return false;
        }

        var success = true;
        foreach (var channelConfig in childChannels.Where(c => c.IsEnabled))
        {
            try
            {
                var channel = _channelManager.GetChannel(channelConfig.PlatformId);
                if (channel != null && channel.IsEnabled)
                {
                    var formattedMessage = channel.FormatMessage(filteredMessage, format);
                    await channel.SendMessageAsync(channelConfig.ChannelId, formattedMessage);

                    _logger.LogInformation("Message sent to {PlatformId} for {ChildName}",
                        channelConfig.PlatformId, child.FirstName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to {PlatformId} for {ChildName}",
                    channelConfig.PlatformId, child.FirstName);
                success = false;
            }
        }

        return success;
    }

    public async Task<bool> SendToPlatformAsync(Child child, string platformId, string message, MessageFormat format = MessageFormat.Auto)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var childChannels = await GetChildChannelsAsync(child);
        var channelConfig = childChannels.FirstOrDefault(c =>
            c.PlatformId.Equals(platformId, StringComparison.OrdinalIgnoreCase));

        if (channelConfig == null || !channelConfig.IsEnabled)
        {
            _logger.LogWarning("Platform {Platform} not configured for {ChildName}",
                platformId, child.FirstName);
            return false;
        }

        var filteredMessage = _contentFilter.FilterForChild(message, child);
        if (!_contentFilter.ValidateMessageSafety(filteredMessage, child))
        {
            _logger.LogWarning("Message failed safety validation for {ChildName} on {Platform}",
                child.FirstName, platformId);
            return false;
        }

        try
        {
            var channel = _channelManager.GetChannel(platformId);
            if (channel != null && channel.IsEnabled)
            {
                var formattedMessage = channel.FormatMessage(filteredMessage, format);
                await channel.SendMessageAsync(channelConfig.ChannelId, formattedMessage);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {Platform} for {ChildName}",
                platformId, child.FirstName);
        }

        return false;
    }

    public Task<IReadOnlyList<ChildChannelConfig>> GetChildChannelsAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        lock (_configLock)
        {
            var key = GetChildKey(child);
            if (_childChannelConfigs.TryGetValue(key, out var configs))
            {
                return Task.FromResult<IReadOnlyList<ChildChannelConfig>>(configs.Select(c => CloneConfig(c)).ToList());
            }
        }

        _logger.LogDebug("No channels configured for {ChildName}", child.FirstName);
        return Task.FromResult<IReadOnlyList<ChildChannelConfig>>(new List<ChildChannelConfig>());
    }

    public async Task<Dictionary<string, bool>> TestChildChannelsAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var results = new Dictionary<string, bool>();
        var childChannels = await GetChildChannelsAsync(child);

        foreach (var channelConfig in childChannels)
        {
            try
            {
                var channel = _channelManager.GetChannel(channelConfig.PlatformId);
                if (channel != null)
                {
                    var isConnected = await channel.TestConnectionAsync();
                    results[channelConfig.PlatformId] = isConnected;
                }
                else
                {
                    results[channelConfig.PlatformId] = false;
                }
            }
            catch
            {
                results[channelConfig.PlatformId] = false;
            }
        }

        return results;
    }

    public async Task<bool> SendAlertAsync(Child child, string alertMessage)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var childChannels = await GetChildChannelsAsync(child);
        var alertChannels = childChannels.Where(c =>
            c.IsEnabled && c.Permissions.CanReceiveAlerts).ToList();

        if (alertChannels.Count == 0)
        {
            _logger.LogWarning("No alert-enabled channels for {ChildName}", child.FirstName);
            return false;
        }

        var formattedAlert = $"⚠️ ALERT for {child.FirstName}: {alertMessage}";

        var success = await SendToSpecificChannels(alertChannels, formattedAlert, child);

        return success;
    }

    public async Task<bool> SendReminderAsync(Child child, string reminderMessage, string? metadata = null)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var childChannels = await GetChildChannelsAsync(child);
        var reminderChannels = childChannels.Where(c =>
            c.IsEnabled && c.Permissions.CanReceiveReminders).ToList();

        if (reminderChannels.Count == 0)
        {
            _logger.LogWarning("No reminder-enabled channels for {ChildName}", child.FirstName);
            return false;
        }

        var formattedReminder = $"Reminder for {child.FirstName}: {reminderMessage}";
        if (!string.IsNullOrEmpty(metadata))
        {
            formattedReminder += $"\n{metadata}";
        }

        var success = await SendToSpecificChannels(reminderChannels, formattedReminder, child);

        return success;
    }

    public async Task<ChildChannelConfig?> GetPreferredChannelAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var channels = await GetChildChannelsAsync(child);
        var preferred = channels.FirstOrDefault(c => c.IsPreferred && c.IsEnabled);

        if (preferred == null)
        {
            // Fall back to first enabled channel
            preferred = channels.FirstOrDefault(c => c.IsEnabled);
        }

        return preferred != null ? CloneConfig(preferred) : null;
    }

    public async Task<bool> HasConfiguredChannelsAsync(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        var channels = await GetChildChannelsAsync(child);
        return channels.Any(c => c.IsEnabled);
    }

    // Configuration management (would be in database in production)
    public void ConfigureChildChannel(Child child, ChildChannelConfig config)
    {
        lock (_configLock)
        {
            var key = GetChildKey(child);
            if (!_childChannelConfigs.ContainsKey(key))
            {
                _childChannelConfigs[key] = new List<ChildChannelConfig>();
            }

            _childChannelConfigs[key].RemoveAll(c =>
                c.PlatformId.Equals(config.PlatformId, StringComparison.OrdinalIgnoreCase));

            config.ChildFirstName = child.FirstName;
            config.ChildLastName = child.LastName;
            _childChannelConfigs[key].Add(config);

            _logger.LogInformation("Configured {Platform} channel for {ChildName}",
                config.PlatformId, child.FirstName);
        }
    }

    private async Task<bool> SendToSpecificChannels(List<ChildChannelConfig> channels,
        string message, Child child)
    {
        var filteredMessage = _contentFilter.FilterForChild(message, child);
        var success = true;

        foreach (var channelConfig in channels)
        {
            try
            {
                var channel = _channelManager.GetChannel(channelConfig.PlatformId);
                if (channel != null && channel.IsEnabled)
                {
                    await channel.SendMessageAsync(channelConfig.ChannelId, filteredMessage);
                    _logger.LogDebug("Sent message to {Platform} for {ChildName}",
                        channelConfig.PlatformId, child.FirstName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send to {Platform} for {ChildName}",
                    channelConfig.PlatformId, child.FirstName);
                success = false;
            }
        }

        return success;
    }

    private string GetChildKey(Child child)
    {
        return $"{child.FirstName}_{child.LastName}".ToLowerInvariant();
    }

    private ChildChannelConfig CloneConfig(ChildChannelConfig config)
    {
        return new ChildChannelConfig
        {
            PlatformId = config.PlatformId,
            ChannelId = config.ChannelId,
            ChildFirstName = config.ChildFirstName,
            ChildLastName = config.ChildLastName,
            IsPreferred = config.IsPreferred,
            IsEnabled = config.IsEnabled,
            Permissions = new ChannelPermissions
            {
                CanReceiveWeekLetters = config.Permissions.CanReceiveWeekLetters,
                CanReceiveReminders = config.Permissions.CanReceiveReminders,
                CanReceiveAlerts = config.Permissions.CanReceiveAlerts,
                CanReceiveAISummaries = config.Permissions.CanReceiveAISummaries,
                CanInteract = config.Permissions.CanInteract
            },
            Metadata = new Dictionary<string, string>(config.Metadata)
        };
    }

}
