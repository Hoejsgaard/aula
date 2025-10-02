using System.Collections.Generic;
using Aula.Core.Models;
using System.Threading.Tasks;
using Aula.Configuration;

namespace Aula.Communication.Channels;

/// <summary>
/// Child-aware channel manager that accepts Child parameters for all operations.
/// Ensures messages are routed only to the channels configured for the specified child.
/// </summary>
public interface IChildChannelManager
{
    /// <summary>
    /// Sends a message to all channels configured for the specified child.
    /// </summary>
    Task<bool> SendMessageAsync(Child child, string message, MessageFormat format = MessageFormat.Auto);

    /// <summary>
    /// Sends a message to a specific platform configured for the specified child.
    /// </summary>
    Task<bool> SendToPlatformAsync(Child child, string platformId, string message, MessageFormat format = MessageFormat.Auto);

    /// <summary>
    /// Gets all channels configured for the specified child.
    /// </summary>
    Task<IReadOnlyList<ChildChannelConfig>> GetChildChannelsAsync(Child child);

    /// <summary>
    /// Tests connectivity for all channels configured for the specified child.
    /// </summary>
    Task<Dictionary<string, bool>> TestChildChannelsAsync(Child child);

    /// <summary>
    /// Sends a high-priority alert to the child's channels.
    /// </summary>
    Task<bool> SendAlertAsync(Child child, string alertMessage);

    /// <summary>
    /// Sends a reminder notification to the child's channels.
    /// </summary>
    Task<bool> SendReminderAsync(Child child, string reminderMessage, string? metadata = null);

    /// <summary>
    /// Gets the preferred channel for the specified child.
    /// </summary>
    Task<ChildChannelConfig?> GetPreferredChannelAsync(Child child);

    /// <summary>
    /// Validates that the specified child has at least one configured channel.
    /// </summary>
    Task<bool> HasConfiguredChannelsAsync(Child child);
}

/// <summary>
/// Configuration for a child's channel.
/// </summary>
public class ChildChannelConfig
{
    public string PlatformId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChildFirstName { get; set; } = string.Empty;
    public string ChildLastName { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public bool IsEnabled { get; set; } = true;
    public ChannelPermissions Permissions { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Permissions for channel operations.
/// </summary>
public class ChannelPermissions
{
    public bool CanReceiveWeekLetters { get; set; } = true;
    public bool CanReceiveReminders { get; set; } = true;
    public bool CanReceiveAlerts { get; set; } = true;
    public bool CanReceiveAISummaries { get; set; } = true;
    public bool CanInteract { get; set; } = true;
}
