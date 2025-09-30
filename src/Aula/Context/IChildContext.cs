using Aula.Configuration;

namespace Aula.Context;

/// <summary>
/// Provides scoped child context for services without requiring Child parameters.
/// This interface enables child-aware operations within a secure, isolated scope.
/// </summary>
public interface IChildContext
{
	/// <summary>
	/// Gets the current child in this context scope.
	/// Returns null if no child has been set.
	/// </summary>
	Child? CurrentChild { get; }

	/// <summary>
	/// Sets the child for this context scope.
	/// Throws if a child has already been set (context is immutable once initialized).
	/// </summary>
	/// <param name="child">The child to set for this context</param>
	/// <exception cref="InvalidOperationException">If child context already set</exception>
	/// <exception cref="ArgumentNullException">If child is null</exception>
	void SetChild(Child child);

	/// <summary>
	/// Clears the child from this context.
	/// Should only be called during disposal or explicit cleanup.
	/// </summary>
	void ClearChild();

	/// <summary>
	/// Validates that a child context is properly set.
	/// Throws if no child is set in the current context.
	/// </summary>
	/// <exception cref="InvalidOperationException">If no child context is set</exception>
	void ValidateContext();

	/// <summary>
	/// Gets a unique identifier for this context scope.
	/// Used for tracking and audit purposes.
	/// </summary>
	Guid ContextId { get; }

	/// <summary>
	/// Gets the timestamp when this context was created.
	/// </summary>
	DateTimeOffset CreatedAt { get; }
}
