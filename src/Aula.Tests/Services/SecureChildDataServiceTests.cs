using Aula.Authentication;
using Aula.Configuration;
using Aula.Context;
using Aula.Integration;
using Aula.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Aula.Tests.Services;

public class SecureChildDataServiceTests
{
    private readonly Mock<IChildContext> _mockContext;
    private readonly Mock<IChildContextValidator> _mockContextValidator;
    private readonly Mock<IChildAuditService> _mockAuditService;
    private readonly Mock<IChildRateLimiter> _mockRateLimiter;
    private readonly Mock<IDataService> _mockDataService;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<IMinUddannelseClient> _mockMinUddannelseClient;
    private readonly Mock<ILogger<SecureChildDataService>> _mockLogger;
    private readonly SecureChildDataService _service;
    private readonly Child _testChild;

    public SecureChildDataServiceTests()
    {
        _mockContext = new Mock<IChildContext>();
        _mockContextValidator = new Mock<IChildContextValidator>();
        _mockAuditService = new Mock<IChildAuditService>();
        _mockRateLimiter = new Mock<IChildRateLimiter>();
        _mockDataService = new Mock<IDataService>();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockMinUddannelseClient = new Mock<IMinUddannelseClient>();
        _mockLogger = new Mock<ILogger<SecureChildDataService>>();

        _testChild = new Child { FirstName = "Test", LastName = "Child" };
        _mockContext.Setup(c => c.CurrentChild).Returns(_testChild);

        _service = new SecureChildDataService(
            _mockContext.Object,
            _mockContextValidator.Object,
            _mockAuditService.Object,
            _mockRateLimiter.Object,
            _mockDataService.Object,
            _mockSupabaseService.Object,
            _mockMinUddannelseClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CacheWeekLetterAsync_WithValidPermissions_CachesLetter()
    {
        // Arrange
        var weekLetter = JObject.Parse("{\"content\": \"test\"}");
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "write:week_letter"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "CacheWeekLetter"))
            .ReturnsAsync(true);

        // Act
        await _service.CacheWeekLetterAsync(2025, 10, weekLetter);

        // Assert
        _mockContext.Verify(c => c.ValidateContext(), Times.Once);
        _mockDataService.Verify(d => d.CacheWeekLetter(_testChild, 2025, 10, weekLetter), Times.Once);
        _mockRateLimiter.Verify(r => r.RecordOperationAsync(_testChild, "CacheWeekLetter"), Times.Once);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "CacheWeekLetter", "week_2025_10", true), Times.Once);
    }

    [Fact]
    public async Task CacheWeekLetterAsync_WithoutPermission_ThrowsUnauthorizedException()
    {
        // Arrange
        var weekLetter = JObject.Parse("{\"content\": \"test\"}");
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "write:week_letter"))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CacheWeekLetterAsync(2025, 10, weekLetter));

        _mockDataService.Verify(d => d.CacheWeekLetter(It.IsAny<Child>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<JObject>()), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(_testChild, "PermissionDenied", "write:week_letter", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task CacheWeekLetterAsync_WhenRateLimitExceeded_ThrowsRateLimitException()
    {
        // Arrange
        var weekLetter = JObject.Parse("{\"content\": \"test\"}");
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "write:week_letter"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "CacheWeekLetter"))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<RateLimitExceededException>(() =>
            _service.CacheWeekLetterAsync(2025, 10, weekLetter));

        _mockDataService.Verify(d => d.CacheWeekLetter(It.IsAny<Child>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<JObject>()), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(_testChild, "RateLimitExceeded", "CacheWeekLetter", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_WithValidPermissions_ReturnsLetter()
    {
        // Arrange
        var expectedLetter = JObject.Parse("{\"content\": \"test\"}");
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:week_letter"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "GetWeekLetter"))
            .ReturnsAsync(true);
        _mockDataService.Setup(d => d.GetWeekLetter(_testChild, 2025, 10))
            .Returns(expectedLetter);

        // Act
        var result = await _service.GetWeekLetterAsync(2025, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedLetter, result);
        _mockRateLimiter.Verify(r => r.RecordOperationAsync(_testChild, "GetWeekLetter"), Times.Once);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "GetWeekLetter", "week_2025_10", true), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_WithoutPermission_ReturnsNull()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:week_letter"))
            .ReturnsAsync(false);

        // Act
        var result = await _service.GetWeekLetterAsync(2025, 10);

        // Assert
        Assert.Null(result);
        _mockDataService.Verify(d => d.GetWeekLetter(It.IsAny<Child>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(_testChild, "PermissionDenied", "read:week_letter", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task StoreWeekLetterAsync_WithValidPermissions_StoresInDatabase()
    {
        // Arrange
        var weekLetter = JObject.Parse("{\"content\": \"test\"}");
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "write:database"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "StoreWeekLetter"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.StoreWeekLetterAsync(2025, 10, weekLetter);

        // Assert
        Assert.True(result);
        _mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
            _testChild.FirstName,
            2025,
            10,
            It.IsAny<string>(),
            It.IsAny<string>(),
            false,
            false), Times.Once);
        _mockRateLimiter.Verify(r => r.RecordOperationAsync(_testChild, "StoreWeekLetter"), Times.Once);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "StoreWeekLetter", "db_week_2025_10", true), Times.Once);
    }

    [Fact]
    public async Task DeleteWeekLetterAsync_WithValidPermissions_DeletesFromDatabase()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "delete:database"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "DeleteWeekLetter"))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteWeekLetterAsync(2025, 10);

        // Assert
        Assert.True(result);
        _mockSupabaseService.Verify(s => s.DeleteWeekLetterAsync(_testChild.FirstName, 2025, 10), Times.Once);
        _mockRateLimiter.Verify(r => r.RecordOperationAsync(_testChild, "DeleteWeekLetter"), Times.Once);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(_testChild, "DataDeletion", "week_2025_10", SecuritySeverity.Warning), Times.Once);
    }

    [Fact]
    public async Task DeleteWeekLetterAsync_WithoutPermission_ReturnsFalse()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "delete:database"))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteWeekLetterAsync(2025, 10);

        // Assert
        Assert.False(result);
        _mockSupabaseService.Verify(s => s.DeleteWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockAuditService.Verify(a => a.LogSecurityEventAsync(_testChild, "PermissionDenied", "delete:database", SecuritySeverity.Critical), Times.Once);
    }

    [Fact]
    public async Task GetStoredWeekLettersAsync_WithValidPermissions_ReturnsLetters()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, "read:database"))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, "GetStoredWeekLetters"))
            .ReturnsAsync(true);

        var storedLetters = new List<StoredWeekLetter>
        {
            new StoredWeekLetter { RawContent = "{\"week\": 1}", WeekNumber = 1 },
            new StoredWeekLetter { RawContent = "{\"week\": 2}", WeekNumber = 2 }
        };
        _mockSupabaseService.Setup(s => s.GetStoredWeekLettersAsync(_testChild.FirstName, 2025))
            .ReturnsAsync(storedLetters);

        // Act
        var result = await _service.GetStoredWeekLettersAsync(2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        _mockRateLimiter.Verify(r => r.RecordOperationAsync(_testChild, "GetStoredWeekLetters"), Times.Once);
        _mockAuditService.Verify(a => a.LogDataAccessAsync(_testChild, "GetStoredWeekLetters", "db_year_2025", true), Times.Once);
    }

    [Fact]
    public async Task AllOperations_WithNullContext_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockContext.Setup(c => c.CurrentChild).Returns((Child?)null);
        _mockContext.Setup(c => c.ValidateContext()).Throws<InvalidOperationException>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetWeekLetterAsync(2025, 10));
    }

    [Fact]
    public Task Constructor_WithNullDependencies_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            null!, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object,
            _mockDataService.Object, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, null!, _mockAuditService.Object, _mockRateLimiter.Object,
            _mockDataService.Object, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, null!, _mockRateLimiter.Object,
            _mockDataService.Object, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, null!,
            _mockDataService.Object, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object,
            null!, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object,
            _mockDataService.Object, null!, _mockMinUddannelseClient.Object, _mockLogger.Object));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object,
            _mockDataService.Object, _mockSupabaseService.Object, _mockMinUddannelseClient.Object, null!));

        Assert.Throws<ArgumentNullException>(() => new SecureChildDataService(
            _mockContext.Object, _mockContextValidator.Object, _mockAuditService.Object, _mockRateLimiter.Object,
            _mockDataService.Object, _mockSupabaseService.Object, null!, _mockLogger.Object));

        return Task.CompletedTask;
    }

    [Fact]
    public async Task CacheOperations_LogAtCorrectLevels()
    {
        // Arrange
        _mockContextValidator.Setup(v => v.ValidateChildPermissionsAsync(_testChild, It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockRateLimiter.Setup(r => r.IsAllowedAsync(_testChild, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act - Cache operation
        var weekLetter = JObject.Parse("{\"content\": \"test\"}");
        await _service.CacheWeekLetterAsync(2025, 10, weekLetter);

        // Assert - Information level for cache operation
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Caching week letter")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Act - Delete operation
        await _service.DeleteWeekLetterAsync(2025, 10);

        // Assert - Warning level for delete operation
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Deleting week letter")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}