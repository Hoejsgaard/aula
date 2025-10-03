using MinUddannelse.Configuration;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Events;

/// <summary>
/// Event arguments for child-specific events.
/// Ensures events are properly scoped to individual children.
/// </summary>
public class ChildEventArgs : EventArgs
{
    public string ChildId { get; init; } = string.Empty;
    public string ChildFirstName { get; init; } = string.Empty;
    public DateTimeOffset EventTime { get; init; }
    public string EventType { get; init; } = string.Empty;
    public JObject? Data { get; init; }

    public ChildEventArgs(string childId, string childFirstName, string eventType, JObject? data = null)
    {
        ChildId = childId ?? throw new ArgumentNullException(nameof(childId));
        ChildFirstName = childFirstName ?? throw new ArgumentNullException(nameof(childFirstName));
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        EventTime = DateTimeOffset.UtcNow;
        Data = data;
    }
}
