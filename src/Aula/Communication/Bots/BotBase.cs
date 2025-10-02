using System.Collections.Concurrent;
using Aula.AI.Services;
using Microsoft.Extensions.Logging;
using Aula.Configuration;
using Aula.External.MinUddannelse;
using Aula.Content.Processing;

namespace Aula.Communication.Bots;

/// <summary>
/// Abstract base class for interactive bots providing shared functionality
/// like week letter hash tracking, child management, and common initialization.
/// </summary>
public abstract class BotBase
{
    protected IAgentService AgentService { get; }
    protected Config Config { get; }
    protected ILogger Logger { get; }
    protected Dictionary<string, Child> ChildrenByName { get; }
    protected ConcurrentDictionary<string, byte> PostedWeekLetterHashes { get; }

    protected BotBase(
        IAgentService agentService,
        Config config,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        AgentService = agentService;
        Config = config;
        Logger = logger;

        ChildrenByName = new Dictionary<string, Child>();
        foreach (var child in Config.MinUddannelse.Children)
        {
            var key = child.FirstName.ToLowerInvariant();
            if (ChildrenByName.ContainsKey(key))
            {
                Logger.LogError("Duplicate child first name found: '{FirstName}'. Child names must be unique for bot functionality.", child.FirstName);
                throw new InvalidOperationException($"Duplicate child first name: '{child.FirstName}'. All children must have unique first names.");
            }
            ChildrenByName[key] = child;
        }

        PostedWeekLetterHashes = new ConcurrentDictionary<string, byte>();
    }

    /// <summary>
    /// Starts the bot. Template method that calls platform-specific initialization.
    /// </summary>
    public async Task Start()
    {
        try
        {
            await ValidateConfiguration();
            await InitializePlatform();
            await SendWelcomeMessage();
            await StartMessageProcessing();

            Logger.LogInformation("{BotType} interactive bot started", GetPlatformType());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start {BotType} bot", GetPlatformType());
            throw;
        }
    }

    /// <summary>
    /// Stops the bot. Template method that calls platform-specific cleanup.
    /// </summary>
    public virtual void Stop()
    {
        try
        {
            StopMessageProcessing();
            Logger.LogInformation("{BotType} interactive bot stopped", GetPlatformType());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error stopping {BotType} bot", GetPlatformType());
        }
    }

    /// <summary>
    /// Posts a week letter to the platform's default channel.
    /// </summary>
    public async Task PostWeekLetter(string childName, string weekLetter)
    {
        if (string.IsNullOrEmpty(weekLetter))
        {
            return;
        }

        var hash = ComputeWeekLetterHash(weekLetter);
        if (PostedWeekLetterHashes.ContainsKey(hash))
        {
            Logger.LogInformation("Week letter for {ChildName} already posted (duplicate detected), skipping", childName);
            return;
        }

        try
        {
            await SendWeekLetterMessage(childName, weekLetter);
            PostedWeekLetterHashes[hash] = 0;
            Logger.LogInformation("Posted week letter for {ChildName}", childName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to post week letter for {ChildName}", childName);
            throw;
        }
    }

    /// <summary>
    /// Builds a welcome message with children information and current week.
    /// </summary>
    protected string BuildWelcomeMessage()
    {
        string childrenList = string.Join(" og ", ChildrenByName.Values.Select(c =>
            c.FirstName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? c.FirstName));

        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);

        var firstChild = ChildrenByName.Values.FirstOrDefault();
        string exampleChildName = firstChild?.FirstName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? firstChild?.FirstName ?? "barnet";

        return $"Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}\n\n" +
               "Du kan spørge mig om:\n" +
               $"• Aktiviteter for en bestemt dag: 'Hvad skal {exampleChildName} i dag?'\n" +
               $"• Oprette påmindelser: 'Mind mig om at hente {exampleChildName} kl 15'\n" +
               "• Se ugeplaner: 'Vis ugeplanen for denne uge'\n" +
               "• Hjælp: 'hjælp' eller 'help'";
    }

    /// <summary>
    /// Computes a hash for week letter content to detect duplicates.
    /// </summary>
    protected string ComputeWeekLetterHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }

    // Abstract methods that must be implemented by derived classes

    /// <summary>
    /// Gets the platform type name (e.g., "Slack", "Telegram").
    /// </summary>
    protected abstract string GetPlatformType();

    /// <summary>
    /// Validates platform-specific configuration.
    /// </summary>
    protected abstract Task ValidateConfiguration();

    /// <summary>
    /// Initializes platform-specific components.
    /// </summary>
    protected abstract Task InitializePlatform();

    /// <summary>
    /// Sends the welcome message to the platform.
    /// </summary>
    protected abstract Task SendWelcomeMessage();

    /// <summary>
    /// Starts platform-specific message processing.
    /// </summary>
    protected abstract Task StartMessageProcessing();

    /// <summary>
    /// Stops platform-specific message processing.
    /// </summary>
    protected abstract void StopMessageProcessing();

    /// <summary>
    /// Sends a week letter message to the platform.
    /// </summary>
    protected abstract Task SendWeekLetterMessage(string childName, string weekLetter);
}
