using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Aula.Configuration;
using Aula.Services;
using Aula.Integration;
using Aula.Utilities;
using Aula.Tools;

namespace Aula.Bots;

/// <summary>
/// Abstract base class for interactive bots providing shared functionality
/// like week letter hash tracking, child management, and common initialization.
/// </summary>
public abstract class BotBase
{
    protected readonly IAgentService _agentService;
    protected readonly Config _config;
    protected readonly ILogger _logger;
    protected readonly ISupabaseService _supabaseService;
    protected readonly Dictionary<string, Child> _childrenByName;
    protected readonly ConcurrentDictionary<string, byte> _postedWeekLetterHashes;
    protected readonly ReminderCommandHandler _reminderHandler;

    protected BotBase(
        IAgentService agentService,
        Config config,
        ILogger logger,
        ISupabaseService supabaseService)
    {
        _agentService = agentService ?? throw new ArgumentNullException(nameof(agentService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

        _childrenByName = new Dictionary<string, Child>();
        foreach (var child in _config.MinUddannelse.Children)
        {
            var key = child.FirstName.ToLowerInvariant();
            if (_childrenByName.ContainsKey(key))
            {
                _logger.LogError("Duplicate child first name found: '{FirstName}'. Child names must be unique for bot functionality.", child.FirstName);
                throw new InvalidOperationException($"Duplicate child first name: '{child.FirstName}'. All children must have unique first names.");
            }
            _childrenByName[key] = child;
        }

        _postedWeekLetterHashes = new ConcurrentDictionary<string, byte>();
        _reminderHandler = new ReminderCommandHandler(_logger, _supabaseService, _childrenByName);
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

            _logger.LogInformation("{BotType} interactive bot started", GetPlatformType());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {BotType} bot", GetPlatformType());
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
            _logger.LogInformation("{BotType} interactive bot stopped", GetPlatformType());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping {BotType} bot", GetPlatformType());
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

        // Check for duplicates using hash
        var hash = ComputeWeekLetterHash(weekLetter);
        if (_postedWeekLetterHashes.ContainsKey(hash))
        {
            _logger.LogInformation("Week letter for {ChildName} already posted (duplicate detected), skipping", childName);
            return;
        }

        try
        {
            await SendWeekLetterMessage(childName, weekLetter);
            _postedWeekLetterHashes[hash] = 0;
            _logger.LogInformation("Posted week letter for {ChildName}", childName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post week letter for {ChildName}", childName);
            throw;
        }
    }

    /// <summary>
    /// Builds a welcome message with children information and current week.
    /// </summary>
    protected string BuildWelcomeMessage()
    {
        // Build a list of available children (first names only)
        string childrenList = string.Join(" og ", _childrenByName.Values.Select(c =>
            c.FirstName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? c.FirstName));

        // Get the current week number
        int weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Now);

        return $"🤖 Jeg er online og har ugeplan for {childrenList} for Uge {weekNumber}\n\n" +
               "Du kan spørge mig om:\n" +
               "• Aktiviteter for en bestemt dag: 'Hvad skal Emma i dag?'\n" +
               "• Oprette påmindelser: 'Mind mig om at hente TestChild1 kl 15'\n" +
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