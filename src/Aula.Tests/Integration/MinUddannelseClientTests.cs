using Moq;
using Moq.Protected;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Integration;

public class MinUddannelseClientTests
{
    private Mock<HttpMessageHandler> CreateMockHandlerWithLoginResponse()
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Default successful login response
        var loginResponseHtml = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\", \"boern\": [{\"id\": \"child1\", \"fornavn\": \"Test\", \"efternavn\": \"Child\"}]};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(loginResponseHtml)
            });

        return mockHandler;
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Arrange
        var config = new Config
        {
            UniLogin = new UniLogin
            {
                Username = "testuser",
                Password = "testpass"
            }
        };

        // Act
        var client = new MinUddannelseClient(config);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetWeekLetter_ReturnsWeekLetter_WhenApiReturnsData()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        // Setup mock response for GetWeekLetter API call
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getUgeBreve")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"ugebreve\": [{\"klasseNavn\": \"Test Class\", \"uge\": \"42\", \"indhold\": \"Test content\"}]}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16); // Week 42 of 2023

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekLetter(child, date);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result["ugebreve"]);
        var weekLetters = result["ugebreve"] as JArray;
        Assert.NotNull(weekLetters);
        Assert.Equal("Test Class", weekLetters[0]["klasseNavn"]?.ToString());
        Assert.Equal("42", weekLetters[0]["uge"]?.ToString());
        Assert.Equal("Test content", weekLetters[0]["indhold"]?.ToString());
    }

    [Fact]
    public async Task GetWeekLetter_ReturnsDefaultMessage_WhenNoWeekLetterExists()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        // Setup mock response with empty ugebreve array
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getUgeBreve")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"ugebreve\": []}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16); // Week 42 of 2023

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekLetter(child, date);

        // Assert
        Assert.NotNull(result);
        var weekLetters = result["ugebreve"] as JArray;
        Assert.NotNull(weekLetters);
        Assert.Single(weekLetters);
        Assert.Equal("N/A", weekLetters[0]["klasseNavn"]?.ToString());
        Assert.Equal("42", weekLetters[0]["uge"]?.ToString());
        Assert.Equal("Der er ikke skrevet nogen ugenoter til denne uge", weekLetters[0]["indhold"]?.ToString());
    }

    [Fact]
    public async Task GetWeekLetter_ThrowsHttpRequestException_WhenApiReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getUgeBreve")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16);

        // Login first
        await client.LoginAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetWeekLetter(child, date));
    }

    [Fact]
    public async Task GetWeekSchedule_ReturnsSchedule_WhenApiReturnsData()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getElevSkema")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"skema\": [{\"tid\": \"08:00\", \"fag\": \"Matematik\"}]}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16);

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekSchedule(child, date);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result["skema"]);
    }

    [Fact]
    public void Constructor_WithConfig_InitializesCorrectly()
    {
        // Arrange
        var config = new Config
        {
            UniLogin = new UniLogin
            {
                Username = "configuser",
                Password = "configpass"
            }
        };

        // Act
        var client = new MinUddannelseClient(config);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public async Task GetWeekSchedule_ThrowsHttpRequestException_WhenApiReturnsError()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getElevSkema")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16);

        // Login first
        await client.LoginAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetWeekSchedule(child, date));
    }

    [Fact]
    public async Task GetWeekSchedule_WithDifferentWeekNumbers_ReturnsCorrectData()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock login response with proper user profile
        var loginResponseHtml = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\", \"boern\": [{\"id\": \"child1\", \"fornavn\": \"Test\", \"efternavn\": \"Child\"}]};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(loginResponseHtml)
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getElevSkema")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"skema\": [{\"tid\": \"09:00\", \"fag\": \"Dansk\"}]}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 1, 2); // Week 1 of 2023

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekSchedule(child, date);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result["skema"]);
        var scheduleArray = result["skema"] as JArray;
        Assert.NotNull(scheduleArray);
        Assert.Equal("09:00", scheduleArray[0]["tid"]?.ToString());
        Assert.Equal("Dansk", scheduleArray[0]["fag"]?.ToString());
    }

    [Fact]
    public async Task LoginAsync_SetsUserProfile_WhenSuccessful()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock successful login sequence
        var loginResponseHtml = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\", \"boern\": [{\"id\": \"child1\", \"fornavn\": \"Alice\", \"efternavn\": \"Test\"}, {\"id\": \"child2\", \"fornavn\": \"Bob\", \"efternavn\": \"Test\"}]};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(loginResponseHtml)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Act
        var result = await client.LoginAsync();

        // Assert
        Assert.True(result);
        
        // Verify user profile was extracted by testing GetChildId functionality
        var child = new Child { FirstName = "Alice", LastName = "Test" };
        var childId = client.TestGetChildId(child);
        Assert.Equal("child1", childId);
    }

    [Fact]
    public async Task ExtractUserProfile_ThrowsException_WhenNoScriptTag()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithoutScript = "<html><body><div>No script tag here</div></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithoutScript)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => client.LoginAsync());
        Assert.Contains("No UserProfile found", exception.Message);
    }

    [Fact]
    public async Task ExtractUserProfile_ThrowsException_WhenScriptIsEmpty()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithEmptyScript = "<html><body><script>var test = 'no tempcontext';</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithEmptyScript)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => client.LoginAsync());
        Assert.Contains("No UserProfile found", exception.Message);
    }

    [Fact]
    public async Task ExtractUserProfile_ThrowsException_WhenContextNotFound()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithWrongScript = "<html><body><script>window.__tempcontext__['someOtherVariable'] = 'test';</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithWrongScript)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => client.LoginAsync());
        Assert.Contains("UserProfile context not found in script", exception.Message);
    }

    [Fact]
    public async Task ExtractUserProfile_ThrowsException_WhenJsonIsInvalid()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithInvalidJson = "<html><body><script>window.__tempcontext__['currentUser'] = {invalid json here;</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithInvalidJson)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => client.LoginAsync());
        Assert.Contains("Failed to parse UserProfile JSON", exception.Message);
    }

    [Fact]
    public void GetChildId_ThrowsException_WhenUserProfileNotLoaded()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");
        var child = new Child { FirstName = "Test", LastName = "Child" };

        // Act & Assert (don't login first - profile should be null/empty)
        var exception = Assert.Throws<Exception>(() => client.TestGetChildId(child));
        Assert.Contains("User profile not loaded", exception.Message);
    }

    [Fact]
    public async Task GetChildId_ThrowsException_WhenNoChildrenInProfile()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithoutChildren = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\"};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithoutChildren)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Login first to load profile
        await client.LoginAsync();

        var child = new Child { FirstName = "Test", LastName = "Child" };

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => client.TestGetChildId(child));
        Assert.Contains("No children found in user profile", exception.Message);
    }

    [Fact]
    public async Task GetChildId_ThrowsException_WhenChildNotFound()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithDifferentChildren = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\", \"boern\": [{\"id\": \"child1\", \"fornavn\": \"Alice\", \"efternavn\": \"Test\"}, {\"id\": \"child2\", \"fornavn\": \"Bob\", \"efternavn\": \"Test\"}]};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithDifferentChildren)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Login first to load profile
        await client.LoginAsync();

        var child = new Child { FirstName = "Charlie", LastName = "NotFound" };

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => client.TestGetChildId(child));
        Assert.Contains("Child not found", exception.Message);
    }

    [Fact]
    public async Task GetChildId_ReturnsCorrectId_WhenChildExists()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();

        var htmlWithChildren = "<html><body><script>window.__tempcontext__['currentUser'] = {\"id\": \"12345\", \"navn\": \"Test User\", \"boern\": [{\"id\": \"child1\", \"fornavn\": \"Alice\", \"efternavn\": \"Test\"}, {\"id\": \"child2\", \"fornavn\": \"Bob\", \"efternavn\": \"Test\"}]};</script></body></html>";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/Node/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(htmlWithChildren)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        // Login first to load profile
        await client.LoginAsync();

        var child1 = new Child { FirstName = "Alice", LastName = "Test" };
        var child2 = new Child { FirstName = "Bob", LastName = "Test" };

        // Act
        var id1 = client.TestGetChildId(child1);
        var id2 = client.TestGetChildId(child2);

        // Assert
        Assert.Equal("child1", id1);
        Assert.Equal("child2", id2);
    }

    [Theory]
    [InlineData(2023, 1, 2, 1)]   // Week 1 of 2023
    [InlineData(2023, 10, 16, 42)] // Week 42 of 2023
    [InlineData(2023, 12, 25, 52)] // Week 52 of 2023
    [InlineData(2024, 1, 1, 1)]   // Week 1 of 2024
    public void GetIsoWeekNumber_ReturnsCorrectWeekNumber(int year, int month, int day, int expectedWeek)
    {
        // Arrange
        var client = new TestableMinUddannelseClient(new HttpClient(), "username", "password");
        var date = new DateOnly(year, month, day);

        // Act
        var weekNumber = client.TestGetIsoWeekNumber(date);

        // Assert
        Assert.Equal(expectedWeek, weekNumber);
    }

    [Fact]
    public async Task GetWeekLetter_WithNullUgebreve_CreatesDefaultContent()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getUgeBreve")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"ugebreve\": null}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 10, 16); // Week 42 of 2023

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekLetter(child, date);

        // Assert
        Assert.NotNull(result);
        var weekLetters = result["ugebreve"] as JArray;
        Assert.NotNull(weekLetters);
        Assert.Single(weekLetters);
        Assert.Equal("N/A", weekLetters[0]["klasseNavn"]?.ToString());
        Assert.Equal("42", weekLetters[0]["uge"]?.ToString());
        Assert.Equal("Der er ikke skrevet nogen ugenoter til denne uge", weekLetters[0]["indhold"]?.ToString());
    }

    [Fact]
    public async Task GetWeekLetter_ParsesJsonCorrectly_WhenApiReturnsValidData()
    {
        // Arrange
        var mockHandler = CreateMockHandlerWithLoginResponse();

        var complexJson = "{\"ugebreve\": [{\"klasseNavn\": \"1A\", \"uge\": \"15\", \"indhold\": \"<p>Matematik og dansk denne uge</p>\", \"dato\": \"2023-04-10\"}], \"meta\": {\"total\": 1, \"version\": \"2.1\"}}";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("getUgeBreve")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(complexJson)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TestableMinUddannelseClient(httpClient, "username", "password");

        var child = new Child { FirstName = "Test", LastName = "Child" };
        var date = new DateOnly(2023, 4, 10);

        // Login first
        await client.LoginAsync();

        // Act
        var result = await client.GetWeekLetter(child, date);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result["meta"]);
        Assert.Equal(1, result["meta"]!["total"]?.Value<int>());
        
        var weekLetters = result["ugebreve"] as JArray;
        Assert.NotNull(weekLetters);
        Assert.Single(weekLetters);
        Assert.Equal("1A", weekLetters[0]["klasseNavn"]?.ToString());
        Assert.Equal("15", weekLetters[0]["uge"]?.ToString());
    }
}