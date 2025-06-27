using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;

namespace Aula.Tests;

public class DataManagerTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly DataManager _dataManager;
    private readonly Child _testChild;

    public DataManagerTests()
    {
        // Create a real memory cache for testing
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        var configMock = new Mock<Config>();
        _dataManager = new DataManager(_cache, configMock.Object, _loggerFactoryMock.Object);

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
        _dataManager.CacheWeekLetter(_testChild, weekLetter);
        var result = _dataManager.GetWeekLetter(_testChild);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Class", result["ugebreve"]?[0]?["klasseNavn"]?.ToString());
    }

    [Fact]
    public void GetWeekLetter_ReturnsNull_WhenNotInCache()
    {
        // Act
        var result = _dataManager.GetWeekLetter(_testChild);

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
        _dataManager.CacheWeekSchedule(_testChild, weekSchedule);
        var result = _dataManager.GetWeekSchedule(_testChild);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mandag", result["skema"]?[0]?["dag"]?.ToString());
    }

    [Fact]
    public void GetWeekSchedule_ReturnsNull_WhenNotInCache()
    {
        // Act
        var result = _dataManager.GetWeekSchedule(_testChild);

        // Assert
        Assert.Null(result);
    }
}