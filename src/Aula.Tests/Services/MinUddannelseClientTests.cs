using Microsoft.Extensions.Logging;
using Moq;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Aula.Repositories;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Services;

public class MinUddannelseClientTests
{
    private readonly Mock<IWeekLetterRepository> _mockWeekLetterRepository;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<MinUddannelseClient>> _mockLogger;
    private readonly Config _testConfig;
    private readonly Child _testChild;

    public MinUddannelseClientTests()
    {
        _mockWeekLetterRepository = new Mock<IWeekLetterRepository>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<MinUddannelseClient>>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(MinUddannelseClient).FullName!)).Returns(_mockLogger.Object);

        _testConfig = new Config
        {
            UniLogin = new UniLogin { Username = "testuser", Password = "testpass" }
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
        var client = new MinUddannelseClient(_testConfig, _mockWeekLetterRepository.Object, _mockLoggerFactory.Object);
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetStoredWeekLetter_WithSupabaseService_CallsService()
    {
        var storedContent = "{\"stored\":\"data\"}";
        _mockWeekLetterRepository.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
            .ReturnsAsync(storedContent);

        var client = new MinUddannelseClient(_testConfig, _mockWeekLetterRepository.Object, _mockLoggerFactory.Object);

        var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

        Assert.NotNull(result);
        Assert.Equal("data", result["stored"]?.ToString());
        _mockWeekLetterRepository.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
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
        _mockWeekLetterRepository.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var client = new MinUddannelseClient(_testConfig, _mockWeekLetterRepository.Object, _mockLoggerFactory.Object);

        var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

        Assert.Null(result);
        _mockWeekLetterRepository.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
    }

    [Fact]
    public async Task GetStoredWeekLetter_SupabaseServiceReturnsInvalidJson_ReturnsNull()
    {
        _mockWeekLetterRepository.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
            .ReturnsAsync("invalid json content {{{");

        var client = new MinUddannelseClient(_testConfig, _mockWeekLetterRepository.Object, _mockLoggerFactory.Object);

        var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

        Assert.Null(result);
        _mockWeekLetterRepository.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once());
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => new MinUddannelseClient((Config)null!));
    }

    [Fact]
    public void Constructor_WithConfigAndServices_VerifyDependencyInjection()
    {
        var client = new MinUddannelseClient(_testConfig, _mockWeekLetterRepository.Object, _mockLoggerFactory.Object);

        Assert.NotNull(client);
        _mockLoggerFactory.Verify(x => x.CreateLogger(typeof(MinUddannelseClient).FullName!), Times.Once());
    }
}
