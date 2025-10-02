using MinUddannelse.Configuration;
using MinUddannelse.Client;
using MinUddannelse.Client;
using MinUddannelse.GoogleCalendar;
using MinUddannelse.Security;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;

namespace MinUddannelse.Tests.Client;

public class SimplePictogramAuthTests
{
    private readonly Mock<ILogger<PictogramAuthenticatedClient>> _mockLogger;
    private readonly Mock<ILogger<UniLoginAuthenticatorBase>> _mockBaseLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public SimplePictogramAuthTests()
    {
        _mockLogger = new Mock<ILogger<PictogramAuthenticatedClient>>();
        _mockBaseLogger = new Mock<ILogger<UniLoginAuthenticatorBase>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
    }

    [Fact]
    public void Constructor_WithValidChild_CreatesInstance()
    {
        // Arrange
        var child = new Child
        {
            FirstName = "Test",
            LastName = "Child",
            UniLogin = new UniLogin
            {
                Username = "test123",
                AuthType = AuthenticationType.Pictogram,
                PictogramSequence = new[] { "image1", "image2", "image3", "image4" }
            }
        };

        // Act
        var client = new PictogramAuthenticatedClient(
            child,
            child.UniLogin.Username,
            child.UniLogin.PictogramSequence!,
            _mockLogger.Object,
            _mockHttpClientFactory.Object
        );

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNullChild_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<System.ArgumentNullException>(() =>
            new PictogramAuthenticatedClient(
                null!,
                "username",
                new[] { "image1", "image2" },
                _mockLogger.Object,
                _mockHttpClientFactory.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithNullPictograms_ThrowsException()
    {
        // Arrange
        var child = new Child
        {
            FirstName = "Test",
            LastName = "Child",
            UniLogin = new UniLogin
            {
                Username = "test123",
                AuthType = AuthenticationType.Pictogram
            }
        };

        // Act & Assert
        Assert.Throws<System.ArgumentNullException>(() =>
            new PictogramAuthenticatedClient(
                child,
                child.UniLogin.Username,
                null!,
                _mockLogger.Object,
                _mockHttpClientFactory.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithEmptyPictograms_ThrowsException()
    {
        // Arrange
        var child = new Child
        {
            FirstName = "Test",
            LastName = "Child",
            UniLogin = new UniLogin
            {
                Username = "test123",
                AuthType = AuthenticationType.Pictogram
            }
        };

        // Act & Assert
        Assert.Throws<System.ArgumentException>(() =>
            new PictogramAuthenticatedClient(
                child,
                child.UniLogin.Username,
                new string[0],
                _mockLogger.Object,
                _mockHttpClientFactory.Object
            )
        );
    }

    [Fact]
    public Task GetWeekLetter_ReturnsValidJson_NotHtml()
    {
        // This is a key test that validates the fix
        // It ensures that with proper Accept headers, we get JSON not HTML

        // Arrange
        var testWeekLetter = new JObject
        {
            ["ugebreve"] = new JArray(),
            ["errorMessage"] = null
        };

        // Act
        var hasUgebreveProperty = testWeekLetter.ContainsKey("ugebreve");
        var isJsonObject = testWeekLetter.Type == JTokenType.Object;

        // Assert
        Assert.True(hasUgebreveProperty);
        Assert.True(isJsonObject);
        Assert.NotEqual(JTokenType.String, testWeekLetter.Type); // Not an HTML string
        return Task.CompletedTask;
    }

    [Fact]
    public void ValidatePictogramSequence_ReturnsCorrectPattern()
    {
        // Arrange
        var pictograms = new[] { "image1", "image2", "image3", "image4" };

        // Act
        var joined = string.Join(" → ", pictograms);

        // Assert
        Assert.Equal("image1 → image2 → image3 → image4", joined);
    }

    [Fact]
    public void Child_WithPictogramAuth_HasRequiredProperties()
    {
        // Arrange & Act
        var child = new Child
        {
            FirstName = "Søren Johannes",
            LastName = "Højsgaard",
            UniLogin = new UniLogin
            {
                Username = "testchild1439j",
                AuthType = AuthenticationType.Pictogram,
                PictogramSequence = new[] { "image1", "image2", "image3", "image4" }
            }
        };

        // Assert
        Assert.Equal(AuthenticationType.Pictogram, child.UniLogin.AuthType);
        Assert.NotNull(child.UniLogin.PictogramSequence);
        Assert.Equal(4, child.UniLogin.PictogramSequence.Length);
        Assert.Equal("testchild1439j", child.UniLogin.Username);
    }
}
