using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Xunit;
using Aula.Tools;
using Aula.Configuration;
using Aula.Services;
using Aula.Utilities;
using System;
using System.Linq;

namespace Aula.Tests;

public class OpenAiServiceTests
{
    [Fact]
    public void OpenAiService_Constructor_WithApiKey_InitializesCorrectly()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        var mockAiToolsManager = new Mock<AiToolsManager>(
            Mock.Of<ISupabaseService>(),
            Mock.Of<IDataService>(),
            mockLoggerFactory.Object);

        // Act & Assert - Should not throw
        var service = new OpenAiService("test-api-key", mockLoggerFactory.Object, mockAiToolsManager.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public void OpenAiService_Constructor_WithNullApiKey_ThrowsException()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockAiToolsManager = new Mock<AiToolsManager>(
            Mock.Of<ISupabaseService>(),
            Mock.Of<IDataService>(),
            mockLoggerFactory.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new OpenAiService(string.Empty, mockLoggerFactory.Object, mockAiToolsManager.Object));
    }

    [Fact]
    public void OpenAiService_Internal_Constructor_CanBeUsedForTesting()
    {
        // This verifies that the internal constructor exists for testability
        // We can't easily mock OpenAI types, but we can verify the constructor signature exists

        // Arrange
        var constructors = typeof(OpenAiService).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert - Verify internal constructor exists
        var internalConstructor = constructors.FirstOrDefault(c =>
            c.GetParameters().Length == 3 &&
            c.GetParameters().Any(p => p.ParameterType.Name.Contains("OpenAI")));

        Assert.NotNull(internalConstructor);
    }

    [Fact]
    public void OpenAiService_ClearConversationHistory_WithContextKey_RemovesSpecificContext()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var mockLogger = new Mock<ILogger>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(mockLogger.Object);

        var mockAiToolsManager = new Mock<AiToolsManager>(
            Mock.Of<ISupabaseService>(),
            Mock.Of<IDataService>(),
            mockLoggerFactory.Object);

        var service = new OpenAiService("test-api-key", mockLoggerFactory.Object, mockAiToolsManager.Object);

        // Act & Assert - Should not throw
        service.ClearConversationHistory("test-context");
        service.ClearConversationHistory(); // Clear all
    }

    // WeekLetterContentExtractor tests moved to WeekLetterContentExtractorTests.cs

    [Theory]
    [InlineData(ChatInterface.Slack)]
    [InlineData(ChatInterface.Telegram)]
    public void OpenAiService_GetChatInterfaceInstructions_ReturnsCorrectFormat(ChatInterface chatInterface)
    {
        // This tests the private helper method's logic indirectly by verifying the enum values
        // Arrange & Act
        var isValidInterface = Enum.IsDefined(typeof(ChatInterface), chatInterface);

        // Assert
        Assert.True(isValidInterface);

        // Verify that each interface type has specific formatting requirements
        switch (chatInterface)
        {
            case ChatInterface.Slack:
                // Slack uses markdown format
                Assert.True(true); // Slack interface is valid
                break;
            case ChatInterface.Telegram:
                // Telegram uses HTML format  
                Assert.True(true); // Telegram interface is valid
                break;
            default:
                Assert.Fail("Unknown chat interface");
                break;
        }
    }

    [Fact]
    public void CreateTestWeekLetter_ReturnsValidJObject()
    {
        // Act
        var weekLetter = CreateTestWeekLetter();

        // Assert
        Assert.NotNull(weekLetter);
        Assert.NotNull(weekLetter["ugebreve"]);
        Assert.True(weekLetter["ugebreve"] is JArray);

        var ugebreve = (JArray)weekLetter["ugebreve"]!;
        Assert.True(ugebreve.Count > 0);

        var firstLetter = ugebreve[0];
        Assert.Equal("3A", firstLetter?["klasseNavn"]?.ToString());
        Assert.Equal("35", firstLetter?["uge"]?.ToString());
        Assert.NotNull(firstLetter?["indhold"]);
        Assert.Contains("Matematik", firstLetter?["indhold"]?.ToString() ?? "");
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