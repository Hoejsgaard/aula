using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aula.Channels;

/// <summary>
/// Secure implementation of child-aware channel management with comprehensive security layers.
/// Ensures complete isolation of messaging per child with access control and content filtering.
/// </summary>
public class SecureChildChannelManager : IChildChannelManager
{
    private readonly IChildContext _context;
    private readonly IChildContextValidator _contextValidator;
    private readonly IChildAuditService _auditService;
    private readonly IChannelManager _channelManager;
    private readonly IMessageContentFilter _contentFilter;
    private readonly ILogger<SecureChildChannelManager> _logger;

    // In-memory channel configurations (in production, this would be in database)
    private readonly Dictionary<string, List<ChildChannelConfig>> _childChannelConfigs = new();
    private readonly object _configLock = new();

    public SecureChildChannelManager(
        IChildContext context,
        IChildContextValidator contextValidator,
        IChildAuditService auditService,
        IChannelManager channelManager,
        IMessageContentFilter contentFilter,
        ILogger<SecureChildChannelManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        _contentFilter = contentFilter ?? throw new ArgumentNullException(nameof(contentFilter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeDefaultConfigurations();
    }

    public async Task<bool> SendMessageAsync(string message, MessageFormat format = MessageFormat.Auto)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "channel:send"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to send messages", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "channel:send", SecuritySeverity.Warning);
            return false;
        }

        // Layer 3: Content filtering
        var filteredMessage = _contentFilter.FilterForChild(message, child);
        if (!_contentFilter.ValidateMessageSafety(filteredMessage, child))
        {
            _logger.LogWarning("Message failed safety validation for {ChildName}", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "UnsafeMessage", "channel:send", SecuritySeverity.Warning);
            return false;
        }

        // Layer 4: Get child's channels
        var childChannels = await GetChildChannelsAsync();
        if (!childChannels.Any())
        {
            _logger.LogWarning("No channels configured for {ChildName}", child.FirstName);
            return false;
        }

        // Layer 5: Send to child's channels only
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

        // Layer 6: Audit logging
        await _auditService.LogDataAccessAsync(child, "SendMessage",
            $"Channels:{childChannels.Count},Success:{success}", success);

        return success;
    }

