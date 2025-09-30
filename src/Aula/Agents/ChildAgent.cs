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
	private ChildAwareSlackInteractiveBot? _slackBot;
	private TelegramInteractiveBot? _telegramBot;

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

		// Start Slack bot if configured for this child
		if (_child.Channels?.Slack?.Enabled == true &&
			_child.Channels?.Slack?.EnableInteractiveBot == true &&
			!string.IsNullOrEmpty(_child.Channels?.Slack?.ApiToken))
		{
			_logger.LogInformation("Starting ChildAwareSlackInteractiveBot for {ChildName} on channel {ChannelId}",
				_child.FirstName, _child.Channels!.Slack!.ChannelId);

			_slackBot = new ChildAwareSlackInteractiveBot(
				_serviceProvider,
				_serviceProvider.GetRequiredService<IChildServiceCoordinator>(),
				_config,
				_loggerFactory);

			await _slackBot.StartForChild(_child);

			_logger.LogInformation("ChildAwareSlackInteractiveBot started successfully for {ChildName}", _child.FirstName);
		}

		// Start Telegram bot if configured globally (but handle this child's messages)
		if (_config.Telegram?.Enabled == true && !string.IsNullOrEmpty(_config.Telegram.Token))
		{
			_logger.LogInformation("Starting Telegram bot handler for child {ChildName}", _child.FirstName);

			var agentService = _serviceProvider.GetRequiredService<IAgentService>();
			var supabaseService = _serviceProvider.GetRequiredService<ISupabaseService>();

			_telegramBot = new TelegramInteractiveBot(agentService, _config, _loggerFactory, supabaseService);
			await _telegramBot.Start();

			_logger.LogInformation("Telegram bot started for child {ChildName}", _child.FirstName);
		}

		// Subscribe to week letter events for this child
		if (_schedulingService is SchedulingService schedService)
		{
			schedService.ChildWeekLetterReady += async (sender, args) =>
			{
				// Only process events for THIS child
				if (!args.ChildFirstName.Equals(_child.FirstName, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}

				_logger.LogInformation("Received week letter event for child: {ChildName}", args.ChildFirstName);

				// Process within a child context scope
				using (var scope = new ChildContextScope(_serviceProvider, _child))
				{
					try
					{
						// Check if this child has Slack configured
						var slackWebhook = _child.Channels?.Slack?.WebhookUrl;

						if (!string.IsNullOrEmpty(slackWebhook) && args.WeekLetter != null)
						{
							// Create a SlackBot instance with the child's webhook
							var slackBot = new SlackBot(slackWebhook);

							// Post week letter to Slack
							var success = await slackBot.PostWeekLetter(args.WeekLetter, _child);

							if (success)
							{
								_logger.LogInformation("Posted week letter to Slack for {ChildName}", args.ChildFirstName);
							}
							else
							{
								_logger.LogError("Failed to post week letter to Slack for {ChildName}", args.ChildFirstName);
							}
						}
						else if (args.WeekLetter != null)
						{
							_logger.LogWarning("Slack not configured for {ChildName} - no webhook URL", args.ChildFirstName);
						}
						else
						{
							_logger.LogWarning("No week letter to post for {ChildName}", args.ChildFirstName);
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error processing week letter event for child: {ChildName}", args.ChildFirstName);
					}
				}
			};
		}

		// Post week letters on startup if configured
		if (_config.Features?.PostWeekLettersOnStartup == true && _schedulingService is SchedulingService startupSchedService)
		{
			_logger.LogInformation("📬 Posting current week letters on startup for {ChildName}", _child.FirstName);

			// Get current week and year
			var now = DateTime.Now;
			var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(now);
			var year = now.Year;

			try
			{
				// Get the week letter from cache/database/API using child coordinator
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

					// Emit the ChildWeekLetterReady event
					var childId = _child.FirstName.ToLowerInvariant().Replace(" ", "_");
					var eventArgs = new ChildWeekLetterEventArgs(
						childId,
						_child.FirstName,
						weekNumber,
						year,
						weekLetter);

					// Fire the event - the event handlers will post to Slack
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

		await Task.CompletedTask;
	}

	public async Task StopAsync()
	{
		_logger.LogInformation("Stopping agent for child {ChildName}", _child.FirstName);

		_slackBot?.Dispose();

		_telegramBot?.Stop();

		await Task.CompletedTask;
	}
}
