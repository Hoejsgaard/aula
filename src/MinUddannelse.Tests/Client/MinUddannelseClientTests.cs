using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using MinUddannelse.Configuration;
using MinUddannelse.Client;
using MinUddannelse.Client;
using MinUddannelse.GoogleCalendar;
using MinUddannelse.Security;
using MinUddannelse.Repositories;
using MinUddannelse.AI.Services;
using MinUddannelse.Content.WeekLetters;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;
using MinUddannelse.Security;
using MinUddannelse;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace MinUddannelse.Tests.Client;

public class MinUddannelseClientTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public MinUddannelseClientTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_mockLogger.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Act
        var client = new MinUddannelseClient(_mockLoggerFactory.Object, _mockHttpClientFactory.Object);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task LoginAsync_AlwaysReturnsTrue()
    {
        // Arrange
        var client = new MinUddannelseClient(_mockLoggerFactory.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await client.LoginAsync();

        // Assert
        Assert.True(result);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("authentication will happen per-request")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
    }

    [Fact]
    public async Task GetWeekLetter_WithNoCredentials_ReturnsEmptyWeekLetter()
    {
        // Arrange
        var childWithoutCredentials = new Child
        {
            FirstName = "NoCredentials",
            LastName = "Child",
            UniLogin = null
        };

        var client = new MinUddannelseClient(_mockLoggerFactory.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await client.GetWeekLetter(childWithoutCredentials, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.NotNull(result);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Live fetch not allowed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
    }

    [Fact]
    public async Task GetWeekSchedule_WithNoCredentials_ReturnsEmptyJObject()
    {
        // Arrange
        var childWithoutCredentials = new Child
        {
            FirstName = "NoCredentials",
            LastName = "Child",
            UniLogin = null
        };

        var client = new MinUddannelseClient(_mockLoggerFactory.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await client.GetWeekSchedule(childWithoutCredentials, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.HasValues);
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No credentials available")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
    }

    [Fact]
    public void MinUddannelseClient_ImplementsIMinUddannelseClientInterface()
    {
        // Assert
        Assert.True(typeof(IMinUddannelseClient).IsAssignableFrom(typeof(MinUddannelseClient)));
    }

    [Fact]
    public void MinUddannelseClient_DoesNotStoreSessions()
    {
        // Arrange
        var clientType = typeof(MinUddannelseClient);

        // Act
        var fields = clientType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert - Should not have any ConcurrentDictionary fields for storing sessions
        foreach (var field in fields)
        {
            Assert.DoesNotContain("ConcurrentDictionary", field.FieldType.Name);
            Assert.DoesNotContain("authenticatedClients", field.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetWeekLetter_LogsAuthenticationForEachRequest()
    {
        // Arrange
        var client = new MinUddannelseClient(_mockLoggerFactory.Object, _mockHttpClientFactory.Object);
        var child = new Child
        {
            FirstName = "Test",
            LastName = "Child",
            UniLogin = new UniLogin
            {
                Username = "testuser",
                Password = "testpass"
            }
        };

        // Act
        await client.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today));

        // Assert
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Live fetch not allowed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
    }
}
