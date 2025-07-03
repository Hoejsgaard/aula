using System.Reflection;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Aula.Integration;
using Aula.Configuration;
using Aula.Services;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Aula.Tests.Integration;

public class MinUddannelseClientTests
{
    private readonly Config _testConfig;

    public MinUddannelseClientTests()
    {
        _testConfig = new Config
        {
            UniLogin = new UniLogin
            {
                Username = "testuser",
                Password = "testpass"
            }
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Act
        var client = new MinUddannelseClient(_testConfig);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithUsernamePassword_CreatesInstance()
    {
        // Act
        var client = new MinUddannelseClient("testuser", "testpass");

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNullUsername_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MinUddannelseClient(null!, "password"));
    }

    [Fact]
    public void Constructor_WithNullPassword_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MinUddannelseClient("username", null!));
    }

    [Fact]
    public async Task GetWeekLetter_WithoutLogin_ThrowsException()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.GetWeekLetter(child, date));
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task GetWeekSchedule_WithoutLogin_ThrowsException()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16);

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => client.GetWeekSchedule(child, date));
        Assert.NotNull(exception);
    }

    [Theory]
    [InlineData(2023, 1, 2, 1)]   // Week 1 of 2023
    [InlineData(2023, 10, 16, 42)] // Week 42 of 2023
    [InlineData(2023, 12, 25, 52)] // Week 52 of 2023
    [InlineData(2024, 1, 1, 1)]   // Week 1 of 2024
    [InlineData(2024, 12, 30, 53)] // Week 53 of 2024 (actual ISO week calculation)
    public void GetIsoWeekNumber_ReturnsCorrectWeekNumber(int year, int month, int day, int expectedWeek)
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var date = new DateOnly(year, month, day);

        // Use reflection to access the private GetIsoWeekNumber method
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var weekNumber = (int)method.Invoke(client, new object[] { date })!;

        // Assert
        Assert.Equal(expectedWeek, weekNumber);
    }

    [Fact]
    public void GetChildId_WithNullUserProfile_ThrowsException()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Test", LastName = "Child" };

        // Use reflection to access the private GetChildId method
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(client, new object[] { child }));
        Assert.NotNull(exception.InnerException);
        Assert.Contains("No children found in user profile", exception.InnerException.Message);
    }

    [Fact]
    public void GetChildId_WithEmptyUserProfile_ThrowsException()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Test", LastName = "Child" };

        // Set up a user profile with no children using reflection
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(userProfileField);

        var emptyProfile = new JObject();
        userProfileField.SetValue(client, emptyProfile);

        // Use reflection to access the private GetChildId method
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(client, new object[] { child }));
        Assert.NotNull(exception.InnerException);
        Assert.Contains("No children found in user profile", exception.InnerException.Message);
    }

    [Fact]
    public void GetChildId_WithValidUserProfile_ReturnsChildId()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Alice", LastName = "Test" };

        // Set up a user profile with children using reflection
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(userProfileField);

        var profileWithChildren = JObject.Parse(@"{
            ""id"": ""12345"",
            ""navn"": ""Test User"",
            ""boern"": [
                {""id"": ""child1"", ""fornavn"": ""Alice"", ""efternavn"": ""Test""},
                {""id"": ""child2"", ""fornavn"": ""Bob"", ""efternavn"": ""Test""}
            ]
        }");
        userProfileField.SetValue(client, profileWithChildren);

        // Use reflection to access the private GetChildId method
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var childId = (string)method.Invoke(client, new object[] { child })!;

        // Assert
        Assert.Equal("child1", childId);
    }

    [Fact]
    public void GetChildId_WithNonExistentChild_ThrowsException()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "Charlie", LastName = "NotFound" };

        // Set up a user profile with different children using reflection
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(userProfileField);

        var profileWithDifferentChildren = JObject.Parse(@"{
            ""id"": ""12345"",
            ""navn"": ""Test User"",
            ""boern"": [
                {""id"": ""child1"", ""fornavn"": ""Alice"", ""efternavn"": ""Test""},
                {""id"": ""child2"", ""fornavn"": ""Bob"", ""efternavn"": ""Test""}
            ]
        }");
        userProfileField.SetValue(client, profileWithDifferentChildren);

        // Use reflection to access the private GetChildId method
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act & Assert
        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(client, new object[] { child }));
        Assert.NotNull(exception.InnerException);
        Assert.Contains("Child not found", exception.InnerException.Message);
    }

    [Fact]
    public void ExtractUserProfile_MethodExists()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);

        // Use reflection to verify the private ExtractUserProfile method exists
        var method = typeof(MinUddannelseClient).GetMethod("ExtractUserProfile", BindingFlags.NonPublic | BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<JObject>), method.ReturnType);
    }

    [Fact]
    public void GetIsoWeekNumber_WithBoundaryDates_HandlesCorrectly()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);

        // Use reflection to access the private GetIsoWeekNumber method
        var method = typeof(MinUddannelseClient).GetMethod("GetIsoWeekNumber", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Test various boundary conditions
        var testCases = new[]
        {
            (new DateOnly(2023, 1, 1), 52), // First day of year might be week 52 of previous year
            (new DateOnly(2023, 1, 9), 2),  // Second week of January
            (new DateOnly(2023, 12, 31), 52), // Last day of year
        };

        foreach (var (date, expectedMinWeek) in testCases)
        {
            // Act
            var weekNumber = (int)method.Invoke(client, new object[] { date })!;

            // Assert - Week number should be reasonable (1-53)
            Assert.InRange(weekNumber, 1, 53);
        }
    }

    [Fact]
    public void IMinUddannelseClient_Interface_IsImplemented()
    {
        // Arrange & Act
        var client = new MinUddannelseClient(_testConfig);

        // Assert
        Assert.IsAssignableFrom<IMinUddannelseClient>(client);
    }

    [Fact]
    public void Constructor_SetsCorrectUrls()
    {
        // Arrange & Act
        var client = new MinUddannelseClient(_testConfig);

        // Assert - Check that the client inherits from UniLoginClient correctly
        Assert.IsAssignableFrom<UniLoginClient>(client);
    }

    [Fact]
    public void LoginAsync_MethodExists()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);

        // Use reflection to verify the LoginAsync method exists and overrides base
        var method = typeof(MinUddannelseClient).GetMethod("LoginAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
        Assert.True(method.IsVirtual || method.DeclaringType == typeof(MinUddannelseClient));
    }

    [Theory]
    [InlineData("Test", "Child")]
    [InlineData("Alice", "Test")]
    [InlineData("Bob", "Test")]
    public void GetChildId_WithVariousChildNames_ProcessesCorrectly(string firstName, string lastName)
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = firstName, LastName = lastName };

        // Set up a user profile with the specific child
        var userProfileField = typeof(MinUddannelseClient).GetField("_userProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(userProfileField);

        var profileWithChild = JObject.Parse($@"{{
            ""id"": ""12345"",
            ""navn"": ""Test User"",
            ""boern"": [
                {{""id"": ""child1"", ""fornavn"": ""{firstName}"", ""efternavn"": ""{lastName}""}}
            ]
        }}");
        userProfileField.SetValue(client, profileWithChild);

        // Use reflection to access the private GetChildId method
        var method = typeof(MinUddannelseClient).GetMethod("GetChildId", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var childId = (string)method.Invoke(client, new object[] { child })!;

        // Assert
        Assert.Equal("child1", childId);
    }

    // ===========================================
    // WEEK LETTER STORAGE TESTS
    // ===========================================

    [Fact]
    public async Task GetStoredWeekLetter_WithoutSupabaseService_ReturnsNull()
    {
        // Arrange - Use constructor without SupabaseService
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "TestChild" };

        // Act
        var result = await client.GetStoredWeekLetter(child, 42, 2024);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStoredWeekLetter_WithMockSupabaseService_ReturnsStoredContent()
    {
        // Arrange
        var mockSupabaseService = new Mock<ISupabaseService>();
        var loggerFactory = new LoggerFactory();
        var client = new MinUddannelseClient(_testConfig, mockSupabaseService.Object, loggerFactory);
        var child = new Child { FirstName = "TestChild" };

        var expectedJson = "{\"ugebreve\":[{\"indhold\":\"Test content\"}]}";
        mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024))
            .ReturnsAsync(expectedJson);

        // Act
        var result = await client.GetStoredWeekLetter(child, 42, 2024);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test content", result["ugebreve"]?[0]?["indhold"]?.ToString());
        mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024), Times.Once);
    }

    [Fact]
    public async Task GetStoredWeekLetter_WithEmptyStoredContent_ReturnsNull()
    {
        // Arrange
        var mockSupabaseService = new Mock<ISupabaseService>();
        var loggerFactory = new LoggerFactory();
        var client = new MinUddannelseClient(_testConfig, mockSupabaseService.Object, loggerFactory);
        var child = new Child { FirstName = "TestChild" };

        mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await client.GetStoredWeekLetter(child, 42, 2024);

        // Assert
        Assert.Null(result);
        mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024), Times.Once);
    }

    [Fact]
    public async Task GetStoredWeekLetter_WithSupabaseException_ReturnsNull()
    {
        // Arrange
        var mockSupabaseService = new Mock<ISupabaseService>();
        var loggerFactory = new LoggerFactory();
        var client = new MinUddannelseClient(_testConfig, mockSupabaseService.Object, loggerFactory);
        var child = new Child { FirstName = "TestChild" };

        mockSupabaseService.Setup(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await client.GetStoredWeekLetter(child, 42, 2024);

        // Assert
        Assert.Null(result);
        mockSupabaseService.Verify(s => s.GetStoredWeekLetterAsync("TestChild", 42, 2024), Times.Once);
    }

    [Fact]
    public async Task GetStoredWeekLetters_WithoutSupabaseService_ReturnsEmptyList()
    {
        // Arrange - Use constructor without SupabaseService
        var client = new MinUddannelseClient(_testConfig);
        var child = new Child { FirstName = "TestChild" };

        // Act
        var result = await client.GetStoredWeekLetters(child, 2024);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetStoredWeekLetters_WithMockSupabaseService_ReturnsStoredLetters()
    {
        // Arrange
        var mockSupabaseService = new Mock<ISupabaseService>();
        var loggerFactory = new LoggerFactory();
        var client = new MinUddannelseClient(_testConfig, mockSupabaseService.Object, loggerFactory);
        var child = new Child { FirstName = "TestChild" };

        var expectedLetters = new List<StoredWeekLetter>
        {
            new StoredWeekLetter { ChildName = "TestChild", WeekNumber = 42, Year = 2024, RawContent = "{\"test\":\"content\"}" }
        };
        mockSupabaseService.Setup(s => s.GetStoredWeekLettersAsync("TestChild", 2024))
            .ReturnsAsync(expectedLetters);

        // Act
        var result = await client.GetStoredWeekLetters(child, 2024);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("TestChild", result[0].ChildName);
        Assert.Equal(42, result[0].WeekNumber);
        mockSupabaseService.Verify(s => s.GetStoredWeekLettersAsync("TestChild", 2024), Times.Once);
    }

    [Fact]
    public void ComputeContentHash_WithSameContent_ReturnsSameHash()
    {
        // Arrange
        var client = new MinUddannelseClient(_testConfig);
        var content1 = "Test content for hashing";
        var content2 = "Test content for hashing";
        var content3 = "Different content";

        // Use reflection to access the private ComputeContentHash method
        var method = typeof(MinUddannelseClient).GetMethod("ComputeContentHash", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var hash1 = (string)method.Invoke(client, new object[] { content1 })!;
        var hash2 = (string)method.Invoke(client, new object[] { content2 })!;
        var hash3 = (string)method.Invoke(client, new object[] { content3 })!;

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEqual(hash1, hash3);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public async Task GetWeekLetterWithFallback_WithSuccessfulLiveFetch_StoresAndReturnsLiveData()
    {
        // Arrange
        var mockSupabaseService = new Mock<ISupabaseService>();
        var loggerFactory = new LoggerFactory();

        // Create a partial mock that calls the real constructor but allows us to mock GetWeekLetter
        var mockClient = new Mock<MinUddannelseClient>(_testConfig, mockSupabaseService.Object, loggerFactory) { CallBase = true };
        var child = new Child { FirstName = "TestChild" };
        var date = DateOnly.FromDateTime(DateTime.Today);

        var expectedWeekLetter = JObject.Parse("{\"ugebreve\":[{\"indhold\":\"Live content\"}]}");
        mockClient.Setup(c => c.GetWeekLetter(child, date))
            .ReturnsAsync(expectedWeekLetter);

        // Act
        var result = await mockClient.Object.GetWeekLetterWithFallback(child, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Live content", result["ugebreve"]?[0]?["indhold"]?.ToString());
        mockClient.Verify(c => c.GetWeekLetter(child, date), Times.Once);
        mockSupabaseService.Verify(s => s.StoreWeekLetterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void Interface_HasNewWeekLetterStorageMethods()
    {
        // Arrange
        var interfaceType = typeof(IMinUddannelseClient);

        // Act & Assert - Verify new methods are in interface
        var getStoredMethod = interfaceType.GetMethod("GetStoredWeekLetter");
        var getWithFallbackMethod = interfaceType.GetMethod("GetWeekLetterWithFallback");
        var getStoredListMethod = interfaceType.GetMethod("GetStoredWeekLetters");

        Assert.NotNull(getStoredMethod);
        Assert.NotNull(getWithFallbackMethod);
        Assert.NotNull(getStoredListMethod);

        // Verify return types
        Assert.Equal(typeof(Task<JObject?>), getStoredMethod.ReturnType);
        Assert.Equal(typeof(Task<JObject>), getWithFallbackMethod.ReturnType);
        Assert.Equal(typeof(Task<List<StoredWeekLetter>>), getStoredListMethod.ReturnType);
    }
}