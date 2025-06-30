using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Tests;

public class AgentServiceTests
{
    private readonly Mock<IMinUddannelseClient> _minUddannelseClientMock;
    private readonly Mock<IDataService> _dataManagerMock;
    private readonly Mock<IOpenAiService> _openAiServiceMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly AgentService _agentService;
    private readonly Child _testChild;
    private readonly DateOnly _testDate;

    public AgentServiceTests()
    {
        _minUddannelseClientMock = new Mock<IMinUddannelseClient>();
        _dataManagerMock = new Mock<IDataService>();
        _openAiServiceMock = new Mock<IOpenAiService>();
        _loggerMock = new Mock<ILogger>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        _agentService = new AgentService(
            _minUddannelseClientMock.Object,
            _dataManagerMock.Object,
            _openAiServiceMock.Object,
            _loggerFactoryMock.Object);

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
            .Returns((JObject?)null);

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
            .Returns((JObject?)null);

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

    [Fact]
    public async Task SummarizeWeekLetterAsync_CallsOpenAiService()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Test Class",
                    ["uge"] = "42",
                    ["indhold"] = "Test content"
                }
            }
        };

        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.SummarizeWeekLetterAsync(weekLetter, It.IsAny<ChatInterface>()))
            .ReturnsAsync("Test summary");

        // Act
        var result = await _agentService.SummarizeWeekLetterAsync(_testChild, _testDate);

        // Assert
        Assert.Equal("Test summary", result);
        _openAiServiceMock.Verify(m => m.SummarizeWeekLetterAsync(weekLetter, ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task AskQuestionAboutWeekLetterAsync_CallsOpenAiService()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Test Class",
                    ["uge"] = "42",
                    ["indhold"] = "Test content"
                }
            }
        };

        var question = "Test question";

        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutWeekLetterAsync(weekLetter, question, It.IsAny<ChatInterface>()))
            .ReturnsAsync("Test answer");

        // Act
        var result = await _agentService.AskQuestionAboutWeekLetterAsync(_testChild, _testDate, question);

        // Assert
        Assert.Equal("Test answer", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutWeekLetterAsync(weekLetter, question, ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task ExtractKeyInformationFromWeekLetterAsync_CallsOpenAiService()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Test Class",
                    ["uge"] = "42",
                    ["indhold"] = "Test content"
                }
            }
        };

        var keyInfo = new JObject
        {
            ["events"] = new JArray { "Event 1", "Event 2" },
            ["deadlines"] = new JArray { "Deadline 1" }
        };

        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.ExtractKeyInformationAsync(weekLetter, It.IsAny<ChatInterface>()))
            .ReturnsAsync(keyInfo);

        // Act
        var result = await _agentService.ExtractKeyInformationFromWeekLetterAsync(_testChild, _testDate);

        // Assert
        Assert.Same(keyInfo, result);
        _openAiServiceMock.Verify(m => m.ExtractKeyInformationAsync(weekLetter, ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task AskQuestionAboutWeekLetterAsync_WithContextKey_CallsOpenAiService()
    {
        // Arrange
        var weekLetter = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["klasseNavn"] = "Test Class",
                    ["uge"] = "42",
                    ["indhold"] = "Test content"
                }
            }
        };

        var question = "Test question";
        var contextKey = "test-context";

        _dataManagerMock.Setup(m => m.GetWeekLetter(_testChild))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutWeekLetterAsync(weekLetter, question, contextKey, It.IsAny<ChatInterface>()))
            .ReturnsAsync("Test answer with context");

        // Act
        var result = await _agentService.AskQuestionAboutWeekLetterAsync(_testChild, _testDate, question, contextKey);

        // Assert
        Assert.Equal("Test answer with context", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutWeekLetterAsync(weekLetter, question, contextKey, ChatInterface.Slack), Times.Once);
    }
}