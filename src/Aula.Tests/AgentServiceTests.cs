using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace Aula.Tests;

public class AgentServiceTests
{
    private readonly Mock<IMinUddannelseClient> _minUddannelseClientMock;
    private readonly Mock<IDataManager> _dataManagerMock;
    private readonly Mock<ILogger<AgentService>> _loggerMock;
    private readonly AgentService _agentService;
    private readonly Child _testChild;
    private readonly DateOnly _testDate;

    public AgentServiceTests()
    {
        _minUddannelseClientMock = new Mock<IMinUddannelseClient>();
        _dataManagerMock = new Mock<IDataManager>();
        _loggerMock = new Mock<ILogger<AgentService>>();
        _agentService = new AgentService(_minUddannelseClientMock.Object, _dataManagerMock.Object, _loggerMock.Object);

        _testChild = new Child
        {
            FirstName = "TestChild",
            LastName = "TestLastName",
            Colour = "Blue"
        };

        _testDate = new DateOnly(2023, 10, 16); // Week 42 of 2023
    }

    [Fact]
    public async Task LoginAsync_CallsClientLoginAsync()
    {
        // Arrange
        _minUddannelseClientMock.Setup(m => m.LoginAsync())
            .ReturnsAsync(true);

        // Act
        var result = await _agentService.LoginAsync();

        // Assert
        Assert.True(result);
        _minUddannelseClientMock.Verify(m => m.LoginAsync(), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_ReturnsCachedData_WhenAvailable()
    {
        // Arrange
        var cachedWeekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Cached Class",
                    ["uge"] = "42",
                    ["indhold"] = "Cached content"
                }
            }
        };

        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns(cachedWeekLetter);

        // Act
        var result = await _agentService.GetWeekLetterAsync(_testChild, _testDate);

        // Assert
        Assert.Same(cachedWeekLetter, result);
        _dataManagerMock.Verify(m => m.GetWeekLetter(_testChild), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekLetter(It.IsAny<Child>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task GetWeekLetterAsync_FetchesAndCachesData_WhenNotCached()
    {
        // Arrange
        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns((JObject)null);

        var apiWeekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "API Class",
                    ["uge"] = "42",
                    ["indhold"] = "API content"
                }
            }
        };

        _minUddannelseClientMock.Setup(m => m.GetWeekLetter(_testChild, _testDate))
            .ReturnsAsync(apiWeekLetter);

        // Act
        var result = await _agentService.GetWeekLetterAsync(_testChild, _testDate);

        // Assert
        Assert.Same(apiWeekLetter, result);
        _dataManagerMock.Verify(m => m.GetWeekLetter(_testChild), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekLetter(_testChild, _testDate), Times.Once);
        _dataManagerMock.Verify(m => m.CacheWeekLetter(_testChild, apiWeekLetter), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_LogsInFirst_WhenNotLoggedIn()
    {
        // Arrange
        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns((JObject)null);

        var apiWeekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "API Class",
                    ["uge"] = "42",
                    ["indhold"] = "API content"
                }
            }
        };

        _minUddannelseClientMock.Setup(m => m.LoginAsync())
            .ReturnsAsync(true);

        _minUddannelseClientMock.Setup(m => m.GetWeekLetter(_testChild, _testDate))
            .ReturnsAsync(apiWeekLetter);

        // Act
        var result = await _agentService.GetWeekLetterAsync(_testChild, _testDate);

        // Assert
        Assert.Same(apiWeekLetter, result);
        _minUddannelseClientMock.Verify(m => m.LoginAsync(), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekLetter(_testChild, _testDate), Times.Once);
    }
} 