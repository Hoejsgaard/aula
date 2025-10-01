using Aula.Configuration;

namespace Aula.Services;

/// <summary>
/// Executes operations within a child-specific context scope.
/// Ensures all child operations are properly isolated and audited.
/// </summary>
public interface IChildOperationExecutor
{
	/// <summary>
	/// Gets the service provider for creating service scopes.
	/// </summary>
	IServiceProvider ServiceProvider { get; }

	/// <summary>
	/// Executes an operation within a child's context scope.
	/// </summary>
	/// <typeparam name="TResult">The result type of the operation</typeparam>
	/// <param name="child">The child context for the operation</param>
	/// <param name="operation">The operation to execute</param>
	/// <param name="operationName">Name of the operation for logging</param>
	/// <returns>The result of the operation</returns>
	Task<TResult> ExecuteInChildContextAsync<TResult>(
		Child child,
		Func<IServiceProvider, Task<TResult>> operation,
		string operationName);

	/// <summary>
	/// Executes an operation within a child's context scope without returning a result.
	/// </summary>
	/// <param name="child">The child context for the operation</param>
	/// <param name="operation">The operation to execute</param>
	/// <param name="operationName">Name of the operation for logging</param>
	Task ExecuteInChildContextAsync(
		Child child,
		Func<IServiceProvider, Task> operation,
		string operationName);

	/// <summary>
	/// Executes operations for all children in parallel with isolated contexts.
	/// </summary>
	/// <typeparam name="TResult">The result type of the operation</typeparam>
	/// <param name="children">The children to execute operations for</param>
	/// <param name="operation">The operation to execute for each child</param>
	/// <param name="operationName">Name of the operation for logging</param>
	/// <returns>Dictionary of results keyed by child</returns>
	Task<Dictionary<Child, TResult>> ExecuteForAllChildrenAsync<TResult>(
		IEnumerable<Child> children,
		Func<Child, IServiceProvider, Task<TResult>> operation,
		string operationName);

	/// <summary>
	/// Executes GDPR-compliant data export for a child.
	/// </summary>
	/// <param name="child">The child to export data for</param>
	/// <returns>Exported data in JSON format</returns>
	Task<string> ExportChildDataAsync(Child child);

	/// <summary>
	/// Executes GDPR-compliant data deletion for a child.
	/// </summary>
	/// <param name="child">The child to delete data for</param>
	/// <returns>True if deletion was successful</returns>
	Task<bool> DeleteChildDataAsync(Child child);

	/// <summary>
	/// Records consent for a child's data processing.
	/// </summary>
	/// <param name="child">The child to record consent for</param>
	/// <param name="consentType">Type of consent being recorded</param>
	/// <param name="granted">Whether consent was granted</param>
	Task RecordConsentAsync(Child child, string consentType, bool granted);
}
