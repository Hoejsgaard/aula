using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;

namespace Aula.Tests.Integration;

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

        _testChild = new Child
        {
            FirstName = "TestChild",
            LastName = "TestLastName",
            Colour = "Blue"
        };

        _testDate = new DateOnly(2023, 10, 16); // Week 42 of 2023

        // Setup the data manager to return test children
        var testChildren = new List<Child>
        {
            new Child { FirstName = "Emma", LastName = "Test", Colour = "Blue" },
            new Child { FirstName = "Hans", LastName = "Test", Colour = "Red" },
            _testChild
        };

        _dataManagerMock.Setup(m => m.GetChildren()).Returns(testChildren);

        _agentService = new AgentService(
            _minUddannelseClientMock.Object,
            _dataManagerMock.Object,
            _openAiServiceMock.Object,
            _loggerFactoryMock.Object);
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

    [Fact]
    public async Task LoginAsync_WhenLoginFails_ReturnsFalse()
    {
        // Arrange
        _minUddannelseClientMock.Setup(m => m.LoginAsync())
            .ReturnsAsync(false);

        // Act
        var result = await _agentService.LoginAsync();

        // Assert
        Assert.False(result);
        _minUddannelseClientMock.Verify(m => m.LoginAsync(), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetterAsync_WithCacheDisabled_FetchesFromApi()
    {
        // Arrange
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
        var result = await _agentService.GetWeekLetterAsync(_testChild, _testDate, useCache: false);

        // Assert
        Assert.Same(apiWeekLetter, result);
        // Should not check cache when useCache is false
        _dataManagerMock.Verify(m => m.GetWeekLetter(_testChild), Times.Never);
        _minUddannelseClientMock.Verify(m => m.GetWeekLetter(_testChild, _testDate), Times.Once);
        _dataManagerMock.Verify(m => m.CacheWeekLetter(_testChild, apiWeekLetter), Times.Once);
    }

    [Fact]
    public async Task GetWeekScheduleAsync_ReturnsCachedData_WhenAvailable()
    {
        // Arrange
        var cachedSchedule = new JObject
        {
            ["skema"] = new JArray
            {
                new JObject
                {
                    ["tid"] = "08:00",
                    ["fag"] = "Cached Subject"
                }
            }
        };

        _dataManagerMock.Setup(m => m.GetWeekSchedule(_testChild))
            .Returns(cachedSchedule);

        // Act
        var result = await _agentService.GetWeekScheduleAsync(_testChild, _testDate);

        // Assert
        Assert.Same(cachedSchedule, result);
        _dataManagerMock.Verify(m => m.GetWeekSchedule(_testChild), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekSchedule(It.IsAny<Child>(), It.IsAny<DateOnly>()), Times.Never);
    }

    [Fact]
    public async Task GetWeekScheduleAsync_FetchesAndCachesData_WhenNotCached()
    {
        // Arrange
        _dataManagerMock.Setup(m => m.GetWeekSchedule(_testChild))
            .Returns((JObject?)null);

        var apiSchedule = new JObject
        {
            ["skema"] = new JArray
            {
                new JObject
                {
                    ["tid"] = "09:00",
                    ["fag"] = "API Subject"
                }
            }
        };

        _minUddannelseClientMock.Setup(m => m.GetWeekSchedule(_testChild, _testDate))
            .ReturnsAsync(apiSchedule);

        // Act
        var result = await _agentService.GetWeekScheduleAsync(_testChild, _testDate);

        // Assert
        Assert.Same(apiSchedule, result);
        _dataManagerMock.Verify(m => m.GetWeekSchedule(_testChild), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekSchedule(_testChild, _testDate), Times.Once);
        _dataManagerMock.Verify(m => m.CacheWeekSchedule(_testChild, apiSchedule), Times.Once);
    }

    [Fact]
    public async Task GetWeekScheduleAsync_LogsInFirst_WhenNotLoggedIn()
    {
        // Arrange
        _dataManagerMock.Setup(m => m.GetWeekSchedule(_testChild))
            .Returns((JObject?)null);

        var apiSchedule = new JObject
        {
            ["skema"] = new JArray
            {
                new JObject
                {
                    ["tid"] = "10:00",
                    ["fag"] = "Test Subject"
                }
            }
        };

        _minUddannelseClientMock.Setup(m => m.LoginAsync())
            .ReturnsAsync(true);

        _minUddannelseClientMock.Setup(m => m.GetWeekSchedule(_testChild, _testDate))
            .ReturnsAsync(apiSchedule);

        // Act
        var result = await _agentService.GetWeekScheduleAsync(_testChild, _testDate);

        // Assert
        Assert.Same(apiSchedule, result);
        _minUddannelseClientMock.Verify(m => m.LoginAsync(), Times.Once);
        _minUddannelseClientMock.Verify(m => m.GetWeekSchedule(_testChild, _testDate), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_CallsOpenAiService()
    {
        // Arrange
        var query = "Test query";
        var contextKey = "test-context";
        var expectedResponse = "Test response from tools";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, It.IsAny<ChatInterface>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal(expectedResponse, result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task AskQuestionAboutChildrenAsync_CallsOpenAiService()
    {
        // Arrange
        var childrenWeekLetters = new Dictionary<string, JObject>
        {
            ["Emma"] = new JObject
            {
                ["ugebreve"] = new JArray
                {
                    new JObject
                    {
                        ["klasseNavn"] = "Emma's Class",
                        ["indhold"] = "Emma's activities"
                    }
                }
            },
            ["Hans"] = new JObject
            {
                ["ugebreve"] = new JArray
                {
                    new JObject
                    {
                        ["klasseNavn"] = "Hans' Class",
                        ["indhold"] = "Hans' activities"
                    }
                }
            }
        };

        var question = "What activities do the children have this week?";
        var contextKey = "multi-child-context";
        var expectedResponse = "Both children have various activities";

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(childrenWeekLetters, question, contextKey, It.IsAny<ChatInterface>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _agentService.AskQuestionAboutChildrenAsync(childrenWeekLetters, question, contextKey);

        // Assert
        Assert.Equal(expectedResponse, result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(childrenWeekLetters, question, contextKey, ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task GetChildByNameAsync_ReturnsMatchingChild_WhenFound()
    {
        // Act
        var result = await _agentService.GetChildByNameAsync("Emma");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Emma", result.FirstName);
        Assert.Equal("Test", result.LastName);
    }

    [Fact]
    public async Task GetChildByNameAsync_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _agentService.GetChildByNameAsync("NonExistentChild");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetChildByNameAsync_IsCaseInsensitive()
    {
        // Act
        var result = await _agentService.GetChildByNameAsync("emma");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Emma", result.FirstName);
    }

    [Fact]
    public async Task GetAllChildrenAsync_ReturnsAllConfiguredChildren()
    {
        // Act
        var result = await _agentService.GetAllChildrenAsync();

        // Assert
        var children = result.ToList();
        Assert.Equal(3, children.Count); // Emma, Hans, and TestChild
        Assert.Contains(children, c => c.FirstName == "Emma");
        Assert.Contains(children, c => c.FirstName == "Hans");
        Assert.Contains(children, c => c.FirstName == "TestChild");
    }

}