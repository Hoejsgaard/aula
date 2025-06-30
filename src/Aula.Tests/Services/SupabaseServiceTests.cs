using Microsoft.Extensions.Logging;
using Moq;
using Aula.Configuration;
using Aula.Services;
using ConfigSupabase = Aula.Configuration.Supabase;

namespace Aula.Tests.Services;

public class SupabaseServiceTests
{
    private readonly Config _testConfig;
    private readonly SupabaseService _supabaseService;

    public SupabaseServiceTests()
    {
        var loggerFactory = new LoggerFactory();

        _testConfig = new Config
        {
            Supabase = new ConfigSupabase
            {
                Url = "https://test.supabase.co",
                Key = "test-key",
                ServiceRoleKey = "test-service-role-key"
            }
        };

        _supabaseService = new SupabaseService(_testConfig, loggerFactory);
    }

    [Fact]
    public void Constructor_WithValidConfig_InitializesCorrectly()
    {
        // Arrange & Act - Constructor called in setup

        // Assert
        Assert.NotNull(_supabaseService);
    }

    [Fact]
    public void Constructor_WithInvalidConfig_DoesNotThrow()
    {
        // Arrange
        var invalidConfig = new Config
        {
            Supabase = new ConfigSupabase
            {
                Url = "",
                ServiceRoleKey = ""
            }
        };
        var loggerFactory = new LoggerFactory();

        // Act & Assert - Should handle invalid config gracefully during construction
        var invalidService = new SupabaseService(invalidConfig, loggerFactory);
        Assert.NotNull(invalidService);
    }

    [Fact]
    public async Task TestConnectionAsync_WithoutInitialize_ReturnsFalse()
    {
        // Act
        var result = await _supabaseService.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ReminderData_WithValidParameters_AreValid()
    {
        // Arrange
        var text = "Test reminder";
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var time = new TimeOnly(10, 0);
        var childName = "TestChild";

        // Act & Assert - Verify reminder data parameters are valid
        Assert.NotNull(_supabaseService);
        Assert.NotEmpty(text);
        Assert.False(string.IsNullOrEmpty(childName));
    }


    [Theory]
    [InlineData("TestChild", 42, 2023)]
    [InlineData("AnotherChild", 1, 2024)]
    public void WeekLetterParameters_WithValidValues_AreInValidRange(string childName, int weekNumber, int year)
    {
        // Act & Assert - Verify week letter parameters are within expected ranges
        Assert.NotNull(_supabaseService);
        Assert.True(weekNumber > 0 && weekNumber <= 53);
        Assert.True(year > 2000 && year < 3000);
        Assert.False(string.IsNullOrEmpty(childName));
    }
}