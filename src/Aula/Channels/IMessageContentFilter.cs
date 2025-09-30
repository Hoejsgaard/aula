using Aula.Configuration;

namespace Aula.Channels;

/// <summary>
/// Filters message content to prevent information leakage between children.
/// </summary>
public interface IMessageContentFilter
{
	/// <summary>
	/// Filters a message to ensure it only contains information appropriate for the target child.
	/// </summary>
	string FilterForChild(string message, Child child);

	/// <summary>
	/// Checks if a message contains information about other children.
	/// </summary>
	bool ContainsOtherChildData(string message, Child currentChild);

	/// <summary>
	/// Removes references to other children from the message.
	/// </summary>
	string RemoveOtherChildReferences(string message, Child currentChild);

	/// <summary>
	/// Validates that a message is safe to send to a child's channel.
	/// </summary>
	bool ValidateMessageSafety(string message, Child targetChild);

	/// <summary>
	/// Redacts sensitive information from messages.
	/// </summary>
	string RedactSensitiveInfo(string message);
}
