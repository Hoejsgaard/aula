using System;
using MinUddannelse.Content.WeekLetters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinUddannelse.Configuration;
using MinUddannelse.AI.Services;
using MinUddannelse.Scheduling;

namespace MinUddannelse.Agents;

/// <summary>
/// Factory implementation for creating ChildAgent instances with proper dependency resolution.
/// Encapsulates the complex ChildAgent creation logic previously in Program.StartChildAgentsAsync.
/// </summary>
public class ChildAgentFactory : IChildAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Config _config;

    /// <summary>
    /// Initializes a new instance of the ChildAgentFactory.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="config">The application configuration.</param>
    public ChildAgentFactory(IServiceProvider serviceProvider, Config config)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Creates a new ChildAgent instance for the specified child.
    /// Resolves all required dependencies and configures the agent according to application settings.
    /// </summary>
    /// <param name="child">The child configuration for which to create the agent.</param>
    /// <param name="schedulingService">The scheduling service instance to inject.</param>
    /// <returns>A configured ChildAgent instance ready to be started.</returns>
    /// <exception cref="ArgumentNullException">Thrown when child or schedulingService is null.</exception>
    public IChildAgent CreateChildAgent(Child child, ISchedulingService schedulingService)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));
        if (schedulingService == null) throw new ArgumentNullException(nameof(schedulingService));

        var openAiService = _serviceProvider.GetRequiredService<IOpenAiService>();
        var weekLetterService = _serviceProvider.GetRequiredService<IWeekLetterService>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        // Get configuration-driven feature flag
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