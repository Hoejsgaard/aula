using Microsoft.Extensions.Logging;
using Moq;
using Aula.Services;
using Aula.Configuration;
using Aula.Integration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Services;

public class HistoricalDataSeederTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<HistoricalDataSeeder>> _mockLogger;
    private readonly Config _config;
    private readonly HistoricalDataSeeder _seeder;

    public HistoricalDataSeederTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<HistoricalDataSeeder>>();

        // Use real LoggerFactory to avoid extension method mocking issues
        var loggerFactory = new LoggerFactory();

        _config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Emma", LastName = "Test" },
                    new Child { FirstName = "Lucas", LastName = "Test" }
                }
            }
        };

        _seeder = new HistoricalDataSeeder(loggerFactory, _mockAgentService.Object, _mockSupabaseService.Object, _config);
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var loggerFactory = new LoggerFactory();

        // Act
        var seeder = new HistoricalDataSeeder(loggerFactory, _mockAgentService.Object, _mockSupabaseService.Object, _config);

        // Assert
        Assert.NotNull(seeder);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsNullReferenceException()
    {
        // Act & Assert
        Assert.Throws<NullReferenceException>(() =>
            new HistoricalDataSeeder(null!, _mockAgentService.Object, _mockSupabaseService.Object, _config));
    }

    [Fact]
    public void Constructor_WithNullAgentService_CreatesInstanceWithoutError()
    {
        // Arrange
        var loggerFactory = new LoggerFactory();

        // Act
        var seeder = new HistoricalDataSeeder(loggerFactory, null!, _mockSupabaseService.Object, _config);

        // Assert - Constructor should not throw even with null agent service
        Assert.NotNull(seeder);
    }

    [Fact]
    public void Constructor_WithNullSupabaseService_CreatesInstanceWithoutError()
    {
        // Arrange
        var loggerFactory = new LoggerFactory();

        // Act
        var seeder = new HistoricalDataSeeder(loggerFactory, _mockAgentService.Object, null!, _config);

        // Assert - Constructor should not throw even with null supabase service
        Assert.NotNull(seeder);
    }

    [Fact]
    public void Constructor_WithNullConfig_CreatesInstanceWithoutError()
    {
        // Arrange
        var loggerFactory = new LoggerFactory();

        // Act
        var seeder = new HistoricalDataSeeder(loggerFactory, _mockAgentService.Object, _mockSupabaseService.Object, null!);

        // Assert - Constructor should not throw even with null config
        Assert.NotNull(seeder);
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithLoginFailure_SkipsSeeding()
    {
        // Arrange
        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(false);

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockAgentService.Verify(s => s.LoginAsync(), Times.Once());
        _mockAgentService.Verify(s => s.GetAllChildrenAsync(), Times.Never());
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithNoChildren_SkipsSeeding()
    {
        // Arrange
        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(true);
        _mockAgentService.Setup(s => s.GetAllChildrenAsync()).ReturnsAsync(new List<Child>());

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockAgentService.Verify(s => s.LoginAsync(), Times.Once());
        _mockAgentService.Verify(s => s.GetAllChildrenAsync(), Times.Once());
        _mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithSuccessfulLogin_ProcessesChildren()
    {
        // Arrange
        var children = new List<Child>
        {
            new Child { FirstName = "Emma", LastName = "Test" }
        };

        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(true);
        _mockAgentService.Setup(s => s.GetAllChildrenAsync()).ReturnsAsync(children);
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync("existing content"); // Simulate existing content to avoid GetWeekLetterAsync calls

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockAgentService.Verify(s => s.LoginAsync(), Times.Once());
        _mockAgentService.Verify(s => s.GetAllChildrenAsync(), Times.Once());
        // Should check for existing content for 8 weeks * 1 child = 8 times
        _mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync("Emma", It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(8));
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithNewWeekLetter_StoresData()
    {
        // Arrange
        var children = new List<Child>
        {
            new Child { FirstName = "Emma", LastName = "Test" }
        };

        var weekLetterJson = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["indhold"] = "Valid week letter content"
                }
            }
        };

        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(true);
        _mockAgentService.Setup(s => s.GetAllChildrenAsync()).ReturnsAsync(children);
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string)null!); // No existing content
        _mockAgentService.Setup(s => s.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>(), true))
            .ReturnsAsync(weekLetterJson);
        _mockSupabaseService.Setup(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(
            "Emma", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Exactly(8)); // Should store for all 8 weeks
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithEmptyWeekLetter_SkipsStoring()
    {
        // Arrange
        var children = new List<Child>
        {
            new Child { FirstName = "Emma", LastName = "Test" }
        };

        var emptyWeekLetterJson = new JObject
        {
            ["ugebreve"] = new JArray
            {
                new JObject
                {
                    ["indhold"] = "Der er ikke skrevet nogen ugenoter" // Empty placeholder
				}
            }
        };

        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(true);
        _mockAgentService.Setup(s => s.GetAllChildrenAsync()).ReturnsAsync(children);
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string)null!);
        _mockAgentService.Setup(s => s.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>(), true))
            .ReturnsAsync(emptyWeekLetterJson);

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithNullWeekLetter_SkipsStoring()
    {
        // Arrange
        var children = new List<Child>
        {
            new Child { FirstName = "Emma", LastName = "Test" }
        };

        _mockAgentService.Setup(s => s.LoginAsync()).ReturnsAsync(true);
        _mockAgentService.Setup(s => s.GetAllChildrenAsync()).ReturnsAsync(children);
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((string)null!);
        _mockAgentService.Setup(s => s.GetWeekLetterAsync(It.IsAny<Child>(), It.IsAny<DateOnly>(), It.IsAny<bool>(), true))
            .ReturnsAsync((JObject?)null);

        // Act
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Assert
        _mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
    }

    [Fact]
    public async Task SeedHistoricalWeekLettersAsync_WithException_HandlesGracefully()
    {
        // Arrange
        _mockAgentService.Setup(s => s.LoginAsync()).ThrowsAsync(new Exception("Login error"));

        // Act & Assert - Should not throw
        await _seeder.SeedHistoricalWeekLettersAsync();

        // Verify the exception was handled
        _mockAgentService.Verify(s => s.LoginAsync(), Times.Once());
    }

    [Fact]
    public void HistoricalDataSeeder_ImplementsIHistoricalDataSeederInterface()
    {
        // Arrange & Act & Assert
        Assert.True(typeof(IHistoricalDataSeeder).IsAssignableFrom(typeof(HistoricalDataSeeder)));
    }

    [Fact]
    public void HistoricalDataSeeder_HasCorrectNamespace()
    {
        // Arrange
        var seederType = typeof(HistoricalDataSeeder);

        // Act & Assert
        Assert.Equal("Aula.Services", seederType.Namespace);
    }

    [Fact]
    public void HistoricalDataSeeder_IsPublicClass()
    {
        // Arrange
        var seederType = typeof(HistoricalDataSeeder);

        // Act & Assert
        Assert.True(seederType.IsPublic);
        Assert.False(seederType.IsAbstract);
        Assert.False(seederType.IsSealed);
    }

    [Fact]
    public void HistoricalDataSeeder_HasCorrectPublicMethods()
    {
        // Arrange
        var seederType = typeof(HistoricalDataSeeder);

        // Act & Assert
        Assert.NotNull(seederType.GetMethod("SeedHistoricalWeekLettersAsync"));
    }

    [Fact]
    public void HistoricalDataSeeder_ConstructorParametersHaveCorrectTypes()
    {
        // Arrange
        var seederType = typeof(HistoricalDataSeeder);
        var constructor = seederType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Equal(4, parameters.Length);
        Assert.Equal(typeof(ILoggerFactory), parameters[0].ParameterType);
        Assert.Equal(typeof(IAgentService), parameters[1].ParameterType);
        Assert.Equal(typeof(ISupabaseService), parameters[2].ParameterType);
        Assert.Equal(typeof(Config), parameters[3].ParameterType);
    }
}
