using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Aula.Bots;

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
            new Child { FirstName = TestChild1, LastName = "Test", Colour = "Red" },
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
            [TestChild1] = new JObject
            {
                ["ugebreve"] = new JArray
                {
                    new JObject
                    {
                        ["klasseNavn"] = "TestChild1' Class",
                        ["indhold"] = "TestChild1' activities"
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
        Assert.Equal(3, children.Count); // Emma, TestChild1, and TestChild
        Assert.Contains(children, c => c.FirstName == "Emma");
        Assert.Contains(children, c => c.FirstName == TestChild1);
        Assert.Contains(children, c => c.FirstName == "TestChild");
    }

    // ===========================================
    // PROCESSQUERYWITHTOOLS COMPREHENSIVE TESTS
    // ===========================================

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithDirectOpenAiResponse_ReturnsResponse()
    {
        // Arrange
        var query = "What's the weather like?";
        var contextKey = "test-context";
        var expectedResponse = "I can help you with school-related questions about your children.";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack);

        // Assert
        Assert.Equal(expectedResponse, result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack), Times.Once);
        // Should not call fallback methods
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(It.IsAny<Dictionary<string, JObject>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatInterface>()), Times.Never);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithFallbackResponse_ProcessesFallbackWorkflow()
    {
        // Arrange
        var query = "What do the children have today?";
        var contextKey = "test-context";
        var fallbackResponse = "Based on the week letters, Emma has math at 9 AM.";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        // Setup week letters for children
        var emmaWeekLetter = new JObject { ["content"] = "Emma has math today" };
        var testchild1WeekLetter = new JObject { ["content"] = "TestChild1 has science today" };

        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == "Emma")))
            .Returns(emmaWeekLetter);
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == TestChild1)))
            .Returns(testchild1WeekLetter);
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == "TestChild")))
            .Returns((JObject?)null);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(), 
                It.IsAny<string>(), 
                contextKey, 
                ChatInterface.Slack))
            .ReturnsAsync(fallbackResponse);

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack);

        // Assert
        Assert.Equal(fallbackResponse, result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack), Times.Once);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
            It.Is<Dictionary<string, JObject>>(d => d.Count == 2 && d.ContainsKey("Emma") && d.ContainsKey(TestChild1)),
            It.IsAny<string>(),
            contextKey,
            ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithTodayQuery_EnhancesWithDayContext()
    {
        // Arrange
        var query = "What do they have today?";
        var contextKey = "test-context";
        var currentDayOfWeek = DateTime.Now.DayOfWeek.ToString();
        var expectedEnhancedQuery = $"{query} (Today is {currentDayOfWeek})";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Telegram))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        var weekLetter = new JObject { ["content"] = "Test content" };
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.IsAny<Child>()))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.Is<string>(q => q.Contains($"(Today is {currentDayOfWeek})")),
                contextKey,
                ChatInterface.Telegram))
            .ReturnsAsync("Enhanced response with day context");

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Telegram);

        // Assert
        Assert.Equal("Enhanced response with day context", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
            It.IsAny<Dictionary<string, JObject>>(),
            It.Is<string>(q => q.Contains($"(Today is {currentDayOfWeek})")),
            contextKey,
            ChatInterface.Telegram), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithTomorrowQuery_EnhancesWithTomorrowContext()
    {
        // Arrange
        var query = "What happens tomorrow?";
        var contextKey = "test-context";
        var tomorrowDayOfWeek = DateTime.Now.AddDays(1).DayOfWeek.ToString();

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        var weekLetter = new JObject { ["content"] = "Test content" };
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.IsAny<Child>()))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.Is<string>(q => q.Contains($"(Tomorrow is {tomorrowDayOfWeek})")),
                contextKey,
                ChatInterface.Slack))
            .ReturnsAsync("Enhanced response with tomorrow context");

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal("Enhanced response with tomorrow context", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
            It.IsAny<Dictionary<string, JObject>>(),
            It.Is<string>(q => q.Contains($"(Tomorrow is {tomorrowDayOfWeek})")),
            contextKey,
            ChatInterface.Slack), Times.Once);
    }

    [Theory]
    [InlineData("Hvad skal børnene lave i dag?", true)]
    [InlineData("Hvad har Emma i morgen?", true)]
    [InlineData("Skal TestChild1 i skole?", true)]
    [InlineData("What do the children have today?", false)]
    [InlineData("What is Emma doing tomorrow?", false)]
    [InlineData("Does TestChild1 have school?", false)]
    public async Task ProcessQueryWithToolsAsync_WithDanishQueries_EnhancesWithLanguageInstruction(string query, bool isDanish)
    {
        // Arrange
        var contextKey = "test-context";
        var expectedLanguageInstruction = isDanish ? "(CRITICAL: Respond in Danish - the user asked in Danish)" : "";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        var weekLetter = new JObject { ["content"] = "Test content" };
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.IsAny<Child>()))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.IsAny<string>(),
                contextKey,
                ChatInterface.Slack))
            .ReturnsAsync("Language-appropriate response");

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal("Language-appropriate response", result);
        if (isDanish)
        {
            _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.Is<string>(q => q.Contains("(CRITICAL: Respond in Danish - the user asked in Danish)")),
                contextKey,
                ChatInterface.Slack), Times.Once);
        }
        else
        {
            _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.Is<string>(q => !q.Contains("(CRITICAL: Respond in Danish")),
                contextKey,
                ChatInterface.Slack), Times.Once);
        }
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithNoChildren_ReturnsNoChildrenMessage()
    {
        // Arrange
        var query = "What do the children have today?";
        var contextKey = "test-context";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        // Override setup to return empty children list
        _dataManagerMock.Setup(m => m.GetChildren()).Returns(new List<Child>());

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal("I don't have any children configured.", result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack), Times.Once);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(It.IsAny<Dictionary<string, JObject>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatInterface>()), Times.Never);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithNoWeekLetters_ReturnsNoDataMessage()
    {
        // Arrange
        var query = "What do the children have today?";
        var contextKey = "test-context";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        // Setup all children to have no week letters
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.IsAny<Child>()))
            .Returns((JObject?)null);

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal("I don't have any week letters available at the moment.", result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack), Times.Once);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(It.IsAny<Dictionary<string, JObject>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ChatInterface>()), Times.Never);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithMixedAvailability_ProcessesAvailableChildren()
    {
        // Arrange
        var query = "What do the children have today?";
        var contextKey = "test-context";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Slack))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        // Setup mixed availability - Emma has data, TestChild1 and TestChild don't
        var emmaWeekLetter = new JObject { ["content"] = "Emma has math today" };
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == "Emma")))
            .Returns(emmaWeekLetter);
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == TestChild1)))
            .Returns((JObject?)null);
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.Is<Child>(c => c.FirstName == "TestChild")))
            .Returns((JObject?)null);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.IsAny<string>(),
                contextKey,
                ChatInterface.Slack))
            .ReturnsAsync("Information available for Emma only");

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey);

        // Assert
        Assert.Equal("Information available for Emma only", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
            It.Is<Dictionary<string, JObject>>(d => d.Count == 1 && d.ContainsKey("Emma")),
            It.IsAny<string>(),
            contextKey,
            ChatInterface.Slack), Times.Once);
    }

    [Fact]
    public async Task ProcessQueryWithToolsAsync_WithComplexDanishQuery_EnhancesWithAllContexts()
    {
        // Arrange
        var query = "Hvad skal børnene lave i dag?"; // Danish: "What should the children do today?"
        var contextKey = "test-context";
        var currentDayOfWeek = DateTime.Now.DayOfWeek.ToString();

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Telegram))
            .ReturnsAsync("FALLBACK_TO_EXISTING_SYSTEM");

        var weekLetter = new JObject { ["content"] = "Test content" };
        _dataManagerMock.Setup(m => m.GetWeekLetter(It.IsAny<Child>()))
            .Returns(weekLetter);

        _openAiServiceMock.Setup(m => m.AskQuestionAboutChildrenAsync(
                It.IsAny<Dictionary<string, JObject>>(),
                It.IsAny<string>(),
                contextKey,
                ChatInterface.Telegram))
            .ReturnsAsync("Børnene har matematik i dag"); // Danish response

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey, ChatInterface.Telegram);

        // Assert
        Assert.Equal("Børnene har matematik i dag", result);
        _openAiServiceMock.Verify(m => m.AskQuestionAboutChildrenAsync(
            It.IsAny<Dictionary<string, JObject>>(),
            It.Is<string>(q => 
                q.Contains($"(Today is {currentDayOfWeek})") && 
                q.Contains("(CRITICAL: Respond in Danish - the user asked in Danish)")),
            contextKey,
            ChatInterface.Telegram), Times.Once);
    }

    [Theory]
    [InlineData(ChatInterface.Slack)]
    [InlineData(ChatInterface.Telegram)]
    public async Task ProcessQueryWithToolsAsync_WithDifferentChatInterfaces_PassesCorrectInterface(ChatInterface chatInterface)
    {
        // Arrange
        var query = "Test query";
        var contextKey = "test-context";
        var expectedResponse = "Interface-specific response";

        _openAiServiceMock.Setup(m => m.ProcessQueryWithToolsAsync(query, contextKey, chatInterface))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _agentService.ProcessQueryWithToolsAsync(query, contextKey, chatInterface);

        // Assert
        Assert.Equal(expectedResponse, result);
        _openAiServiceMock.Verify(m => m.ProcessQueryWithToolsAsync(query, contextKey, chatInterface), Times.Once);
    }

}