using Aula.Configuration;

namespace Aula.Authentication;

/// <summary>
/// Provides audit logging for child-related authentication and data access operations.
/// </summary>
public interface IChildAuditService
{
    /// <summary>
    /// Logs an authentication attempt for a child.
    /// </summary>
    /// <param name="child">The child attempting authentication</param>
    /// <param name="success">Whether the authentication succeeded</param>
    /// <param name="reason">Additional details about the attempt</param>
    /// <param name="sessionId">The session identifier</param>
    Task LogAuthenticationAttemptAsync(Child child, bool success, string reason, string sessionId);

    /// <summary>
    /// Logs a data access operation for a child.
    /// </summary>
    /// <param name="child">The child accessing data</param>
    /// <param name="operation">The operation being performed</param>
    /// <param name="resource">The resource being accessed</param>
    /// <param name="success">Whether the operation succeeded</param>
    Task LogDataAccessAsync(Child child, string operation, string resource, bool success);

    /// <summary>
    /// Logs a session invalidation event.
    /// </summary>
    /// <param name="child">The child whose session was invalidated</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="reason">The reason for invalidation</param>
    Task LogSessionInvalidationAsync(Child child, string sessionId, string reason);

    /// <summary>
    /// Logs a session timeout event.
    /// </summary>
    /// <param name="child">The child whose session timed out</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="lastActivity">The last activity timestamp</param>
    Task LogSessionTimeoutAsync(Child child, string sessionId, DateTimeOffset lastActivity);

    /// <summary>
    /// Logs a security event.
    /// </summary>
    /// <param name="child">The child involved</param>
    /// <param name="eventType">The type of security event</param>
    /// <param name="details">Additional details</param>
    /// <param name="severity">The severity level</param>
    Task LogSecurityEventAsync(Child? child, string eventType, string details, SecuritySeverity severity);

    /// <summary>
    /// Gets the audit trail for a specific child.
    /// </summary>
    /// <param name="child">The child to get audit trail for</param>
    /// <param name="startDate">Start date for the audit trail</param>
    /// <param name="endDate">End date for the audit trail</param>
    /// <returns>List of audit entries</returns>
    Task<List<AuditEntry>> GetAuditTrailAsync(Child child, DateTimeOffset startDate, DateTimeOffset endDate);
}

/// <summary>
/// Represents a single audit log entry.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string ChildName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Details { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public SecuritySeverity Severity { get; set; } = SecuritySeverity.Information;
}

/// <summary>
/// Security event severity levels.
/// </summary>
public enum SecuritySeverity
{
    Information,
    Warning,
    Error,
    Critical
}
