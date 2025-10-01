using Aula.Channels;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Repositories;
using Aula.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Services;

/// <summary>
/// Coordinates all child-specific operations across the application.
/// Ensures all operations happen within proper child context scopes.
/// </summary>
public class ChildServiceCoordinator : IChildServiceCoordinator
{
	private readonly IChildDataService _dataService;
	private readonly IAgentService _agentService;
	private readonly Config _config;
	private readonly ILogger<ChildServiceCoordinator> _logger;
	private readonly IServiceProvider _serviceProvider;

	public ChildServiceCoordinator(
		IChildDataService dataService,
		IAgentService agentService,
		Config config,
		ILogger<ChildServiceCoordinator> logger,
		IServiceProvider serviceProvider)
	{
		_dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
		_agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	/// <summary>
	/// Helper method to execute operations within a child context scope.
	/// This replaces the executor pattern while maintaining proper child context isolation.
	/// </summary>
	private async Task<T> ExecuteInChildScopeAsync<T>(Child child, Func<IServiceProvider, Task<T>> operation)
	{
		using var scope = _serviceProvider.CreateScope();
		var childContext = scope.ServiceProvider.GetRequiredService<IChildContext>();
		childContext.SetChild(child);
		return await operation(scope.ServiceProvider);
	}

	/// <summary>
	/// Helper method to execute operations within a child context scope (void return).
	/// </summary>
	private async Task ExecuteInChildScopeAsync(Child child, Func<IServiceProvider, Task> operation)
	{
		using var scope = _serviceProvider.CreateScope();
		var childContext = scope.ServiceProvider.GetRequiredService<IChildContext>();
		childContext.SetChild(child);
		await operation(scope.ServiceProvider);
	}

	public async Task PreloadWeekLettersForAllChildrenAsync()
	{
		_logger.LogInformation("Starting week letter preload for all children");

		var allChildren = await _agentService.GetAllChildrenAsync();
		var today = DateOnly.FromDateTime(DateTime.Today);
		var successCount = 0;

		foreach (var child in allChildren)
		{
			try
			{
				// Fetch current week
				var currentWeek = await _dataService.GetOrFetchWeekLetterAsync(child, today, true);

				// Fetch past 2 weeks
				var lastWeek = await _dataService.GetOrFetchWeekLetterAsync(child, today.AddDays(-7), true);
				var twoWeeksAgo = await _dataService.GetOrFetchWeekLetterAsync(child, today.AddDays(-14), true);

				successCount++;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to preload week letters for child {ChildName}", child.FirstName);
			}
		}

		_logger.LogInformation("Completed week letter preload for {Count}/{Total} children",
			successCount, allChildren.Count());
	}

	public async Task PostWeekLettersToChannelsAsync()
	{
		_logger.LogInformation("Posting week letters to channels for all children");

		var allChildren = await _agentService.GetAllChildrenAsync();
		var today = DateOnly.FromDateTime(DateTime.Today);
		var successCount = 0;

		foreach (var child in allChildren)
		{
			try
			{
				// Calculate ISO week number
				var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
				var weekNumber = calendar.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue),
					System.Globalization.CalendarWeekRule.FirstFourDayWeek,
					DayOfWeek.Monday);

				var weekLetter = await _dataService.GetWeekLetterAsync(child, weekNumber, today.Year);

				if (weekLetter == null)
				{
					continue;
				}

				// Format the week letter content
				var message = $"📚 Ugebrev for uge {weekNumber}\n\n{weekLetter.ToString()}";

				// Send to all configured channels for this child (requires child context)
				var success = await ExecuteInChildScopeAsync(child, async serviceProvider =>
				{
					var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();
					return await channelManager.SendMessageAsync(message, MessageFormat.Markdown);
				});

				if (success)
				{
					successCount++;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to post week letter for child {ChildName}", child.FirstName);
			}
		}

		_logger.LogInformation("Posted week letters for {Success}/{Total} children",
			successCount, allChildren.Count());
	}

	public async Task<bool> FetchWeekLetterForChildAsync(Child child, DateOnly date)
	{
		try
		{
			var letter = await _dataService.GetOrFetchWeekLetterAsync(child, date, true);
			return letter != null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to fetch week letter for child {ChildName} on {Date}", child.FirstName, date);
			return false;
		}
	}

	public async Task<JObject?> GetWeekLetterForChildAsync(Child child, DateOnly date)
	{
		try
		{
			return await _dataService.GetOrFetchWeekLetterAsync(child, date, true);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get week letter for child {ChildName} on {Date}", child.FirstName, date);
			return null;
		}
	}

