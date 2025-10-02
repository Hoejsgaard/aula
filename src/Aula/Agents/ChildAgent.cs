using System;
using Aula.Content.WeekLetters;
using System.Threading.Tasks;
using Aula.Configuration;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Scheduling;
using Aula.Communication.Bots;
using Aula.Communication.Channels;
using Aula.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Aula.Agents;

public class ChildAgent : IChildAgent
{
    private readonly Child _child;
    private readonly IOpenAiService _openAiService;
    private readonly IWeekLetterService _weekLetterService;
    private readonly bool _postWeekLettersOnStartup;
    private readonly ISchedulingService _schedulingService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private SlackInteractiveBot? _slackBot;
    private TelegramInteractiveBot? _telegramBot;
    private EventHandler<ChildWeekLetterEventArgs>? _weekLetterHandler;

    public ChildAgent(
        Child child,
        IOpenAiService openAiService,
        IWeekLetterService weekLetterService,
        bool postWeekLettersOnStartup,
        ISchedulingService schedulingService,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(openAiService);
        ArgumentNullException.ThrowIfNull(weekLetterService);
        ArgumentNullException.ThrowIfNull(schedulingService);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _child = child;
        _openAiService = openAiService;
        _weekLetterService = weekLetterService;
        _postWeekLettersOnStartup = postWeekLettersOnStartup;
        _schedulingService = schedulingService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ChildAgent>();
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Starting agent for child {ChildName}", _child.FirstName);

        await StartSlackBotAsync();
        await StartTelegramBotAsync();
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
        if (_weekLetterHandler != null)
        {
            _schedulingService.ChildWeekLetterReady -= _weekLetterHandler;
            _weekLetterHandler = null;
        }

        _slackBot?.Dispose();
        _telegramBot?.Dispose();

        await Task.CompletedTask;
    }

    private async Task StartSlackBotAsync()
    {
        if (_child.Channels?.Slack?.Enabled == true &&
            _child.Channels?.Slack?.EnableInteractiveBot == true &&
            !string.IsNullOrEmpty(_child.Channels?.Slack?.ApiToken))
        {
            _logger.LogInformation("Starting SlackInteractiveBot for {ChildName} on channel {ChannelId}",
                _child.FirstName, _child.Channels!.Slack!.ChannelId);

            _slackBot = new SlackInteractiveBot(
                _child,
                _openAiService,
                _loggerFactory);

            await _slackBot.Start();

            _logger.LogInformation("SlackInteractiveBot started successfully for {ChildName}", _child.FirstName);
        }
    }

    private async Task StartTelegramBotAsync()
    {
        if (_child.Channels?.Telegram?.Enabled == true &&
            _child.Channels?.Telegram?.EnableInteractiveBot == true &&
            !string.IsNullOrEmpty(_child.Channels?.Telegram?.Token))
        {
            _logger.LogInformation("Starting TelegramInteractiveBot for {ChildName}", _child.FirstName);

            _telegramBot = new TelegramInteractiveBot(
                _child,
                _openAiService,
                _loggerFactory);

            await _telegramBot.Start();

            _logger.LogInformation("TelegramInteractiveBot started successfully for {ChildName}", _child.FirstName);
        }
        else
        {
            _logger.LogInformation("Telegram bot disabled for {ChildName} - not configured or not enabled", _child.FirstName);
        }
    }

    private void SubscribeToWeekLetterEvents()
    {
        _weekLetterHandler = async (sender, args) =>
        {
            var handler = new ChildWeekLetterHandler(_child, _loggerFactory);
            await handler.HandleWeekLetterEventAsync(args, _slackBot, _telegramBot);
        };
        _schedulingService.ChildWeekLetterReady += _weekLetterHandler;
    }

    private async Task PostStartupWeekLetterAsync()
    {
        if (!(_schedulingService is SchedulingService startupSchedService))
            return;

        _logger.LogInformation("Posting current week letters on startup for {ChildName}", _child.FirstName);

        var now = DateTime.Now;
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(now);
        var year = now.Year;

        try
        {
            var date = DateOnly.FromDateTime(now.AddDays(-7 * (System.Globalization.ISOWeek.GetWeekOfYear(now) - weekNumber)));
            var weekLetter = await _weekLetterService.GetOrFetchWeekLetterAsync(_child, date, true);

            if (weekLetter != null)
            {
                _logger.LogInformation("Emitting week letter event for {ChildName} (week {WeekNumber}/{Year})",
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
