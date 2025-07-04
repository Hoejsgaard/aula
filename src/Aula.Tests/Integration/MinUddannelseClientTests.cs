using Microsoft.Extensions.Logging;
using Moq;
using Aula.Configuration;
using Aula.Integration;
using Aula.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Integration;

public class MinUddannelseClientTests
{
    private readonly Mock<ISupabaseService> _mockSupabaseService;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger<MinUddannelseClient>> _mockLogger;
    private readonly Config _testConfig;
    private readonly Child _testChild;

    public MinUddannelseClientTests()
    {
        _mockSupabaseService = new Mock<ISupabaseService>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger<MinUddannelseClient>>();
        
        _mockLoggerFactory.Setup(x => x.CreateLogger<MinUddannelseClient>()).Returns(_mockLogger.Object);
        
        _testConfig = new Config
        {
            UniLogin = new UniLogin { Username = "testuser", Password = "testpass" },
            Features = new Features 
            { 
                UseMockData = false,
                MockCurrentWeek = 25,
                MockCurrentYear = 2024
            }
        };
        
        _testChild = new Child { FirstName = "Emma", LastName = "Test" };
    }

    [Fact]
    public void Constructor_WithConfig_InitializesCorrectly()
    {
        // Act
        var client = new MinUddannelseClient(_testConfig);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithConfigAndServices_InitializesCorrectly()
    {
        // Act
        var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithUsernamePassword_InitializesCorrectly()
    {
        // Act
        var client = new MinUddannelseClient("testuser", "testpass");

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetWeekLetter_MockModeEnabled_WithStoredData_ReturnsStoredWeekLetter()
    {
        // Arrange
        _testConfig.Features.UseMockData = true;
        var storedContent = "{\"test\":\"stored data\"}";
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
            .ReturnsAsync(storedContent);

        var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
        var testDate = new DateOnly(2024, 6, 17); // Week 25

        // Act
        var result = await client.GetWeekLetter(_testChild, testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("stored data", result["test"]?.ToString());
        _mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024), Times.Once);
    }

    [Fact]
    public async Task GetWeekLetter_MockModeEnabled_NoStoredData_ReturnsEmptyWeekLetter()
    {
        // Arrange
        _testConfig.Features.UseMockData = true;
        _mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync(_testChild.FirstName, 25, 2024))
            .ReturnsAsync((string?)null);

        var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
        var testDate = new DateOnly(2024, 6, 17); // Week 25

        // Act
        var result = await client.GetWeekLetter(_testChild, testDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result["ugebreve"]);
        var ugebreve = result["ugebreve"] as JArray;
        Assert.NotNull(ugebreve);
        Assert.Single(ugebreve);
        
        var firstItem = ugebreve[0] as JObject;
        Assert.NotNull(firstItem);
        Assert.Equal("Mock Class", firstItem["klasseNavn"]?.ToString());
        Assert.Equal("25", firstItem["uge"]?.ToString());
        Assert.Contains("mock mode", firstItem["indhold"]?.ToString());
    }

    [Fact]
    public async Task GetWeekLetter_MockModeDisabled_DoesNotCallSupabase()
    {
        // Arrange
        _testConfig.Features.UseMockData = false;
        var client = new MinUddannelseClient(_testConfig, _mockSupabaseService.Object, _mockLoggerFactory.Object);
        var testDate = new DateOnly(2024, 6, 17);

        // Act & Assert - This will fail because we're not mocking HTTP, but we can verify Supabase isn't called
        var exception = await Record.ExceptionAsync(() => client.GetWeekLetter(_testChild, testDate));
        
        // The test will throw due to HTTP call, but we can verify mock mode logic wasn't triggered
        _mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GetIsoWeekNumber_ValidDate_ReturnsCorrectWeekNumber()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        var date = new DateOnly(2024, 6, 17); // This should be week 25 in 2024

        // Act - Using reflection to access private method
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (int)method!.Invoke(client, new object[] { date })!;

        // Assert
        Assert.Equal(25, result);
    }

    [Fact]
    public void GetIsoWeekNumber_January1st2024_ReturnsWeek1()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        var date = new DateOnly(2024, 1, 1);

        // Act
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (int)method!.Invoke(client, new object[] { date })!;

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetIsoWeekNumber_December31st2024_ReturnsWeek1Of2025()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        var date = new DateOnly(2024, 12, 31);

        // Act
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (int)method!.Invoke(client, new object[] { date })!;

        // Assert
        // December 31, 2024 is actually week 1 of 2025 in ISO week dating
        Assert.Equal(1, result);
    }