	public async Task<IEnumerable<(Child child, JObject? weekLetter)>> FetchWeekLettersForAllChildrenAsync(DateOnly date)
	{
		var allChildren = await _agentService.GetAllChildrenAsync();
		var results = new List<(Child child, JObject? weekLetter)>();

		foreach (var child in allChildren)
		{
			try
			{
				var letter = await _dataService.GetOrFetchWeekLetterAsync(child, date, true);
				results.Add((child, letter));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to fetch week letter for child {ChildName} on {Date}", child.FirstName, date);
				results.Add((child, null));
			}
		}

		return results;
	}

	public async Task ProcessScheduledTasksForChildAsync(Child child)
	{
		try
		{
			await ExecuteInChildScopeAsync(child, async serviceProvider =>
			{
				var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
				await scheduler.ProcessDueTasksAsync();
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process scheduled tasks for child {ChildName}", child.FirstName);
		}
	}

	public async Task ProcessScheduledTasksForAllChildrenAsync()
	{
		var allChildren = await _agentService.GetAllChildrenAsync();

		foreach (var child in allChildren)
		{
			try
			{
				await ExecuteInChildScopeAsync(child, async serviceProvider =>
				{
					var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
					await scheduler.ProcessDueTasksAsync();
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to process scheduled tasks for child {ChildName}", child.FirstName);
			}
		}
	}

	public async Task<bool> SendReminderToChildAsync(Child child, string reminderMessage)
	{
		try
		{
			return await ExecuteInChildScopeAsync(child, async serviceProvider =>
			{
				var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();
				return await channelManager.SendReminderAsync(reminderMessage);
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send reminder to child {ChildName}", child.FirstName);
			return false;
		}
	}

	public async Task<string> ProcessAiQueryForChildAsync(Child child, string query)
	{
		try
		{
			return await ExecuteInChildScopeAsync(child, async serviceProvider =>
			{
				var aiService = serviceProvider.GetRequiredService<IChildAwareOpenAiService>();
				var response = await aiService.GetResponseAsync(query);
				return response ?? "Jeg kunne ikke behandle din forespørgsel.";
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process AI query for child {ChildName}", child.FirstName);
			return "Jeg kunne ikke behandle din forespørgsel.";
		}
	}

	public async Task SeedHistoricalDataForChildAsync(Child child, int weeksBack = 12)
	{
		try
		{
			var today = DateOnly.FromDateTime(DateTime.Today);

			for (int i = 0; i < weeksBack; i++)
			{
				var date = today.AddDays(-7 * i);
				await _dataService.GetOrFetchWeekLetterAsync(child, date, false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to seed historical data for child {ChildName}", child.FirstName);
		}
	}

	public async Task SeedHistoricalDataForAllChildrenAsync(int weeksBack = 12)
	{
		var allChildren = await _agentService.GetAllChildrenAsync();
		var today = DateOnly.FromDateTime(DateTime.Today);

		foreach (var child in allChildren)
		{
			try
			{
				for (int i = 0; i < weeksBack; i++)
				{
					var date = today.AddDays(-7 * i);
					await _dataService.GetOrFetchWeekLetterAsync(child, date, false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to seed historical data for child {ChildName}", child.FirstName);
			}
		}
	}

	public async Task<DateTime?> GetNextScheduledTaskTimeForChildAsync(Child child)
	{
		try
		{
			return await ExecuteInChildScopeAsync(child, async serviceProvider =>
			{
				var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
				var tasks = await scheduler.GetScheduledTasksAsync();

				var nextTask = tasks
					.Where(t => t.Enabled && t.NextRun.HasValue)
					.OrderBy(t => t.NextRun)
					.Select(t => t.NextRun!.Value)
					.Cast<DateTime?>()
					.FirstOrDefault();

				return nextTask;
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get next scheduled task time for child {ChildName}", child.FirstName);
			return null;
		}
	}

	public async Task<bool> ValidateChildServicesAsync()
	{
		try
		{
			_logger.LogInformation("Validating child-aware services");

			// Check if we can get all children
			var allChildren = await _agentService.GetAllChildrenAsync();
			if (!allChildren.Any())
			{
				_logger.LogWarning("No children configured");
				return false;
			}

			// Test creating a scope for the first child
			var testChild = allChildren.First();
			var testResult = await ExecuteInChildScopeAsync(testChild, serviceProvider =>
			{
				// Verify all required services can be resolved
				var requiredServices = new[]
				{
					typeof(IChildContext),
					typeof(IChildDataService),
					typeof(IChildChannelManager),
					typeof(IChildScheduler),
					typeof(IChildAwareOpenAiService)
				};

				foreach (var serviceType in requiredServices)
				{
					var service = serviceProvider.GetService(serviceType);
					if (service == null)
					{
						_logger.LogError("Failed to resolve service {ServiceType}", serviceType.Name);
						return Task.FromResult(false);
					}
				}

				// Verify context is properly set
				var context = serviceProvider.GetRequiredService<IChildContext>();
				if (context.CurrentChild == null ||
					context.CurrentChild.FirstName != testChild.FirstName)
				{
					_logger.LogError("Child context not properly set");
					return Task.FromResult(false);
				}

				return Task.FromResult(true);
			});

			return testResult;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to validate child services");
			return false;
		}
	}

	public async Task<Dictionary<string, bool>> GetChildServicesHealthAsync()
	{
		var health = new Dictionary<string, bool>();

		try
		{
			// Check AgentService
			var allChildren = await _agentService.GetAllChildrenAsync();
			health["AgentService"] = allChildren.Any();

			if (allChildren.Any())
			{
				var testChild = allChildren.First();

				// Check each service
				var serviceChecks = await ExecuteInChildScopeAsync(testChild, async serviceProvider =>
				{
					var results = new Dictionary<string, bool>();

					// Check IChildContext
					try
					{
						var context = serviceProvider.GetRequiredService<IChildContext>();
						results["ChildContext"] = context.CurrentChild != null;
					}
					catch { results["ChildContext"] = false; }

					// Check IChildDataService
					try
					{
						var dataService = serviceProvider.GetRequiredService<IChildDataService>();
						results["ChildDataService"] = dataService != null;
					}
					catch { results["ChildDataService"] = false; }

					// Check IChildChannelManager
					try
					{
						var channelManager = serviceProvider.GetRequiredService<IChildChannelManager>();
						var hasChannels = await channelManager.HasConfiguredChannelsAsync();
						results["ChildChannelManager"] = true;
						results["ConfiguredChannels"] = hasChannels;
					}
					catch { results["ChildChannelManager"] = false; }

					// Check IChildScheduler
					try
					{
						var scheduler = serviceProvider.GetRequiredService<IChildScheduler>();
						results["ChildScheduler"] = scheduler != null;
					}
					catch { results["ChildScheduler"] = false; }

					// Check IChildAwareOpenAiService
					try
					{
						var aiService = serviceProvider.GetRequiredService<IChildAwareOpenAiService>();
						results["ChildAwareOpenAiService"] = aiService != null;
					}
					catch { results["ChildAwareOpenAiService"] = false; }

					return results;
				});

				foreach (var kvp in serviceChecks)
				{
					health[kvp.Key] = kvp.Value;
				}
			}
			else
			{
				health["NoChildrenConfigured"] = false;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get service health");
			health["Error"] = false;
		}

		return health;
	}

	public async Task<IEnumerable<Child>> GetAllChildrenAsync()
	{
		return await _agentService.GetAllChildrenAsync();
	}

	public async Task PostWeekLettersForAllChildrenAsync(DateOnly date, Func<Child, JObject?, Task> postAction)
	{
		ArgumentNullException.ThrowIfNull(postAction);

		var allChildren = await _agentService.GetAllChildrenAsync();

		foreach (var child in allChildren)
		{
			try
			{
				var weekLetter = await _dataService.GetOrFetchWeekLetterAsync(child, date, true);
				await postAction(child, weekLetter);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to post week letter for child {ChildName} on {Date}", child.FirstName, date);
				await postAction(child, null);
			}
		}
	}

	public async Task PreloadChildrenWeekLetters(Config config, ILogger logger)
	{
		logger.LogInformation("📚 Starting week letter preload for current and recent weeks");

		var today = DateOnly.FromDateTime(DateTime.Today);
		var weeksToCheck = config.Features?.WeeksToPreload ?? 3;
		var successCount = 0;
		var totalAttempts = 0;

		for (int weeksBack = 0; weeksBack < weeksToCheck; weeksBack++)
		{
			var targetDate = today.AddDays(-7 * weeksBack);
			var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(targetDate.ToDateTime(TimeOnly.MinValue));
			var year = targetDate.Year;

			logger.LogInformation("📅 Checking week {WeekNumber}/{Year} (date: {Date})", weekNumber, year, targetDate);

			var results = await FetchWeekLettersForAllChildrenAsync(targetDate);

			foreach (var (child, weekLetter) in results)
			{
				totalAttempts++;
				if (weekLetter != null && weekLetter["ugebreve"] != null)
				{
					logger.LogInformation("✅ Preloaded week letter for {ChildName} week {WeekNumber}/{Year}",
						child.FirstName, weekNumber, year);
					successCount++;
				}
				else
				{
					logger.LogInformation("📭 No week letter available for {ChildName} week {WeekNumber}/{Year}",
						child.FirstName, weekNumber, year);
				}
			}
		}

		logger.LogInformation("📚 Week letter preload complete: {SuccessCount}/{TotalAttempts} successfully loaded",
			successCount, totalAttempts);
	}
}
