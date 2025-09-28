using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Authentication;

public class ChildAwareMinUddannelseClientTests
{
    private readonly Mock<IChildContext> _mockContext;
    private readonly Mock<IChildContextValidator> _mockContextValidator;
    private readonly Mock<IChildAuditService> _mockAuditService;
    private readonly Mock<IMinUddannelseClient> _mockInnerClient;
    private readonly Mock<ILogger<ChildAwareMinUddannelseClient>> _mockLogger;
    private readonly ChildAwareMinUddannelseClient _client;
    private readonly Child _testChild;

    public ChildAwareMinUddannelseClientTests()
    {
        _mockContext = new Mock<IChildContext>();
        _mockContextValidator = new Mock<IChildContextValidator>();
        _mockAuditService = new Mock<IChildAuditService>();
        _mockInnerClient = new Mock<IMinUddannelseClient>();
        _mockLogger = new Mock<ILogger<ChildAwareMinUddannelseClient>>();

        _testChild = new Child { FirstName = "Test", LastName = "Child" };
        _mockContext.Setup(c => c.CurrentChild).Returns(_testChild);
        _mockContext.Setup(c => c.ContextId).Returns(Guid.NewGuid());

        _client = new ChildAwareMinUddannelseClient(
            _mockContext.Object,
            _mockContextValidator.Object,
            _mockAuditService.Object,
            _mockInnerClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidContext_ReturnsTrue()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Act
        var result = await _client.AuthenticateAsync();

        // Assert
        Assert.True(result);
        _mockContext.Verify(c => c.ValidateContext(), Times.Once);
        _mockAuditService.Verify(a => a.LogAuthenticationAttemptAsync(
            _testChild, true, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidContext_ReturnsFalse()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(false);

        // Act
        var result = await _client.AuthenticateAsync();

        // Assert
        Assert.False(result);
        _mockInnerClient.Verify(c => c.LoginAsync(), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "InvalidContext", It.IsAny<string>(), SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenLoginFails_ReturnsFalse()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(false);

        // Act
        var result = await _client.AuthenticateAsync();

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogAuthenticationAttemptAsync(
            _testChild, false, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenExceptionThrown_ReturnsFalse()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ThrowsAsync(new Exception("Login error"));

        // Act
        var result = await _client.AuthenticateAsync();

        // Assert
        Assert.False(result);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "AuthenticationError", It.IsAny<string>(), SecuritySeverity.Error), Times.Once);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_AfterSuccessfulAuth_ReturnsTrue()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Act
        await _client.AuthenticateAsync();
        var isAuthenticated = await _client.IsAuthenticatedAsync();

        // Assert
        Assert.True(isAuthenticated);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithoutAuth_ReturnsFalse()
    {
        // Act
        var isAuthenticated = await _client.IsAuthenticatedAsync();

        // Assert
        Assert.False(isAuthenticated);
    }

    [Fact]
    public async Task GetWeekLetterAsync_WithoutPermission_ReturnsNull()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:week_letter"))
            .ReturnsAsync(false);

        // Act
        var result = await _client.GetWeekLetterAsync(DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.Null(result);
        _mockInnerClient.Verify(c => c.GetWeekLetter(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>()), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(
            _testChild, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_WithPermission_ReturnsData()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:week_letter"))
            .ReturnsAsync(true);
        var expectedData = JObject.Parse("{\"test\": \"data\"}");
        _mockInnerClient.Setup(c => c.GetWeekLetter(_testChild, It.IsAny<DateOnly>(), false))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _client.GetWeekLetterAsync(DateOnly.FromDateTime(DateTime.Today), false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedData, result);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(
            _testChild, "GetWeekLetter", It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public async Task GetWeekScheduleAsync_RequiresAuthentication()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:week_schedule"))
            .ReturnsAsync(true);
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        var expectedData = JObject.Parse("{\"schedule\": \"data\"}");
        _mockInnerClient.Setup(c => c.GetWeekSchedule(_testChild, It.IsAny<DateOnly>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _client.GetWeekScheduleAsync(DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.NotNull(result);
        _mockInnerClient.Verify(c => c.LoginAsync(), Times.Once); // Should authenticate
    }

    [Fact]
    public async Task InvalidateSessionAsync_RemovesSession()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Act
        await _client.AuthenticateAsync();
        await _client.InvalidateSessionAsync();
        var isAuthenticated = await _client.IsAuthenticatedAsync();

        // Assert
        Assert.False(isAuthenticated);
        _mockAuditService.Verify(a => a.LogSessionInvalidationAsync(
            _testChild, It.IsAny<string>(), "Manual invalidation"), Times.Once);
    }

    [Fact]
    public void GetSessionId_ReturnsUniqueId()
    {
        // Act
        var sessionId1 = _client.GetSessionId();
        var sessionId2 = _client.GetSessionId();

        // Assert
        Assert.NotEmpty(sessionId1);
        Assert.Equal(sessionId1, sessionId2); // Same session for same context
        Assert.True(Guid.TryParse(sessionId1, out _)); // Valid GUID
    }

    [Fact]
    public async Task GetLastAuthenticationTime_AfterAuth_ReturnsTimestamp()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Act
        var beforeAuth = _client.GetLastAuthenticationTime();
        await _client.AuthenticateAsync();
        var afterAuth = _client.GetLastAuthenticationTime();

        // Assert
        Assert.Null(beforeAuth);
        Assert.NotNull(afterAuth);
        Assert.True(afterAuth.Value <= DateTimeOffset.UtcNow);
        Assert.True(afterAuth.Value > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task MultipleChildren_HaveSeparateSessions()
    {
        // Arrange
        var child1 = new Child { FirstName = "Child1", LastName = "Test" };
        var child2 = new Child { FirstName = "Child2", LastName = "Test" };

        var mockContext1 = new Mock<IChildContext>();
        mockContext1.Setup(c => c.CurrentChild).Returns(child1);

        var mockContext2 = new Mock<IChildContext>();
        mockContext2.Setup(c => c.CurrentChild).Returns(child2);

        var client1 = new ChildAwareMinUddannelseClient(
            mockContext1.Object,
            _mockContextValidator.Object,
            _mockAuditService.Object,
            _mockInnerClient.Object,
            _mockLogger.Object);

        var client2 = new ChildAwareMinUddannelseClient(
            mockContext2.Object,
            _mockContextValidator.Object,
            _mockAuditService.Object,
            _mockInnerClient.Object,
            _mockLogger.Object);

        _mockContextValidator.Setup(v => v.ValidateContextIntegrityAsync(It.IsAny<IChildContext>()))
            .ReturnsAsync(true);
        _mockInnerClient.Setup(c => c.LoginAsync()).ReturnsAsync(true);

        // Act
        await client1.AuthenticateAsync();
        var session1 = client1.GetSessionId();

        await client2.AuthenticateAsync();
        var session2 = client2.GetSessionId();

        // Assert
        Assert.NotEqual(session1, session2); // Different sessions
    }

    [Fact]
    public void Constructor_WithNullDependencies_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChildAwareMinUddannelseClient(
            null!, _mockContextValidator.Object, _mockAuditService.Object, _mockInnerClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new ChildAwareMinUddannelseClient(
            _mockContext.Object, null!, _mockAuditService.Object, _mockInnerClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new ChildAwareMinUddannelseClient(
            _mockContext.Object, _mockContextValidator.Object, null!, _mockInnerClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new ChildAwareMinUddannelseClient(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, null!, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new ChildAwareMinUddannelseClient(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockInnerClient.Object, null!));
    }
}