    public async Task<bool> SendToPlatformAsync(string platformId, string message, MessageFormat format = MessageFormat.Auto)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "channel:send"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to send to {Platform}",
                child.FirstName, platformId);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied",
                $"channel:send:{platformId}", SecuritySeverity.Warning);
            return false;
        }

        // Layer 3: Check if child has access to this platform
        var childChannels = await GetChildChannelsAsync();
        var channelConfig = childChannels.FirstOrDefault(c =>
            c.PlatformId.Equals(platformId, StringComparison.OrdinalIgnoreCase));

        if (channelConfig == null || !channelConfig.IsEnabled)
        {
            _logger.LogWarning("Platform {Platform} not configured for {ChildName}",
                platformId, child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "UnauthorizedPlatform",
                platformId, SecuritySeverity.Warning);
            return false;
        }

        // Layer 4: Content filtering
        var filteredMessage = _contentFilter.FilterForChild(message, child);
        if (!_contentFilter.ValidateMessageSafety(filteredMessage, child))
        {
            _logger.LogWarning("Message failed safety validation for {ChildName} on {Platform}",
                child.FirstName, platformId);
            return false;
        }

        // Layer 5: Send message
        try
        {
            var channel = _channelManager.GetChannel(platformId);
            if (channel != null && channel.IsEnabled)
            {
                var formattedMessage = channel.FormatMessage(filteredMessage, format);
                await channel.SendMessageAsync(channelConfig.ChannelId, formattedMessage);

                // Layer 6: Audit logging
                await _auditService.LogDataAccessAsync(child, "SendToPlatform",
                    $"Platform:{platformId}", true);

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {Platform} for {ChildName}",
                platformId, child.FirstName);
            await _auditService.LogDataAccessAsync(child, "SendToPlatform",
                $"Platform:{platformId}", false);
        }

        return false;
    }

    public async Task<IReadOnlyList<ChildChannelConfig>> GetChildChannelsAsync()
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "channel:read"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to read channel config", child.FirstName);
            return new List<ChildChannelConfig>();
        }

        // Layer 3: Get child-specific channels
        lock (_configLock)
        {
            var key = GetChildKey(child);
            if (_childChannelConfigs.TryGetValue(key, out var configs))
            {
                // Return defensive copies
                return configs.Select(c => CloneConfig(c)).ToList();
            }
        }

        _logger.LogDebug("No channels configured for {ChildName}", child.FirstName);
        return new List<ChildChannelConfig>();
    }

    public async Task<Dictionary<string, bool>> TestChildChannelsAsync()
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        var results = new Dictionary<string, bool>();
        var childChannels = await GetChildChannelsAsync();

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

        await _auditService.LogDataAccessAsync(child, "TestChannels",
            $"Count:{results.Count}", true);

        return results;
    }

    public async Task<bool> SendAlertAsync(string alertMessage)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Permission validation for alerts
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "channel:alert"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to send alerts", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied",
                "channel:alert", SecuritySeverity.Warning);
            return false;
        }

        // Layer 3: Check alert permissions per channel
        var childChannels = await GetChildChannelsAsync();
        var alertChannels = childChannels.Where(c =>
            c.IsEnabled && c.Permissions.CanReceiveAlerts).ToList();

        if (!alertChannels.Any())
        {
            _logger.LogWarning("No alert-enabled channels for {ChildName}", child.FirstName);
            return false;
        }

        // Layer 4: Format alert message
        var formattedAlert = $"⚠️ ALERT for {child.FirstName}: {alertMessage}";

        // Layer 5: Send alert with high priority
        var success = await SendToSpecificChannels(alertChannels, formattedAlert, child);

        await _auditService.LogDataAccessAsync(child, "SendAlert",
            $"Channels:{alertChannels.Count},Success:{success}", success);

        return success;
    }

    public async Task<bool> SendReminderAsync(string reminderMessage, string? metadata = null)
    {
        // Layer 1: Context validation
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Layer 2: Check reminder permissions
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "channel:reminder"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to send reminders", child.FirstName);
            return false;
        }

        // Layer 3: Get reminder-enabled channels
        var childChannels = await GetChildChannelsAsync();
        var reminderChannels = childChannels.Where(c =>
            c.IsEnabled && c.Permissions.CanReceiveReminders).ToList();

        if (!reminderChannels.Any())
        {
            _logger.LogWarning("No reminder-enabled channels for {ChildName}", child.FirstName);
            return false;
        }

        // Layer 4: Format reminder
        var formattedReminder = $"📅 Reminder for {child.FirstName}: {reminderMessage}";
        if (!string.IsNullOrEmpty(metadata))
        {
            formattedReminder += $"\n{metadata}";
        }

        // Layer 5: Send reminder
        var success = await SendToSpecificChannels(reminderChannels, formattedReminder, child);

        await _auditService.LogDataAccessAsync(child, "SendReminder",
            $"Channels:{reminderChannels.Count},Success:{success}", success);

        return success;
    }

    public async Task<ChildChannelConfig?> GetPreferredChannelAsync()
    {
        var channels = await GetChildChannelsAsync();
        var preferred = channels.FirstOrDefault(c => c.IsPreferred && c.IsEnabled);

        if (preferred == null)
        {
            // Fall back to first enabled channel
            preferred = channels.FirstOrDefault(c => c.IsEnabled);
        }

        return preferred != null ? CloneConfig(preferred) : null;
    }

    public async Task<bool> HasConfiguredChannelsAsync()
    {
        var channels = await GetChildChannelsAsync();
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

            // Remove existing config for same platform if exists
            _childChannelConfigs[key].RemoveAll(c =>
                c.PlatformId.Equals(config.PlatformId, StringComparison.OrdinalIgnoreCase));

            // Add new config
            config.ChildFirstName = child.FirstName;
            config.ChildLastName = child.LastName;
            _childChannelConfigs[key].Add(config);

            _logger.LogInformation("Configured {Platform} channel for {ChildName}",
                config.PlatformId, child.FirstName);
        }
    }

    // Helper methods
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

    private void InitializeDefaultConfigurations()
    {
        // This would normally load from database
        // For now, initialize with some default configurations for testing

        // Example: Configure Slack for TestChild
        var testChild = new Child { FirstName = "Test", LastName = "Child" };
        var slackConfig = new ChildChannelConfig
        {
            PlatformId = "slack",
            ChannelId = "test-channel",
            IsPreferred = true,
            IsEnabled = true,
            Permissions = new ChannelPermissions
            {
                CanReceiveWeekLetters = true,
                CanReceiveReminders = true,
                CanReceiveAlerts = true,
                CanReceiveAISummaries = true,
                CanInteract = true
            }
        };
        ConfigureChildChannel(testChild, slackConfig);
    }
}