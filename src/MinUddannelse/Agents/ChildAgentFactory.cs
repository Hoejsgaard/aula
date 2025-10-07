using System;
using MinUddannelse.Content.WeekLetters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinUddannelse.Configuration;
using MinUddannelse.AI.Services;
using MinUddannelse.Scheduling;

namespace MinUddannelse.Agents;

public class ChildAgentFactory : IChildAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Config _config;

    public ChildAgentFactory(IServiceProvider serviceProvider, Config config)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public IChildAgent CreateChildAgent(Child child, ISchedulingService schedulingService)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (schedulingService == null) throw new ArgumentNullException(nameof(schedulingService));

        var openAiService = _serviceProvider.GetRequiredService<IOpenAiService>();
        var weekLetterService = _serviceProvider.GetRequiredService<IWeekLetterService>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        var postWeekLettersOnStartup = _config.WeekLetter?.PostOnStartup ?? false;

        return new ChildAgent(
            child,
            openAiService,
            weekLetterService,
            postWeekLettersOnStartup,
            schedulingService,
            loggerFactory);
    }
}
