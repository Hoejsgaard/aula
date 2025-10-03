namespace MinUddannelse.Events;

/// <summary>
/// Event arguments for child message events - used for any message that needs to be sent to a specific child.
/// This includes reminders, notifications, AI analysis results, etc.
/// </summary>
public class ChildMessageEventArgs : ChildEventArgs
{
    public string Message { get; init; }
    public string MessageType { get; init; }

    public ChildMessageEventArgs(string childId, string childFirstName, string message, string messageType)
        : base(childId, childFirstName, "message", null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        Message = message;
        MessageType = messageType;
    }
}