using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace Aula.Authentication;

/// <summary>
/// Child-aware authentication service that uses IChildContext to determine the current child.
/// Provides secure session isolation and audit logging for each child.
/// </summary>
public class ChildAwareMinUddannelseClient : IChildAuthenticationService
{
    private readonly IChildContext _context;
    private readonly IChildContextValidator _contextValidator;
    private readonly IChildAuditService _auditService;
    private readonly IMinUddannelseClient _innerClient;
    private readonly ILogger<ChildAwareMinUddannelseClient> _logger;
    private readonly ConcurrentDictionary<string, ChildSessionState> _sessions;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public ChildAwareMinUddannelseClient(
        IChildContext context,
        IChildContextValidator contextValidator,
        IChildAuditService auditService,
        IMinUddannelseClient innerClient,
        ILogger<ChildAwareMinUddannelseClient> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _contextValidator = contextValidator ?? throw new ArgumentNullException(nameof(contextValidator));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessions = new ConcurrentDictionary<string, ChildSessionState>();
    }

    public async Task<bool> AuthenticateAsync()
    {
        // Validate context
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        if (!await _contextValidator.ValidateContextIntegrityAsync(_context))
        {
            _logger.LogWarning("Context validation failed for {ChildName}", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "InvalidContext", "Context integrity validation failed", SecuritySeverity.Warning);
            return false;
        }

        // Get or create session state
        var sessionKey = GetSessionKey(child);
        var session = _sessions.GetOrAdd(sessionKey, _ => new ChildSessionState(child));

        try
        {
            _logger.LogInformation("Authenticating {ChildName} (Session: {SessionId})", child.FirstName, session.SessionId);

            // Perform authentication
            var success = await _innerClient.LoginAsync();

            // Update session state
            if (success)
            {
                session.IsAuthenticated = true;
                session.LastAuthenticationTime = DateTimeOffset.UtcNow;
                session.LastActivityTime = DateTimeOffset.UtcNow;
                _logger.LogInformation("Authentication successful for {ChildName}", child.FirstName);
            }
            else
            {
                session.IsAuthenticated = false;
                _logger.LogWarning("Authentication failed for {ChildName}", child.FirstName);
            }

            // Audit the attempt
            await _auditService.LogAuthenticationAttemptAsync(
                child,
                success,
                success ? "Authentication successful" : "Authentication failed",
                session.SessionId);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for {ChildName}", child.FirstName);

            await _auditService.LogSecurityEventAsync(
                child,
                "AuthenticationError",
                $"Exception during authentication: {ex.Message}",
                SecuritySeverity.Error);

            session.IsAuthenticated = false;
            return false;
        }
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;
        var sessionKey = GetSessionKey(child);

        if (_sessions.TryGetValue(sessionKey, out var session))
        {
            // Check if session is still valid (not timed out)
            if (session.IsAuthenticated && !IsSessionExpired(session))
            {
                session.LastActivityTime = DateTimeOffset.UtcNow;
                return Task.FromResult(true);
            }
            else if (IsSessionExpired(session))
            {
                _logger.LogInformation("Session expired for {ChildName}", child.FirstName);
                _ = _auditService.LogSessionTimeoutAsync(child, session.SessionId, session.LastActivityTime);
                session.IsAuthenticated = false;
            }
        }

        return Task.FromResult(false);
    }

