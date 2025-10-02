using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Aula.Configuration;
using Aula.AI.Services;
using Aula.Content.WeekLetters;
using Aula.Core.Models;
using Aula.Core.Security;
using Aula.Core.Utilities;

namespace Aula.Tests.Core.Utilities;

public class DataServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly DataService _dataManager;
    private readonly Child _testChild;

    public DataServiceTests()
    {
        // Create a real memory cache for testing
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        var configMock = new Mock<Config>();
        _dataManager = new DataService(_cache, configMock.Object, _loggerFactoryMock.Object);

        _testChild = new Child
        {
            FirstName = "TestChild",
            LastName = "TestLastName",
            Colour = "Blue"
        };
    }

    [Fact]
    public void CacheWeekLetter_StoresDataInCache()
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

        // Act
        _dataManager.CacheWeekLetter(_testChild, 42, 2025, weekLetter);
        var result = _dataManager.GetWeekLetter(_testChild, 42, 2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Class", result["ugebreve"]?[0]?["klasseNavn"]?.ToString());
    }

    [Fact]
    public void GetWeekLetter_ReturnsNull_WhenNotInCache()
    {
        // Act
        var result = _dataManager.GetWeekLetter(_testChild, 10, 2025);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void CacheWeekSchedule_StoresDataInCache()
    {
        // Arrange
        var weekSchedule = new JObject
        {
            ["skema"] = new JArray
            {
                new JObject
                {
                    ["dag"] = "Mandag",
                    ["lektioner"] = new JArray
                    {
                        new JObject
                        {
                            ["fag"] = "Matematik",
                            ["tid"] = "08:00-09:00"
                        }
                    }
                }
            }
        };

        // Act
        _dataManager.CacheWeekSchedule(_testChild, 42, 2025, weekSchedule);
        var result = _dataManager.GetWeekSchedule(_testChild, 42, 2025);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mandag", result["skema"]?[0]?["dag"]?.ToString());
    }

    [Fact]
    public void GetWeekSchedule_ReturnsNull_WhenNotInCache()
    {
        // Act
        var result = _dataManager.GetWeekSchedule(_testChild, 10, 2025);

        // Assert
        Assert.Null(result);
    }
}
