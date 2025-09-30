using Microsoft.Extensions.Logging;
using Moq;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Services;

public class MinUddannelseClientTests
{
	private readonly Mock<ISupabaseService> _mockSupabaseService;
	private readonly Mock<ILoggerFactory> _mockLoggerFactory;
	private readonly Mock<ILogger<MinUddannelseClient>> _mockLogger;
	private readonly Config _testConfig;
	private readonly Child _testChild;

	public MinUddannelseClientTests()
	{
		_mockSupabaseService = new Mock<ISupabaseService>();
		_mockLoggerFactory = new Mock<ILoggerFactory>();
		_mockLogger = new Mock<ILogger<MinUddannelseClient>>();

		_mockLoggerFactory.Setup(x => x.CreateLogger(typeof(MinUddannelseClient).FullName!)).Returns(_mockLogger.Object);

		_testConfig = new Config
		{
			UniLogin = new UniLogin { Username = "testuser", Password = "testpass" },
			Features = new Features
			{
				UseMockData = false,
				MockCurrentWeek = 25,
				MockCurrentYear = 2024
			}
		};

		_testChild = new Child { FirstName = "Emma", LastName = "Test" };
	}

	[Fact]
	public void Constructor_WithConfig_InitializesCorrectly()
	{
		var client = new MinUddannelseClient(_testConfig);
		Assert.NotNull(client);
	}

	[Fact]
	public void Constructor_WithConfigAndServices_InitializesCorrectly()
	{
		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
		Assert.NotNull(client);
	}

	[Fact]
	public void Constructor_WithUsernamePassword_InitializesCorrectly()
	{
		var client = new MinUddannelseClient("testuser", "testpass");
		Assert.NotNull(client);
	}

	[Fact]
	public async Task GetWeekLetter_MockModeEnabled_WithStoredData_ReturnsStoredWeekLetter()
	{
		_testConfig.Features.UseMockData = true;
		var storedContent = "{\"test\":\"stored data\"}";
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ReturnsAsync(storedContent);

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
		var testDate = new DateOnly(2024, 6, 17);

		var result = await client.GetWeekLetter(_testChild, testDate);

		Assert.NotNull(result);
		Assert.Equal("stored data", result["test"]?.ToString());
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
	}

	[Fact]
	public async Task GetWeekLetter_MockModeEnabled_NoStoredData_ReturnsEmptyWeekLetter()
	{
		_testConfig.Features.UseMockData = true;
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ReturnsAsync((string?)null);

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
		var testDate = new DateOnly(2024, 6, 17);

		var result = await client.GetWeekLetter(_testChild, testDate);

		Assert.NotNull(result);
		Assert.NotNull(result["ugebreve"]);
		var ugebreve = result["ugebreve"] as JArray;
		Assert.NotNull(ugebreve);
		Assert.Single(ugebreve);

		var firstItem = ugebreve[0] as JObject;
		Assert.NotNull(firstItem);
		Assert.Equal("Mock Class", firstItem["klasseNavn"]?.ToString());
		Assert.Equal("25", firstItem["uge"]?.ToString());
		Assert.Contains("mock mode", firstItem["indhold"]?.ToString());
	}

	[Fact]
	public async Task GetStoredWeekLetter_WithSupabaseService_CallsService()
	{
		var storedContent = "{\"stored\":\"data\"}";
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ReturnsAsync(storedContent);

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

		Assert.NotNull(result);
		Assert.Equal("data", result["stored"]?.ToString());
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
	}

	[Fact]
	public async Task GetStoredWeekLetter_NoSupabaseService_ReturnsNull()
	{
		var client = new MinUddannelseClient(_testConfig);

		var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

		Assert.Null(result);
	}

	[Fact]
	public async Task GetStoredWeekLetter_SupabaseServiceThrowsException_ReturnsNull()
	{
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ThrowsAsync(new InvalidOperationException("Database connection failed"));

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

		Assert.Null(result);
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
	}

	[Fact]
	public async Task GetStoredWeekLetter_SupabaseServiceReturnsInvalidJson_ReturnsNull()
	{
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ReturnsAsync("invalid json content {{{");

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

		Assert.Null(result);
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
	}

	[Fact]
	public async Task GetWeekLetter_MockModeWithSupabaseException_ThrowsException()
	{
		_testConfig.Features.UseMockData = true;
		_mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
			.ThrowsAsync(new TimeoutException("Request timeout"));

		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
		var testDate = new DateOnly(2024, 6, 17);

		await Assert.ThrowsAsync<TimeoutException>(() => client.GetWeekLetter(_testChild, testDate));
		_mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
	}

	[Fact]
	public void Constructor_WithNullConfig_ThrowsNullReferenceException()
	{
		Assert.Throws<NullReferenceException>(() => new MinUddannelseClient((Config)null!));
	}

	[Fact]
	public void Constructor_WithNullUsername_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new MinUddannelseClient(null!, "password"));
	}

	[Fact]
	public void Constructor_WithNullPassword_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new MinUddannelseClient("username", null!));
	}

	[Fact]
	public void Constructor_WithConfigAndServices_VerifyDependencyInjection()
	{
		var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);

		Assert.NotNull(client);
		_mockLoggerFactory.Verify(x => x.CreateLogger(typeof(MinUddannelseClient).FullName!), Times.Once());
	}
}
