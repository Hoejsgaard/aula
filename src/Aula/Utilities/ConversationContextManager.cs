using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Aula.Utilities;

public class ConversationContext
{
    public string? LastChildName { get; set; }
    public bool WasAboutToday { get; set; }
    public bool WasAboutTomorrow { get; set; }
    public bool WasAboutHomework { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsStillValid => (DateTime.Now - Timestamp).TotalMinutes < 10; // Context expires after 10 minutes

    public override string ToString()
    {
        return $"Child: {LastChildName ?? "none"}, Today: {WasAboutToday}, Tomorrow: {WasAboutTomorrow}, Homework: {WasAboutHomework}, Age: {(DateTime.Now - Timestamp).TotalMinutes.ToString("F1", CultureInfo.InvariantCulture)} minutes";
    }
}

public class ConversationContextManager<TKey> where TKey : notnull
{
    private readonly ILogger _logger;
    private readonly Dictionary<TKey, ConversationContext> _conversationContexts = new();

    public ConversationContextManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void UpdateContext(TKey key, string? childName, bool isAboutToday = false, bool isAboutTomorrow = false, bool isAboutHomework = false)
    {
        _conversationContexts[key] = new ConversationContext
        {
            LastChildName = childName,
            WasAboutToday = isAboutToday,
            WasAboutTomorrow = isAboutTomorrow,
            WasAboutHomework = isAboutHomework,
            Timestamp = DateTime.Now
        };

        _logger.LogInformation("Updated conversation context for key {Key}: {Context}", key, _conversationContexts[key]);
    }

    public ConversationContext? GetContext(TKey key)
    {
        if (_conversationContexts.TryGetValue(key, out var context) && context.IsStillValid)
        {
            return context;
        }

        // Remove expired context
        if (_conversationContexts.ContainsKey(key))
        {
            _conversationContexts.Remove(key);
            _logger.LogInformation("Removed expired conversation context for key {Key}", key);
        }

        return null;
    }

    public void ClearContext(TKey key)
    {
        if (_conversationContexts.Remove(key))
        {
            _logger.LogInformation("Cleared conversation context for key {Key}", key);
        }
    }

    public void ClearAllContexts()
    {
        var count = _conversationContexts.Count;
        _conversationContexts.Clear();
        _logger.LogInformation("Cleared all {Count} conversation contexts", count);
    }
}