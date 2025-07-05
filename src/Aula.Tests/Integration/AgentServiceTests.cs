using Microsoft.Extensions.Logging;
using Moq;
using Aula.Integration;
using Aula.Services;
using Aula.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Aula.Tests.Integration;

public class AgentServiceTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IDataService> _mockDataService;
    private readonly Mock<IOpenAiService> _mockOpenAiService;
    private readonly Mock<IMinUddannelseClient> _mockMinUddannelseClient;

    public AgentServiceTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockDataService = new Mock<IDataService>();
        _mockOpenAiService = new Mock<IOpenAiService>();
        _mockMinUddannelseClient = new Mock<IMinUddannelseClient>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var service = new AgentService(_mockMinUddannelseClient.Object, _mockDataService.Object,
            _mockOpenAiService.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullMinUddannelseClient_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentService(null!, _mockDataService.Object, _mockOpenAiService.Object, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullDataService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentService(_mockMinUddannelseClient.Object, null!, _mockOpenAiService.Object, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullOpenAiService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentService(_mockMinUddannelseClient.Object, _mockDataService.Object, null!, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AgentService(_mockMinUddannelseClient.Object, _mockDataService.Object, _mockOpenAiService.Object, null!));
    }

    [Fact]
    public void AgentService_ImplementsIAgentServiceInterface()
    {
        // Arrange & Act & Assert
        Assert.True(typeof(IAgentService).IsAssignableFrom(typeof(AgentService)));
    }

    [Fact]
    public void AgentService_HasCorrectPublicMethods()
    {
        // Arrange
        var serviceType = typeof(AgentService);

        // Act & Assert
        Assert.NotNull(serviceType.GetMethod("LoginAsync"));
        Assert.NotNull(serviceType.GetMethod("GetWeekLetterAsync"));
        Assert.NotNull(serviceType.GetMethod("GetWeekScheduleAsync"));
        Assert.NotNull(serviceType.GetMethod("SummarizeWeekLetterAsync"));
        Assert.NotNull(serviceType.GetMethod("ExtractKeyInformationFromWeekLetterAsync"));
        Assert.NotNull(serviceType.GetMethod("GetChildByNameAsync"));
        Assert.NotNull(serviceType.GetMethod("GetAllChildrenAsync"));
        Assert.NotNull(serviceType.GetMethod("AskQuestionAboutChildrenAsync"));
        Assert.NotNull(serviceType.GetMethod("ProcessQueryWithToolsAsync"));
    }

    [Fact]
    public void AgentService_HasCorrectNamespace()
    {
        // Arrange
        var serviceType = typeof(AgentService);

        // Act & Assert
        Assert.Equal("Aula.Integration", serviceType.Namespace);
    }

    [Fact]
    public void AgentService_IsPublicClass()
    {
        // Arrange
        var serviceType = typeof(AgentService);

        // Act & Assert
        Assert.True(serviceType.IsPublic);
        Assert.False(serviceType.IsAbstract);
        Assert.False(serviceType.IsSealed);
    }

    [Fact]
    public void AgentService_ConstructorParametersHaveCorrectTypes()
    {
        // Arrange
        var serviceType = typeof(AgentService);
        var constructor = serviceType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Equal(4, parameters.Length);
        Assert.Equal(typeof(IMinUddannelseClient), parameters[0].ParameterType);
        Assert.Equal(typeof(IDataService), parameters[1].ParameterType);
        Assert.Equal(typeof(IOpenAiService), parameters[2].ParameterType);
        Assert.Equal(typeof(ILoggerFactory), parameters[3].ParameterType);
    }

    [Fact]
    public void AgentService_ConstructorParametersHaveCorrectNames()
    {
        // Arrange
        var serviceType = typeof(AgentService);
        var constructor = serviceType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Equal("minUddannelseClient", parameters[0].Name);
        Assert.Equal("dataManager", parameters[1].Name);
        Assert.Equal("openAiService", parameters[2].Name);
        Assert.Equal("loggerFactory", parameters[3].Name);
    }
}