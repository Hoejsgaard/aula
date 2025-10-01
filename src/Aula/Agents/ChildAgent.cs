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
    private readonly IOpenAiService _openAiService;
    private readonly ILogger<ChildWeekLetterHandler> _weekLetterHandlerLogger;
    private readonly IWeekLetterService _weekLetterService;
    private readonly bool _postWeekLettersOnStartup;
    private readonly ISchedulingService _schedulingService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ChildAgent> _logger;
    private SlackInteractiveBot? _slackBot;
    private EventHandler<ChildWeekLetterEventArgs>? _weekLetterHandler;

    public ChildAgent(
        Child child,
        IOpenAiService openAiService,
        ILogger<ChildWeekLetterHandler> weekLetterHandlerLogger,
        IWeekLetterService weekLetterService,
        bool postWeekLettersOnStartup,
        ISchedulingService schedulingService,
        ILoggerFactory loggerFactory)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _weekLetterHandlerLogger = weekLetterHandlerLogger ?? throw new ArgumentNullException(nameof(weekLetterHandlerLogger));
        _weekLetterService = weekLetterService ?? throw new ArgumentNullException(nameof(weekLetterService));
        _postWeekLettersOnStartup = postWeekLettersOnStartup;
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

        if (_postWeekLettersOnStartup)
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


        await Task.CompletedTask;
    }

    private async Task InitializeSlackBotAsync()
    {
        // Start Slack bot if configured for this child
        if (_child.Channels?.Slack?.Enabled == true &&
            _child.Channels?.Slack?.EnableInteractiveBot == true &&
            !string.IsNullOrEmpty(_child.Channels?.Slack?.ApiToken))
        {
            _logger.LogInformation("Starting SlackInteractiveBot for {ChildName} on channel {ChannelId}",
                _child.FirstName, _child.Channels!.Slack!.ChannelId);

            _slackBot = new SlackInteractiveBot(
                _openAiService,
                _loggerFactory);

            await _slackBot.StartForChild(_child);

            _logger.LogInformation("SlackInteractiveBot started successfully for {ChildName}", _child.FirstName);
        }
    }

    private async Task InitializeTelegramBotAsync()
    {
        if (_child.Channels?.Telegram?.Enabled == true &&
            _child.Channels?.Telegram?.EnableInteractiveBot == true &&
            !string.IsNullOrEmpty(_child.Channels?.Telegram?.Token))
        {
            _logger.LogInformation("Starting Telegram bot for {ChildName} with token ending in {TokenSuffix}",
                _child.FirstName, _child.Channels!.Telegram!.Token.Substring(Math.Max(0, _child.Channels.Telegram.Token.Length - 4)));

            _logger.LogInformation("Telegram bot initialized for {ChildName} - using shared TelegramMessageHandler", _child.FirstName);
        }
        else
        {
            _logger.LogInformation("Telegram bot disabled for {ChildName} - not configured or not enabled", _child.FirstName);
        }

        await Task.CompletedTask;
    }

    private void SubscribeToWeekLetterEvents()
    {
        if (_schedulingService is SchedulingService schedService)
        {
            _weekLetterHandler = async (sender, args) =>
            {
                var handler = new ChildWeekLetterHandler(_child, _weekLetterHandlerLogger);
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
            var date = DateOnly.FromDateTime(now.AddDays(-7 * (System.Globalization.ISOWeek.GetWeekOfYear(now) - weekNumber)));
            var weekLetter = await _weekLetterService.GetOrFetchWeekLetterAsync(_child, date, true);

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