    [Fact]
    public void CreateEmptyWeekLetter_ValidWeekNumber_ReturnsCorrectStructure()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        var weekNumber = 25;

        // Act
        var method = typeof(MinUddannelseClient).GetMethod("CreateEmptyWeekLetter", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (JObject)method!.Invoke(client, new object[] { weekNumber })!;

        // Assert
        Assert.NotNull(result);
        Assert.Null(result["errorMessage"]);
        
        var ugebreve = result["ugebreve"] as JArray;
        Assert.NotNull(ugebreve);
        Assert.Single(ugebreve);
        
        var firstItem = ugebreve[0] as JObject;
        Assert.NotNull(firstItem);
        Assert.Equal("Mock Class", firstItem["klasseNavn"]?.ToString());
        Assert.Equal("25", firstItem["uge"]?.ToString());
        Assert.Contains("mock mode", firstItem["indhold"]?.ToString());
    }

    [Fact]
    public void CreateEmptyWeekLetter_DifferentWeekNumbers_ReturnsCorrectWeekNumber()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");

        // Act & Assert for different week numbers
        foreach (var weekNumber in new[] { 1, 15, 30, 52 })
        {
            var method = typeof(MinUddannelseClient).GetMethod("CreateEmptyWeekLetter", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (JObject)method!.Invoke(client, new object[] { weekNumber })!;
            
            var ugebreve = result["ugebreve"] as JArray;
            var firstItem = ugebreve![0] as JObject;
            Assert.Equal(weekNumber.ToString(), firstItem!["uge"]?.ToString());
        }
    }

    [Fact]
    public void GetChildId_UserProfileNotLoaded_ThrowsInvalidOperationException()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");

        // Act & Assert
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
            method!.Invoke(client, new object[] { _testChild }));
        
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("User profile not loaded", exception.InnerException.Message);
    }

    [Fact]
    public void GetChildId_WithLoadedProfile_ChildNotFound_ThrowsArgumentException()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        
        // Set up user profile with different child name
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userProfile = new JObject
        {
            ["boern"] = new JArray(new JObject
            {
                ["fornavn"] = "DifferentName",
                ["id"] = "123"
            })
        };
        userProfileField!.SetValue(client, userProfile);

        // Act & Assert
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
            method!.Invoke(client, new object[] { _testChild }));
        
        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Child with first name 'Emma' not found", exception.InnerException.Message);
    }

    [Fact]
    public void GetChildId_WithLoadedProfile_ChildFound_ReturnsCorrectId()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        
        // Set up user profile with matching child
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userProfile = new JObject
        {
            ["boern"] = new JArray(
                new JObject
                {
                    ["fornavn"] = "Emma",
                    ["id"] = "child123"
                },
                new JObject
                {
                    ["fornavn"] = TestChild1,
                    ["id"] = "child456"
                }
            )
        };
        userProfileField!.SetValue(client, userProfile);

        // Act
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (string)method!.Invoke(client, new object[] { _testChild })!;

        // Assert
        Assert.Equal("child123", result);
    }

    [Fact]
    public void GetChildId_NoChildrenInProfile_ThrowsInvalidOperationException()
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        
        // Set up user profile without children
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var userProfile = new JObject(); // No "boern" property
        userProfileField!.SetValue(client, userProfile);

        // Act & Assert
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
            method!.Invoke(client, new object[] { _testChild }));
        
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("No children found in user profile", exception.InnerException.Message);
    }

    [Theory]
    [InlineData(2024, 1, 1, 1)]      // January 1st, 2024 - Week 1
    [InlineData(2024, 6, 17, 25)]    // June 17th, 2024 - Week 25  
    [InlineData(2024, 12, 30, 1)]    // December 30th, 2024 - Week 1 of 2025
    [InlineData(2023, 1, 2, 1)]      // January 2nd, 2023 - Week 1
    [InlineData(2023, 12, 31, 52)]   // December 31st, 2023 - Week 52
    public void GetIsoWeekNumber_VariousDates_ReturnsCorrectWeekNumbers(int year, int month, int day, int expectedWeek)
    {
        // Arrange
        var client = new MinUddannelseClient("test", "test");
        var date = new DateOnly(year, month, day);

        // Act
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (int)method!.Invoke(client, new object[] { date })!;

        // Assert
        Assert.Equal(expectedWeek, result);
    }

    [Fact]
    public async Task GetStoredWeekLetter_NoSupabaseService_ReturnsNull()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig); // No Supabase service provided

        // Act
        var result = await client.GetStoredWeekLetter(_testChild, 25, 2024);

        // Assert
        Assert.Null(result);
    }
}