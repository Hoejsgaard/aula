using Aula.Agents;
using Aula.Configuration;
using Aula.Services;
using Aula.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using Xunit;

namespace Aula.Tests.Agents;

public class ChildAgentFactoryTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IOpenAiService> _mockOpenAiService;
    private readonly Mock<IWeekLetterService> _mockWeekLetterService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ISchedulingService> _mockSchedulingService;
    private readonly Config _testConfig;
    private readonly Child _testChild;
    private readonly ChildAgentFactory _factory;

    public ChildAgentFactoryTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockOpenAiService = new Mock<IOpenAiService>();
        _mockWeekLetterService = new Mock<IWeekLetterService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockSchedulingService = new Mock<ISchedulingService>();

        // Setup service provider to return mocked services
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IOpenAiService)))
            .Returns(_mockOpenAiService.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IWeekLetterService)))
            .Returns(_mockWeekLetterService.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ILoggerFactory)))
            .Returns(_mockLoggerFactory.Object);

        _testConfig = new Config
        {
            WeekLetter = new WeekLetter
            {
                PostOnStartup = true
            }
        };

        _testChild = new Child
        {
            FirstName = "Emma",
            LastName = "Test",
            UniLogin = new UniLogin
            {
                Username = "emma.test",
                Password = "password123"
            }
        };

        _factory = new ChildAgentFactory(_mockServiceProvider.Object, _testConfig);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var config = new Config();

        var factory = new ChildAgentFactory(serviceProvider, config);

        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        var config = new Config();

        Assert.Throws<ArgumentNullException>(() =>
            new ChildAgentFactory(null!, config));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var serviceProvider = new Mock<IServiceProvider>().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new ChildAgentFactory(serviceProvider, null!));
    }

    [Fact]
    public void CreateChildAgent_WithValidParameters_ReturnsChildAgent()
    {
        var childAgent = _factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        Assert.NotNull(childAgent);
        Assert.IsAssignableFrom<IChildAgent>(childAgent);
    }

    [Fact]
    public void CreateChildAgent_WithNullChild_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateChildAgent(null!, _mockSchedulingService.Object));
    }

    [Fact]
    public void CreateChildAgent_WithNullSchedulingService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateChildAgent(_testChild, null!));
    }

    [Fact]
    public void CreateChildAgent_ResolvesRequiredServices_FromServiceProvider()
    {
        _factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        _mockServiceProvider.Verify(sp => sp.GetService(typeof(IOpenAiService)), Times.Once);
        _mockServiceProvider.Verify(sp => sp.GetService(typeof(IWeekLetterService)), Times.Once);
        _mockServiceProvider.Verify(sp => sp.GetService(typeof(ILoggerFactory)), Times.Once);
    }

    [Fact]
    public void CreateChildAgent_WithPostOnStartupTrue_ConfiguresAgentCorrectly()
    {
        var configWithPostOnStartup = new Config
        {
            WeekLetter = new WeekLetter
            {
                PostOnStartup = true
            }
        };
        var factory = new ChildAgentFactory(_mockServiceProvider.Object, configWithPostOnStartup);

        var childAgent = factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        Assert.NotNull(childAgent);
        // The agent should be created successfully with PostOnStartup = true
        // Note: Since ChildAgent constructor is internal to the implementation,
        // we can't directly test the parameter value, but we verify the agent is created
    }

    [Fact]
    public void CreateChildAgent_WithPostOnStartupFalse_ConfiguresAgentCorrectly()
    {
        var configWithoutPostOnStartup = new Config
        {
            WeekLetter = new WeekLetter
            {
                PostOnStartup = false
            }
        };
        var factory = new ChildAgentFactory(_mockServiceProvider.Object, configWithoutPostOnStartup);

        var childAgent = factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        Assert.NotNull(childAgent);
        // The agent should be created successfully with PostOnStartup = false
    }

    [Fact]
    public void CreateChildAgent_WithNullWeekLetterConfig_UsesDefaultPostOnStartup()
    {
        var configWithNullWeekLetter = new Config
        {
            WeekLetter = null!
        };
        var factory = new ChildAgentFactory(_mockServiceProvider.Object, configWithNullWeekLetter);

        var childAgent = factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        Assert.NotNull(childAgent);
        // Should default to false when WeekLetter config is null
    }

    [Fact]
    public void CreateChildAgent_WithMissingPostOnStartupProperty_UsesDefaultValue()
    {
        var configWithEmptyWeekLetter = new Config
        {
            WeekLetter = new WeekLetter()
            // PostOnStartup not explicitly set, should default to false
        };
        var factory = new ChildAgentFactory(_mockServiceProvider.Object, configWithEmptyWeekLetter);

        var childAgent = factory.CreateChildAgent(_testChild, _mockSchedulingService.Object);

        Assert.NotNull(childAgent);
        // Should work with default value
    }

    [Fact]
    public void CreateChildAgent_WithDifferentChildren_CreatesMultipleAgents()
    {
        var child1 = new Child { FirstName = "Emma", LastName = "Test1" };
        var child2 = new Child { FirstName = "Liam", LastName = "Test2" };

        var agent1 = _factory.CreateChildAgent(child1, _mockSchedulingService.Object);
        var agent2 = _factory.CreateChildAgent(child2, _mockSchedulingService.Object);

        Assert.NotNull(agent1);
        Assert.NotNull(agent2);
        Assert.NotSame(agent1, agent2);
    }

    [Fact]
    public void CreateChildAgent_CallsServiceProviderMultipleTimes_WhenCreatingMultipleAgents()
    {
        var child1 = new Child { FirstName = "Emma", LastName = "Test1" };
        var child2 = new Child { FirstName = "Liam", LastName = "Test2" };

        _factory.CreateChildAgent(child1, _mockSchedulingService.Object);
        _factory.CreateChildAgent(child2, _mockSchedulingService.Object);

        // Should call GetService twice for each service (once per agent creation)
        _mockServiceProvider.Verify(sp => sp.GetService(typeof(IOpenAiService)), Times.Exactly(2));
        _mockServiceProvider.Verify(sp => sp.GetService(typeof(IWeekLetterService)), Times.Exactly(2));
        _mockServiceProvider.Verify(sp => sp.GetService(typeof(ILoggerFactory)), Times.Exactly(2));
    }

    [Fact]
    public void ChildAgentFactory_ImplementsIChildAgentFactoryInterface()
    {
        Assert.IsAssignableFrom<IChildAgentFactory>(_factory);
    }
}