using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Aula.Integration;
using Aula.Configuration;

namespace Aula.Tests.Integration;

public class MinUddannelseClientTests
{
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
        var mockHandler = new Mock<HttpMessageHandler>();

        // Setup mock response for GetWeekLetter
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
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
}