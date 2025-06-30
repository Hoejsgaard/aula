using System;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Tests.Services;

public class GoogleCalendarTests
{
    private readonly ILoggerFactory _loggerFactory;

    public GoogleCalendarTests()
    {
        // Use real logger factory to avoid mocking extension methods
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Critical));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenPrefixIsNull()
    {
        // Arrange
        var testServiceAccount = CreateTestServiceAccount();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GoogleCalendar(testServiceAccount, null!, _loggerFactory));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenPrefixIsEmpty()
    {
        // Arrange
        var testServiceAccount = CreateTestServiceAccount();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GoogleCalendar(testServiceAccount, "", _loggerFactory));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenPrefixIsTooShort()
    {
        // Arrange
        var testServiceAccount = CreateTestServiceAccount();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new GoogleCalendar(testServiceAccount, "ab", _loggerFactory));
    }

    [Fact]
    public void Constructor_ParameterValidation_PassesWithValidPrefix()
    {
        // Arrange
        var testServiceAccount = CreateTestServiceAccount();

        // Act & Assert - We expect Google API to fail but parameter validation to pass
        var exception = Assert.ThrowsAny<Exception>(() =>
            new GoogleCalendar(testServiceAccount, "test", _loggerFactory));

        // The exception should NOT be ArgumentNullException or ArgumentException
        // It should be a Google API related exception (credential, authentication, etc.)
        Assert.IsNotType<ArgumentNullException>(exception);
        Assert.IsNotType<ArgumentException>(exception);
    }

    private static GoogleServiceAccount CreateTestServiceAccount()
    {
        return new GoogleServiceAccount
        {
            Type = "service_account",
            ProjectId = "test-project",
            PrivateKeyId = "test-key-id",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC7VJTUt9Us8cKB\ntest-key-content\n-----END PRIVATE KEY-----\n",
            ClientEmail = "test@test-project.iam.gserviceaccount.com",
            ClientId = "123456789",
            AuthUri = "https://accounts.google.com/o/oauth2/auth",
            TokenUri = "https://oauth2.googleapis.com/token",
            AuthProviderX509CertUrl = "https://www.googleapis.com/oauth2/v1/certs",
            ClientX509CertUrl = "https://www.googleapis.com/robot/v1/metadata/x509/test%40test-project.iam.gserviceaccount.com"
        };
    }
}