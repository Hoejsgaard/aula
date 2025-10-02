using System.Collections.Generic;
using Aula.Core.Models;
using System.Threading.Tasks;
using Aula.Configuration;

namespace Aula.Communication.Channels;

public interface IChildChannelManager
{
    Task<bool> SendMessageAsync(Child child, string message, MessageFormat format = MessageFormat.Auto);
    Task<bool> SendToPlatformAsync(Child child, string platformId, string message, MessageFormat format = MessageFormat.Auto);
    Task<IReadOnlyList<ChildChannelConfig>> GetChildChannelsAsync(Child child);
    Task<Dictionary<string, bool>> TestChildChannelsAsync(Child child);
    Task<bool> SendAlertAsync(Child child, string alertMessage);
    Task<bool> SendReminderAsync(Child child, string reminderMessage, string? metadata = null);
    Task<ChildChannelConfig?> GetPreferredChannelAsync(Child child);
    Task<bool> HasConfiguredChannelsAsync(Child child);
}

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

public class ChannelPermissions
{
    public bool CanReceiveWeekLetters { get; set; } = true;
    public bool CanReceiveReminders { get; set; } = true;
    public bool CanReceiveAlerts { get; set; } = true;
    public bool CanReceiveAISummaries { get; set; } = true;
    public bool CanInteract { get; set; } = true;
}
