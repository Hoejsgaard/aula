using Aula.Configuration;

namespace Aula.Context;

/// <summary>
/// Validates child context integrity and permissions to ensure secure isolation.
/// </summary>
public interface IChildContextValidator
{
	/// <summary>
	/// Validates that the context is properly initialized and not tampered with.
	/// </summary>
	/// <param name="context">The context to validate</param>
	/// <returns>True if the context is valid, false otherwise</returns>
	Task<bool> ValidateContextIntegrityAsync(IChildContext context);

	/// <summary>
	/// Validates that a child has permission to perform a specific operation.
	/// </summary>
	/// <param name="child">The child to validate</param>
	/// <param name="operation">The operation identifier (e.g., "read:week_letter")</param>
	/// <returns>True if the child has permission, false otherwise</returns>
	Task<bool> ValidateChildPermissionsAsync(Child child, string operation);

	/// <summary>
	/// Validates that the current context matches the expected child.
	/// </summary>
	/// <param name="context">The current context</param>
	/// <param name="expectedChild">The expected child</param>
	/// <returns>True if the context matches the expected child</returns>
	bool ValidateContextMatchesChild(IChildContext context, Child expectedChild);

	/// <summary>
	/// Validates that the context has not exceeded its lifetime limits.
	/// </summary>
	/// <param name="context">The context to validate</param>
	/// <param name="maxLifetime">Maximum allowed lifetime for a context</param>
	/// <returns>True if the context is within lifetime limits</returns>
	bool ValidateContextLifetime(IChildContext context, TimeSpan maxLifetime);
}
