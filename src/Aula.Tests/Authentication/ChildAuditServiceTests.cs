using Aula.Authentication;
using Aula.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Authentication;

public class ChildAuditServiceTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ChildAuditService _auditService;
    private readonly Child _testChild;

    public ChildAuditServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        _auditService = new ChildAuditService(_mockLoggerFactory.Object);
        _testChild = new Child { FirstName = "Test", LastName = "Child" };
    }

    [Fact]
    public async Task LogAuthenticationAttemptAsync_WithSuccess_LogsInformation()
    {
        // Act
        await _auditService.LogAuthenticationAttemptAsync(_testChild, true, "Login successful", "session-123");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Authentication successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuthenticationAttemptAsync_WithFailure_LogsWarning()
    {
        // Act
        await _auditService.LogAuthenticationAttemptAsync(_testChild, false, "Invalid credentials", "session-123");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Authentication failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAuthenticationAttemptAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _auditService.LogAuthenticationAttemptAsync(null!, true, "reason", "session-123"));
    }

    [Fact]
    public async Task LogDataAccessAsync_LogsDebugInformation()
    {
        // Act
        await _auditService.LogDataAccessAsync(_testChild, "GetWeekLetter", "week_2025_39", true);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Data access")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogSessionInvalidationAsync_LogsInformation()
    {
        // Act
        await _auditService.LogSessionInvalidationAsync(_testChild, "session-123", "Manual logout");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Session invalidated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogSessionTimeoutAsync_LogsInformation()
    {
        // Arrange
        var lastActivity = DateTimeOffset.UtcNow.AddMinutes(-35);

        // Act
        await _auditService.LogSessionTimeoutAsync(_testChild, "session-123", lastActivity);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Session timeout")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogSecurityEventAsync_WithCriticalSeverity_LogsCritical()
    {
        // Act
        await _auditService.LogSecurityEventAsync(_testChild, "UnauthorizedAccess", "Attempted to access restricted resource", SecuritySeverity.Critical);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Security event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogSecurityEventAsync_WithNullChild_UsesSystemAsChildName()
    {
        // Act
        await _auditService.LogSecurityEventAsync(null, "SystemEvent", "System-level event", SecuritySeverity.Information);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("System")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsFilteredEntries()
    {
        // Arrange
        var startDate = DateTimeOffset.UtcNow.AddHours(-1);

        // Add some audit entries
        await _auditService.LogAuthenticationAttemptAsync(_testChild, true, "Login", "session-1");
        await _auditService.LogDataAccessAsync(_testChild, "GetWeekLetter", "resource", true);

        var otherChild = new Child { FirstName = "Other", LastName = "Child" };
        await _auditService.LogAuthenticationAttemptAsync(otherChild, true, "Login", "session-2");

        var endDate = DateTimeOffset.UtcNow.AddMinutes(1); // Set end date after adding entries

        // Act
        var trail = await _auditService.GetAuditTrailAsync(_testChild, startDate, endDate);

        // Assert
        Assert.Equal(2, trail.Count); // Only entries for _testChild
        Assert.All(trail, entry => Assert.Equal(_testChild.FirstName, entry.ChildName));
    }

    [Fact]
    public async Task GetAuditTrailAsync_WithNullChild_ThrowsArgumentNullException()
    {
        // Arrange
        var startDate = DateTimeOffset.UtcNow.AddHours(-1);
        var endDate = DateTimeOffset.UtcNow;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _auditService.GetAuditTrailAsync(null!, startDate, endDate));
    }

    [Fact]
    public async Task GetAuditTrailAsync_FiltersbyDateRange()
    {
        // Arrange
        await _auditService.LogAuthenticationAttemptAsync(_testChild, true, "Login", "session-1");
        await Task.Delay(10); // Ensure time difference

        var midPoint = DateTimeOffset.UtcNow;
        await Task.Delay(10);

        await _auditService.LogDataAccessAsync(_testChild, "GetWeekLetter", "resource", true);

        // Act - Get only entries after midpoint
        var trail = await _auditService.GetAuditTrailAsync(_testChild, midPoint, DateTimeOffset.UtcNow.AddMinutes(1));

        // Assert
        Assert.Single(trail);
        Assert.Equal("DataAccess", trail[0].EventType);
    }

    [Fact]
    public void AuditEntry_HasCorrectDefaults()
    {
        // Act
        var entry = new AuditEntry();

        // Assert
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.True(entry.Timestamp <= DateTimeOffset.UtcNow);
        Assert.True(entry.Timestamp > DateTimeOffset.UtcNow.AddSeconds(-5));
        Assert.Equal(string.Empty, entry.ChildName);
        Assert.Equal(string.Empty, entry.EventType);
        Assert.Equal(string.Empty, entry.Operation);
        Assert.Equal(string.Empty, entry.Resource);
        Assert.False(entry.Success);
        Assert.Equal(string.Empty, entry.Details);
        Assert.Equal(string.Empty, entry.SessionId);
        Assert.Equal(SecuritySeverity.Information, entry.Severity);
    }
}
