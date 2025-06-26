using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests;

public class OpenAiServiceTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly string _testApiKey = "test-api-key";

    public OpenAiServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
    }

    [Fact(Skip = "Requires valid OpenAI API key")]
    public async Task SummarizeWeekLetterAsync_WithValidInput_ReturnsSummary()
    {
        // Arrange
        var service = new OpenAiService(_testApiKey, _mockLoggerFactory.Object);
        var weekLetter = CreateTestWeekLetter();

        // Act
        var result = await service.SummarizeWeekLetterAsync(weekLetter);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact(Skip = "Requires valid OpenAI API key")]
    public async Task AskQuestionAboutWeekLetterAsync_WithValidInput_ReturnsAnswer()
    {
        // Arrange
        var service = new OpenAiService(_testApiKey, _mockLoggerFactory.Object);
        var weekLetter = CreateTestWeekLetter();
        var question = "What activities are planned for this week?";

        // Act
        var result = await service.AskQuestionAboutWeekLetterAsync(weekLetter, question);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact(Skip = "Requires valid OpenAI API key")]
    public async Task ExtractKeyInformationAsync_WithValidInput_ReturnsJsonObject()
    {
        // Arrange
        var service = new OpenAiService(_testApiKey, _mockLoggerFactory.Object);
        var weekLetter = CreateTestWeekLetter();

        // Act
        var result = await service.ExtractKeyInformationAsync(weekLetter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count > 0);
    }

    private static JObject CreateTestWeekLetter()
    {
        return JObject.Parse(@"
        {
            ""ugebreve"": [
                {
                    ""klasseNavn"": ""3A"",
                    ""uge"": ""35"",
                    ""indhold"": ""<p>Kære forældre,</p>
                        <p>I denne uge skal vi arbejde med:</p>
                        <ul>
                            <li>Matematik: Brøker og decimaltal</li>
                            <li>Dansk: Læsning af H.C. Andersen eventyr</li>
                            <li>Natur/teknologi: Undersøgelse af insekter</li>
                        </ul>
                        <p>Husk at medbringe:</p>
                        <ul>
                            <li>Regntøj til tur i skoven på torsdag</li>
                            <li>Idrætstøj til tirsdag og fredag</li>
                        </ul>
                        <p>Forældremøde næste onsdag kl. 17:00 i klasselokalet.</p>
                        <p>Med venlig hilsen,<br>Klasselæreren</p>""
                }
            ]
        }");
    }
} 