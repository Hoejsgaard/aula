using System;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.Services;
using Aula.Scheduling;
using Aula.Bots;
using Aula.Channels;
using Aula.Context;
using Aula.Events;
using Aula.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Agents;

public class ChildAgent : IChildAgent
{
	private readonly Child _child;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILoggerFactory _loggerFactory;
	private readonly ILogger<ChildAgent> _logger;
	private readonly Config _config;
	private readonly ISchedulingService _schedulingService;
	private readonly IChannelManager _channelManager;
	private EventHandler<ChildWeekLetterEventArgs>? _weekLetterHandler;

	public ChildAgent(
		Child child,
		IServiceProvider serviceProvider,
		Config config,
		ISchedulingService schedulingService,
		IChannelManager channelManager,
		ILoggerFactory loggerFactory)
	{
		_child = child ?? throw new ArgumentNullException(nameof(child));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
		_channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_logger = _loggerFactory.CreateLogger<ChildAgent>();
	}

	public async Task StartAsync()
	{
		_logger.LogInformation("Starting agent for child {ChildName}", _child.FirstName);

		await InitializeChannelsAsync();
		SubscribeToWeekLetterEvents();

		if (_config.Features?.PostWeekLettersOnStartup == true)
		{
			await PostStartupWeekLetterAsync();
		}
	}

	public async Task StopAsync()
	{
		_logger.LogInformation("Stopping agent for child {ChildName}", _child.FirstName);

		// Unsubscribe from events to prevent memory leak
		if (_weekLetterHandler != null && _schedulingService is SchedulingService schedService)
		{
			schedService.ChildWeekLetterReady -= _weekLetterHandler;
			_weekLetterHandler = null;
		}

		await _channelManager.StopAllChannelsAsync();

		await Task.CompletedTask;
	}

	private async Task InitializeChannelsAsync()
	{
		_logger.LogInformation("Initializing channels for child {ChildName}", _child.FirstName);

		try
		{
			await _channelManager.InitializeAllChannelsAsync();
			await _channelManager.StartAllChannelsAsync();

			_logger.LogInformation("Channels initialized successfully for child {ChildName}", _child.FirstName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to initialize channels for child {ChildName}", _child.FirstName);
			throw;
		}
	}

	private void SubscribeToWeekLetterEvents()
	{
		if (_schedulingService is SchedulingService schedService)
		{
			_weekLetterHandler = async (sender, args) =>
			{
				using (var scope = new ChildContextScope(_serviceProvider, _child))
				{
					await scope.ExecuteAsync(async provider =>
					{
						var channelManager = provider.GetRequiredService<IChildChannelManager>();
						var logger = provider.GetRequiredService<ILogger<ChildWeekLetterHandler>>();

						var handler = new ChildWeekLetterHandler(_child, channelManager, logger);
						await handler.HandleWeekLetterEventAsync(args);
					});
				}
			};
			schedService.ChildWeekLetterReady += _weekLetterHandler;
		}
	}

	private async Task PostStartupWeekLetterAsync()
	{
		if (!(_schedulingService is SchedulingService startupSchedService))
			return;

		_logger.LogInformation("📬 Posting current week letters on startup for {ChildName}", _child.FirstName);

		var now = DateTime.Now;
		var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(now);
		var year = now.Year;

		try
		{
			JObject? weekLetter = null;
			using (var scope = new ChildContextScope(_serviceProvider, _child))
			{
				await scope.ExecuteAsync(async provider =>
				{
					var dataService = provider.GetRequiredService<IChildDataService>();
					var date = DateOnly.FromDateTime(now.AddDays(-7 * (System.Globalization.ISOWeek.GetWeekOfYear(now) - weekNumber)));
					weekLetter = await dataService.GetOrFetchWeekLetterAsync(date, true);
				});
			}

			if (weekLetter != null)
			{
				_logger.LogInformation("📨 Emitting week letter event for {ChildName} (week {WeekNumber}/{Year})",
					_child.FirstName, weekNumber, year);

				var childId = _child.FirstName.ToLowerInvariant().Replace(" ", "_");
				var eventArgs = new ChildWeekLetterEventArgs(
					childId,
					_child.FirstName,
					weekNumber,
					year,
					weekLetter);

				startupSchedService.TriggerChildWeekLetterReady(eventArgs);
			}
			else
			{
				_logger.LogWarning("No week letter found for {ChildName} (week {WeekNumber}/{Year})",
					_child.FirstName, weekNumber, year);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error posting week letter on startup for {ChildName}", _child.FirstName);
		}
	}
}
