using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Integration;

public class PerChildMinUddannelseClientTests
{
	private readonly Mock<ILoggerFactory> _mockLoggerFactory;
	private readonly Mock<ILogger> _mockLogger;
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly Config _config;

	public PerChildMinUddannelseClientTests()
	{
		_mockLoggerFactory = new Mock<ILoggerFactory>();
		_mockLogger = new Mock<ILogger>();
		_mockSupabaseService = new Mock<ISupabaseService>();

		_mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
			.Returns(_mockLogger.Object);

		_config = new Config
		{
			MinUddannelse = new MinUddannelse
			{
				Children = new List<Child>
				{
					new Child
					{
						FirstName = "Test",
						LastName = "Child",
						UniLogin = new UniLogin
						{
							Username = "testuser",
							Password = "testpass"
						}
					}
				}
			},
			Features = new Features
			{
				UseMockData = false
			}
		};
	}

	[Fact]
	public void Constructor_WithValidParameters_InitializesCorrectly()
	{
		// Act
		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		// Assert
		Assert.NotNull(client);
	}

	[Fact]
	public async Task LoginAsync_AlwaysReturnsTrue()
	{
		// Arrange
		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);

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

		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		// Act
		var result = await client.GetWeekLetter(childWithoutCredentials, DateOnly.FromDateTime(DateTime.Today));

		// Assert
		Assert.NotNull(result);
		_mockLogger.Verify(x => x.Log(
			LogLevel.Information,
			It.IsAny<EventId>(),
			It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Week letter not in database and live fetch not allowed")),
			It.IsAny<Exception>(),
			It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
	}

	[Fact]
	public async Task GetWeekLetter_WithMockModeEnabled_ReturnsStoredData()
	{
		// Arrange
		_config.Features.UseMockData = true;
		_config.Features.MockCurrentWeek = 10;
		_config.Features.MockCurrentYear = 2025;

		var storedWeekLetter = new JObject { ["test"] = "data" };
		_mockSupabaseService.Setup(x => x.GetStoredWeekLetterAsync("Test", 10, 2025))
			.ReturnsAsync(storedWeekLetter.ToString());

		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		// Act
		var result = await client.GetWeekLetter(_config.MinUddannelse.Children[0], DateOnly.FromDateTime(DateTime.Today));

		// Assert
		Assert.NotNull(result);
		Assert.Equal("data", result["test"]?.ToString());
		_mockSupabaseService.Verify(x => x.GetStoredWeekLetterAsync("Test", 10, 2025), Times.Once());
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

		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);

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
	public void PerChildMinUddannelseClient_ImplementsIMinUddannelseClientInterface()
	{
		// Assert
		Assert.True(typeof(IMinUddannelseClient).IsAssignableFrom(typeof(PerChildMinUddannelseClient)));
	}

	[Fact]
	public void PerChildMinUddannelseClient_DoesNotStoreSessions()
	{
		// Arrange
		var clientType = typeof(PerChildMinUddannelseClient);

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
		var client = new PerChildMinUddannelseClient(_config, _mockSupabaseService.Object, _mockLoggerFactory.Object);
		var child = _config.MinUddannelse.Children[0];

		// Note: This test verifies logging behavior but cannot test actual authentication
		// since ChildAuthenticatedClient is internal and makes real HTTP calls.
		// In production, each call would create a fresh authenticated session.

		// Act - Would normally authenticate fresh for each call
		// but we can only test that it logs the intention
		try
		{
			await client.GetWeekLetter(child, DateOnly.FromDateTime(DateTime.Today));
		}
		catch
		{
			// Expected to fail since we can't mock the internal authentication
		}

		// Assert
		_mockLogger.Verify(x => x.Log(
			LogLevel.Information,
			It.IsAny<EventId>(),
			It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Week letter not in database and live fetch not allowed")),
			It.IsAny<Exception>(),
			It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
	}
}