    public async Task<JObject?> GetWeekLetterAsync(DateOnly targetDate, bool allowLiveFetch = false)
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Validate permissions
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_letter"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to read week letter", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning);
            return null;
        }

        // Ensure authenticated if live fetch is requested
        if (allowLiveFetch && !await IsAuthenticatedAsync())
        {
            _logger.LogInformation("Authenticating {ChildName} for week letter fetch", child.FirstName);
            if (!await AuthenticateAsync())
            {
                _logger.LogWarning("Failed to authenticate {ChildName} for week letter fetch", child.FirstName);
                return null;
            }
        }

        try
        {
            var result = await _innerClient.GetWeekLetter(child, targetDate, allowLiveFetch);

            await _auditService.LogDataAccessAsync(
                child,
                "GetWeekLetter",
                $"week_{targetDate:yyyy-MM-dd}",
                result != null);

            UpdateSessionActivity(child);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching week letter for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "GetWeekLetter", $"week_{targetDate:yyyy-MM-dd}", false);
            throw;
        }
    }

    public async Task<JObject?> GetWeekScheduleAsync(DateOnly targetDate)
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Validate permissions
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_schedule"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to read week schedule", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_schedule", SecuritySeverity.Warning);
            return null;
        }

        // Ensure authenticated
        if (!await IsAuthenticatedAsync())
        {
            _logger.LogInformation("Authenticating {ChildName} for schedule fetch", child.FirstName);
            if (!await AuthenticateAsync())
            {
                _logger.LogWarning("Failed to authenticate {ChildName} for schedule fetch", child.FirstName);
                return null;
            }
        }

        try
        {
            var result = await _innerClient.GetWeekSchedule(child, targetDate);

            await _auditService.LogDataAccessAsync(
                child,
                "GetWeekSchedule",
                $"schedule_{targetDate:yyyy-MM-dd}",
                result != null);

            UpdateSessionActivity(child);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching week schedule for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "GetWeekSchedule", $"schedule_{targetDate:yyyy-MM-dd}", false);
            throw;
        }
    }

    public async Task<JObject?> GetStoredWeekLetterAsync(int weekNumber, int year)
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;

        // Validate permissions
        if (!await _contextValidator.ValidateChildPermissionsAsync(child, "read:week_letter"))
        {
            _logger.LogWarning("Permission denied for {ChildName} to read stored week letter", child.FirstName);
            await _auditService.LogSecurityEventAsync(child, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning);
            return null;
        }

        try
        {
            var result = await _innerClient.GetStoredWeekLetter(child, weekNumber, year);

            await _auditService.LogDataAccessAsync(
                child,
                "GetStoredWeekLetter",
                $"stored_week_{weekNumber}_{year}",
                result != null);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stored week letter for {ChildName}", child.FirstName);
            await _auditService.LogDataAccessAsync(child, "GetStoredWeekLetter", $"stored_week_{weekNumber}_{year}", false);
            throw;
        }
    }

    public async Task InvalidateSessionAsync()
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;
        var sessionKey = GetSessionKey(child);

        if (_sessions.TryRemove(sessionKey, out var session))
        {
            _logger.LogInformation("Session invalidated for {ChildName} (Session: {SessionId})",
                child.FirstName, session.SessionId);

            await _auditService.LogSessionInvalidationAsync(
                child,
                session.SessionId,
                "Manual invalidation");
        }
    }

    public string GetSessionId()
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;
        var sessionKey = GetSessionKey(child);

        var session = _sessions.GetOrAdd(sessionKey, _ => new ChildSessionState(child));
        return session.SessionId;
    }

    public DateTimeOffset? GetLastAuthenticationTime()
    {
        _context.ValidateContext();
        var child = _context.CurrentChild!;
        var sessionKey = GetSessionKey(child);

        if (_sessions.TryGetValue(sessionKey, out var session))
        {
            return session.LastAuthenticationTime;
        }

        return null;
    }

    private static string GetSessionKey(Child child)
    {
        // Create a unique key based on child's first and last name
        return $"{child.FirstName}_{child.LastName}".ToLowerInvariant();
    }

    private bool IsSessionExpired(ChildSessionState session)
    {
        return DateTimeOffset.UtcNow - session.LastActivityTime > _sessionTimeout;
    }

    private void UpdateSessionActivity(Child child)
    {
        var sessionKey = GetSessionKey(child);
        if (_sessions.TryGetValue(sessionKey, out var session))
        {
            session.LastActivityTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Internal class to track session state for each child.
    /// </summary>
    private sealed class ChildSessionState
    {
        public string SessionId { get; }
        public Child Child { get; }
        public bool IsAuthenticated { get; set; }
        public DateTimeOffset? LastAuthenticationTime { get; set; }
        public DateTimeOffset LastActivityTime { get; set; }

        public ChildSessionState(Child child)
        {
            Child = child;
            SessionId = Guid.NewGuid().ToString();
            IsAuthenticated = false;
            LastActivityTime = DateTimeOffset.UtcNow;
        }
    }
}