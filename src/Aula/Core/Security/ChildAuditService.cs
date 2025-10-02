using Aula.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aula.Core.Security;

/// <summary>
/// Implementation of audit logging for child-related operations.
/// Stores audit entries in memory for this implementation (production would use persistent storage).
/// </summary>
public class ChildAuditService : IChildAuditService
{
    private readonly ILogger _logger;
    private readonly ConcurrentBag<AuditEntry> _auditTrail;

    public ChildAuditService(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<ChildAuditService>();
        _auditTrail = new ConcurrentBag<AuditEntry>();
    }

    public Task LogAuthenticationAttemptAsync(Child child, bool success, string reason, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(child);

        var entry = new AuditEntry
        {
            ChildName = child.FirstName,
            EventType = "Authentication",
            Operation = success ? "LoginSuccess" : "LoginFailure",
            Resource = "MinUddannelse",
            Success = success,
            Details = reason,
            SessionId = sessionId,
            Severity = success ? SecuritySeverity.Information : SecuritySeverity.Warning
        };

        _auditTrail.Add(entry);

        if (success)
        {
            _logger.LogInformation(
                "Authentication successful for {ChildName} (Session: {SessionId})",
                child.FirstName, sessionId);
        }
        else
        {
            _logger.LogWarning(
                "Authentication failed for {ChildName}: {Reason} (Session: {SessionId})",
                child.FirstName, reason, sessionId);
        }

        return Task.CompletedTask;
    }

    public Task LogDataAccessAsync(Child child, string operation, string resource, bool success)
    {
        ArgumentNullException.ThrowIfNull(child);

        var entry = new AuditEntry
        {
            ChildName = child.FirstName,
            EventType = "DataAccess",
            Operation = operation,
            Resource = resource,
            Success = success,
            Severity = success ? SecuritySeverity.Information : SecuritySeverity.Warning
        };

        _auditTrail.Add(entry);

        _logger.LogDebug(
            "Data access {Operation} for {ChildName} on {Resource}: {Success}",
            operation, child.FirstName, resource, success ? "Success" : "Failed");

        return Task.CompletedTask;
    }

    public Task LogSessionInvalidationAsync(Child child, string sessionId, string reason)
    {
        ArgumentNullException.ThrowIfNull(child);

        var entry = new AuditEntry
        {
            ChildName = child.FirstName,
            EventType = "SessionInvalidation",
            Operation = "InvalidateSession",
            Resource = "Session",
            Success = true,
            Details = reason,
            SessionId = sessionId,
            Severity = SecuritySeverity.Information
        };

        _auditTrail.Add(entry);

        _logger.LogInformation(
            "Session invalidated for {ChildName} (Session: {SessionId}): {Reason}",
            child.FirstName, sessionId, reason);

        return Task.CompletedTask;
    }

    public Task LogSessionTimeoutAsync(Child child, string sessionId, DateTimeOffset lastActivity)
    {
        ArgumentNullException.ThrowIfNull(child);

        var entry = new AuditEntry
        {
            ChildName = child.FirstName,
            EventType = "SessionTimeout",
            Operation = "TimeoutSession",
            Resource = "Session",
            Success = true,
            Details = $"Last activity: {lastActivity:O}",
            SessionId = sessionId,
            Severity = SecuritySeverity.Information
        };

        _auditTrail.Add(entry);

        _logger.LogInformation(
            "Session timeout for {ChildName} (Session: {SessionId}), last activity: {LastActivity}",
            child.FirstName, sessionId, lastActivity);

        return Task.CompletedTask;
    }

    public Task LogSecurityEventAsync(Child? child, string eventType, string details, SecuritySeverity severity)
    {
        var entry = new AuditEntry
        {
            ChildName = child?.FirstName ?? "System",
            EventType = "Security",
            Operation = eventType,
            Resource = "System",
            Success = false,
            Details = details,
            Severity = severity
        };

        _auditTrail.Add(entry);

        var logLevel = severity switch
        {
            SecuritySeverity.Critical => LogLevel.Critical,
            SecuritySeverity.Error => LogLevel.Error,
            SecuritySeverity.Warning => LogLevel.Warning,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "Security event [{EventType}] for {ChildName}: {Details}",
            eventType, entry.ChildName, details);

        return Task.CompletedTask;
    }

    public Task<List<AuditEntry>> GetAuditTrailAsync(Child child, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        ArgumentNullException.ThrowIfNull(child);

        var entries = _auditTrail
            .Where(e => e.ChildName == child.FirstName
                       && e.Timestamp >= startDate
                       && e.Timestamp <= endDate)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} audit entries for {ChildName} between {StartDate} and {EndDate}",
            entries.Count, child.FirstName, startDate, endDate);

        return Task.FromResult(entries);
    }
}
