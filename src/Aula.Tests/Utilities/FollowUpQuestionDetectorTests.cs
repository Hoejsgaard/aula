using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Aula.Configuration;
using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class FollowUpQuestionDetectorTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly List<Child> _testChildren;

    public FollowUpQuestionDetectorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _testChildren = new List<Child>
        {
            new Child { FirstName = "Emma Rose", LastName = "Wilson" },
            new Child { FirstName = "Liam", LastName = "Johnson" },
            new Child { FirstName = "Hans Martin", LastName = "Hans Martinsen" },
            new Child { FirstName = "Søren", LastName = "Andersen" },
            new Child { FirstName = "Johannes", LastName = "Nielsen" }
        };
    }

    [Fact]
    public void IsFollowUpQuestion_WithNullInput_ReturnsFalse()
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(null!, _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFollowUpQuestion_WithEmptyInput_ReturnsFalse()
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion("", _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFollowUpQuestion_WithWhitespaceOnly_ReturnsFalse()
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion("   \t\n  ", _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("what about Emma")]
    [InlineData("how about Liam")]
    [InlineData("What About Emma?")]
    [InlineData("and what about today")]
    [InlineData("also Emma")]
    [InlineData("and?")]
    public void IsFollowUpQuestion_WithEnglishFollowUpPhrases_ReturnsTrue(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("hvad med Emma")]
    [InlineData("hvordan med Liam")]
    [InlineData("og hvad med idag")]
    [InlineData("også Emma")]
    [InlineData("og?")]
    public void IsFollowUpQuestion_WithDanishFollowUpPhrases_ReturnsTrue(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("og Emma")]
    [InlineData("and Liam")]
    [InlineData("Og Emma?")]
    [InlineData("And Liam")]
    public void IsFollowUpQuestion_WithStartingFollowUpWords_ReturnsTrue(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("okay")]
    [InlineData("OK")]
    [InlineData("Okay")]
    [InlineData("short?")]
    [InlineData("what?")]
    public void IsFollowUpQuestion_WithShortFollowUps_ReturnsTrue(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("Emma")]
    [InlineData("EMMA")]
    [InlineData("emma")]
    [InlineData("Liam")]
    [InlineData("Wilson")]
    [InlineData("Hans Martinsen")]
    public void IsFollowUpQuestion_WithChildNameAndShortMessage_ReturnsTrue(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("Hans Martin")]
    [InlineData("Johannes")]
    [InlineData("søren")]
    [InlineData("SØREN")]
    public void IsFollowUpQuestion_WithUnicodeChildNames_WorksCorrectly(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("Emma today")]
    [InlineData("Liam tomorrow")]
    [InlineData("Emma i dag")]
    [InlineData("Liam i morgen")]
    public void IsFollowUpQuestion_WithChildNameAndTimeReference_ReturnsFalse(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("This is a very long message that contains Emma but should not be considered a follow up question")]
    [InlineData("A detailed question about Liam's homework that is definitely not a follow up")]
    public void IsFollowUpQuestion_WithLongMessageContainingChildName_ReturnsFalse(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("goodbye")]
    [InlineData("random text")]
    [InlineData("no child names here")]
    public void IsFollowUpQuestion_WithoutFollowUpPhrases_ReturnsFalse(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFollowUpQuestion_WithEmptyChildrenList_HandlesSafely()
    {
        // Arrange
        var emptyChildren = new List<Child>();

        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion("Emma", emptyChildren, _mockLogger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFollowUpQuestion_WithMultipleChildNamesInShortMessage_ReturnsTrue()
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion("Emma Liam", _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("what about Emma Rose")]
    [InlineData("hvad med Hans Martin")]
    [InlineData("Emma Rose?")]
    [InlineData("Hans Martin")]
    public void IsFollowUpQuestion_WithFullChildNames_WorksCorrectly(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFollowUpQuestion_LogsCorrectlyForDetectedFollowUp()
    {
        // Act
        FollowUpQuestionDetector.IsFollowUpQuestion("what about Emma", _testChildren, _mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Detected follow-up question")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public void IsFollowUpQuestion_LogsCorrectlyForShortMessage()
    {
        // Act
        FollowUpQuestionDetector.IsFollowUpQuestion("ok", _testChildren, _mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Detected likely follow-up based on short message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Theory]
    [InlineData("Emma's")]
    [InlineData("Emma?")]
    [InlineData("Emma!")]
    [InlineData("Emma.")]
    [InlineData("Emma,")]
    public void IsFollowUpQuestion_WithPunctuationAroundChildName_WorksCorrectly(string input)
    {
        // Act
        var result = FollowUpQuestionDetector.IsFollowUpQuestion(input, _testChildren, _mockLogger.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsFollowUpQuestion_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert - Logger is required for logging
        Assert.Throws<ArgumentNullException>(() =>
            FollowUpQuestionDetector.IsFollowUpQuestion("what about Emma", _testChildren, null!));
    }
}