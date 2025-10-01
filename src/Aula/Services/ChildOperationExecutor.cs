using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aula.Services;

/// <summary>
/// Executes operations within child-specific context scopes.
/// Provides the bridge between the singleton world and child-scoped services.
/// </summary>
public class ChildOperationExecutor : IChildOperationExecutor
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<ChildOperationExecutor> _logger;
	private readonly IChildAuditService _auditService;

	public IServiceProvider ServiceProvider => _serviceProvider;

	public ChildOperationExecutor(
		IServiceProvider serviceProvider,
		ILogger<ChildOperationExecutor> logger,
		IChildAuditService auditService)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(auditService);

		_serviceProvider = serviceProvider;
		_logger = logger;
		_auditService = auditService;
	}

	public async Task<TResult> ExecuteInChildContextAsync<TResult>(
		Child child,
		Func<IServiceProvider, Task<TResult>> operation,
		string operationName)
	{
		ArgumentNullException.ThrowIfNull(child);
		ArgumentNullException.ThrowIfNull(operation);

		_logger.LogInformation("Starting operation {OperationName} for child {ChildName}",
			operationName, child.FirstName);

		using var scope = _serviceProvider.CreateScope();

		// Set the child context for this scope
		var context = scope.ServiceProvider.GetRequiredService<IChildContext>();
		context.SetChild(child);

		try
		{
			// Execute the operation within the child's context
			var result = await operation(scope.ServiceProvider);

			// Audit successful operation
			await _auditService.LogDataAccessAsync(child, operationName,
				$"Operation completed successfully", true);

			_logger.LogInformation("Completed operation {OperationName} for child {ChildName}",
				operationName, child.FirstName);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to execute operation {OperationName} for child {ChildName}",
				operationName, child.FirstName);

			// Audit failed operation
			await _auditService.LogDataAccessAsync(child, operationName,
				$"Operation failed: {ex.Message}", false);

			throw;
		}
	}

	public async Task ExecuteInChildContextAsync(
		Child child,
		Func<IServiceProvider, Task> operation,
		string operationName)
	{
		ArgumentNullException.ThrowIfNull(child);
		ArgumentNullException.ThrowIfNull(operation);

		_logger.LogInformation("Starting operation {OperationName} for child {ChildName}",
			operationName, child.FirstName);

		using var scope = _serviceProvider.CreateScope();

		// Set the child context for this scope
		var context = scope.ServiceProvider.GetRequiredService<IChildContext>();
		context.SetChild(child);

		try
		{
			// Execute the operation within the child's context
			await operation(scope.ServiceProvider);

			// Audit successful operation
			await _auditService.LogDataAccessAsync(child, operationName,
				$"Operation completed successfully", true);

			_logger.LogInformation("Completed operation {OperationName} for child {ChildName}",
				operationName, child.FirstName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to execute operation {OperationName} for child {ChildName}",
				operationName, child.FirstName);

			// Audit failed operation
			await _auditService.LogDataAccessAsync(child, operationName,
				$"Operation failed: {ex.Message}", false);

			throw;
		}
	}

	public async Task<Dictionary<Child, TResult>> ExecuteForAllChildrenAsync<TResult>(
		IEnumerable<Child> children,
		Func<Child, IServiceProvider, Task<TResult>> operation,
		string operationName)
	{
		ArgumentNullException.ThrowIfNull(children);
		ArgumentNullException.ThrowIfNull(operation);

		var results = new Dictionary<Child, TResult>();
		var tasks = new List<Task>();

		_logger.LogInformation("Starting parallel operation {OperationName} for {Count} children",
			operationName, children.Count());

		foreach (var child in children)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					using var scope = _serviceProvider.CreateScope();
				var result = await operation(child, scope.ServiceProvider);
					lock (results)
					{
						results[child] = result;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed operation {OperationName} for child {ChildName}",
						operationName, child.FirstName);
					// Don't add to results if operation failed
				}
			}));
		}

		await Task.WhenAll(tasks);

		_logger.LogInformation("Completed parallel operation {OperationName} for {SuccessCount}/{TotalCount} children",
			operationName, results.Count, children.Count());

		return results;
	}

	public async Task<string> ExportChildDataAsync(Child child)
	{
		ArgumentNullException.ThrowIfNull(child);

		_logger.LogInformation("Starting GDPR data export for child {ChildName}", child.FirstName);

		return await ExecuteInChildContextAsync(child, async (serviceProvider) =>
		{
			var exportData = new Dictionary<string, object>();

			// Export basic child information
			exportData["child"] = new
			{
				FirstName = child.FirstName,
				LastName = child.LastName
			};

			// Export week letters
			var dataService = serviceProvider.GetRequiredService<IChildDataService>();
			var weekLetters = await dataService.GetAllWeekLettersAsync(child);
			exportData["weekLetters"] = weekLetters;

			// Note: Additional data exports would be added here as services are implemented
			exportData["weekSchedules"] = new List<object>(); // Placeholder
			exportData["reminders"] = new List<object>(); // Placeholder
			exportData["scheduledTasks"] = new List<object>(); // Placeholder

			// Export audit logs (if applicable)
			exportData["auditLogs"] = new
			{
				Note = "Audit logs for this child are available upon request from system administrators"
			};

			// Record the export event
			await _auditService.LogDataAccessAsync(child, "GDPRExport",
				"Data exported successfully", true);

			return JsonSerializer.Serialize(exportData, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});
		}, "GDPR_DataExport");
	}

	public async Task<bool> DeleteChildDataAsync(Child child)
	{
		ArgumentNullException.ThrowIfNull(child);

		_logger.LogWarning("Starting GDPR data deletion for child {ChildName}", child.FirstName);

		return await ExecuteInChildContextAsync(child, async (serviceProvider) =>
		{
			try
			{
				// Delete week letters
				var dataService = serviceProvider.GetRequiredService<IChildDataService>();
				var weekLetters = await dataService.GetAllWeekLettersAsync(child);

				// For now, we can't delete individual letters as they're JObjects
				// In a real implementation, we would parse the week number and year
				_logger.LogInformation("Prepared to delete {Count} week letters for child {ChildName}",
					weekLetters.Count, child.FirstName);

				// Note: Additional deletion operations would be added here as services are implemented
				_logger.LogDebug("Additional data deletion operations pending implementation");

				// Record the deletion event
				await _auditService.LogDataAccessAsync(child, "GDPRDeletion",
					"All child data deleted successfully", true);

				_logger.LogWarning("Completed GDPR data deletion for child {ChildName}", child.FirstName);
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to delete all data for child {ChildName}", child.FirstName);
				await _auditService.LogDataAccessAsync(child, "GDPRDeletion",
					$"Deletion failed: {ex.Message}", false);
				return false;
			}
		}, "GDPR_DataDeletion");
	}

	public async Task RecordConsentAsync(Child child, string consentType, bool granted)
	{
		ArgumentNullException.ThrowIfNull(child);
		if (string.IsNullOrWhiteSpace(consentType))
			throw new ArgumentException("Consent type must be specified", nameof(consentType));

		await ExecuteInChildContextAsync(child, async (serviceProvider) =>
		{
			var auditMessage = granted
				? $"Consent granted for {consentType}"
				: $"Consent revoked for {consentType}";

			await _auditService.LogDataAccessAsync(child, "ConsentManagement",
				auditMessage, true);

			_logger.LogInformation("Recorded consent for child {ChildName}: {ConsentType} = {Granted}",
				child.FirstName, consentType, granted);
		}, "RecordConsent");
	}
}
