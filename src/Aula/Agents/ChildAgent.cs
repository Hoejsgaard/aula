using System;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.Services;
using Aula.Scheduling;
using Aula.Bots;
using Aula.Channels;
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
	private ChildAwareSlackInteractiveBot? _slackBot;
	private TelegramInteractiveBot? _telegramBot;
	private EventHandler<ChildWeekLetterEventArgs>? _weekLetterHandler;

	public ChildAgent(
		Child child,
		IServiceProvider serviceProvider,
		Config config,
		ISchedulingService schedulingService,
		ILoggerFactory loggerFactory)
	{
		_child = child ?? throw new ArgumentNullException(nameof(child));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_schedulingService = schedulingService ?? throw new ArgumentNullException(nameof(schedulingService));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_logger = _loggerFactory.CreateLogger<ChildAgent>();
	}

	public async Task StartAsync()
	{
		_logger.LogInformation("Starting agent for child {ChildName}", _child.FirstName);

		await InitializeSlackBotAsync();
		await InitializeTelegramBotAsync();
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

		_slackBot?.Dispose();

		_telegramBot?.Stop();

		await Task.CompletedTask;
	}

	private async Task InitializeSlackBotAsync()
	{
		// Start Slack bot if configured for this child
		if (_child.Channels?.Slack?.Enabled == true &&
			_child.Channels?.Slack?.EnableInteractiveBot == true &&
			!string.IsNullOrEmpty(_child.Channels?.Slack?.ApiToken))
		{
			_logger.LogInformation("Starting ChildAwareSlackInteractiveBot for {ChildName} on channel {ChannelId}",
				_child.FirstName, _child.Channels!.Slack!.ChannelId);

			_slackBot = new ChildAwareSlackInteractiveBot(
				_serviceProvider,
				_serviceProvider.GetRequiredService<IChildAwareOpenAiService>(),
				_config,
				_loggerFactory);

			await _slackBot.StartForChild(_child);

			_logger.LogInformation("ChildAwareSlackInteractiveBot started successfully for {ChildName}", _child.FirstName);
		}
	}

	private async Task InitializeTelegramBotAsync()
	{
		// Start Telegram bot if configured for this child
		if (_child.Channels?.Telegram?.Enabled == true &&
			!string.IsNullOrEmpty(_child.Channels?.Telegram?.Token))
		{
			_logger.LogInformation("Starting Telegram bot handler for child {ChildName}", _child.FirstName);

			var agentService = _serviceProvider.GetRequiredService<IAgentService>();
			var supabaseService = _serviceProvider.GetRequiredService<ISupabaseService>();

			_telegramBot = new TelegramInteractiveBot(agentService, _config, _loggerFactory, supabaseService);
			await _telegramBot.Start();

			_logger.LogInformation("Telegram bot started for child {ChildName}", _child.FirstName);
		}
	}

	private void SubscribeToWeekLetterEvents()
	{
		if (_schedulingService is SchedulingService schedService)
		{
			_weekLetterHandler = async (sender, args) =>
			{
				var logger = _serviceProvider.GetRequiredService<ILogger<ChildWeekLetterHandler>>();
				var handler = new ChildWeekLetterHandler(_child, logger);
				await handler.HandleWeekLetterEventAsync(args, _slackBot);
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
			var dataService = _serviceProvider.GetRequiredService<IChildDataService>();
			var date = DateOnly.FromDateTime(now.AddDays(-7 * (System.Globalization.ISOWeek.GetWeekOfYear(now) - weekNumber)));
			var weekLetter = await dataService.GetOrFetchWeekLetterAsync(_child, date, true);

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
