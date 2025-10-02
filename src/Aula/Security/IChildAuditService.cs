using Aula.Configuration;

namespace Aula.Security;

public interface IChildAuditService
{
    Task LogAuthenticationAttemptAsync(Child child, bool success, string reason, string sessionId);
    Task LogDataAccessAsync(Child child, string operation, string resource, bool success);
    Task LogSessionInvalidationAsync(Child child, string sessionId, string reason);
    Task LogSessionTimeoutAsync(Child child, string sessionId, DateTimeOffset lastActivity);
    Task LogSecurityEventAsync(Child? child, string eventType, string details, SecuritySeverity severity);
    Task<List<AuditEntry>> GetAuditTrailAsync(Child child, DateTimeOffset startDate, DateTimeOffset endDate);
}

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

public enum SecuritySeverity
{
    Information,
    Warning,
    Error,
    Critical
}
