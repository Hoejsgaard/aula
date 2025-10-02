using Aula.Communication.Channels;
using Aula.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aula.Tests.Communication.Channels;

public class MessageContentFilterTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly MessageContentFilter _filter;
    private readonly Child _testChild;

    public MessageContentFilterTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _filter = new MessageContentFilter(_mockLoggerFactory.Object);
        _testChild = new Child { FirstName = "Emil", LastName = "Hansen" };
    }

    [Fact]
    public void FilterForChild_RemovesOtherChildReferences()
    {
        // Arrange
        var message = @"Ugebrev for Emil Hansen - 3. klasse
Husk at Lars Nielsen skal have sin bog med i morgen.
Emil skal møde kl. 8:00.";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.Contains("Emil Hansen", filtered);
        Assert.Contains("Emil skal møde", filtered);
        Assert.DoesNotContain("Lars Nielsen", filtered);
    }

    [Fact]
    public void FilterForChild_RedactsCPRNumbers()
    {
        // Arrange
        var message = "CPR nummer: 1234567890 skal opdateres";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.DoesNotContain("1234567890", filtered);
        Assert.Contains("[CPR REDACTED]", filtered);
    }

    [Fact]
    public void FilterForChild_RedactsFormattedCPRNumbers()
    {
        // Arrange
        var message = "CPR: 1234-56-78-9012 skal verificeres";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.DoesNotContain("1234-56-78-9012", filtered);
        Assert.Contains("[CPR REDACTED]", filtered);
    }

    [Fact]
    public void FilterForChild_RedactsNonSchoolEmails()
    {
        // Arrange
        var message = @"Kontakt: parent@gmail.com eller teacher@aula.dk";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.DoesNotContain("parent@gmail.com", filtered);
        Assert.Contains("[EMAIL REDACTED]", filtered);
        Assert.Contains("teacher@aula.dk", filtered); // School emails preserved
    }

    [Fact]
    public void FilterForChild_RedactsPhoneNumbers()
    {
        // Arrange
        var message = "Ring på 12 34 56 78 eller +45 87654321";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.DoesNotContain("12 34 56 78", filtered);
        Assert.DoesNotContain("87654321", filtered);
        Assert.Contains("[PHONE REDACTED]", filtered);
    }

    [Fact]
    public void FilterForChild_RedactsHomeAddresses()
    {
        // Arrange
        var message = "Mødes på Elmevej 42, 2800 Lyngby";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.DoesNotContain("Elmevej 42", filtered);
        Assert.Contains("[ADDRESS REDACTED]", filtered);
    }

    [Fact]
    public void ContainsOtherChildData_DetectsOtherChildNames()
    {
        // Arrange
        var message = "Lars Nielsen - 3. klasse skal have ekstra hjælp";

        // Act
        var result = _filter.ContainsOtherChildData(message, _testChild);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsOtherChildData_AllowsCurrentChildName()
    {
        // Arrange
        var message = "Emil Hansen - 3. klasse har det godt";

        // Act
        var result = _filter.ContainsOtherChildData(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsOtherChildData_DetectsMultipleClasses()
    {
        // Arrange
        var message = @"3. klasse mødes kl. 8:00
5. klasse mødes kl. 9:00";

        // Act
        var result = _filter.ContainsOtherChildData(message, _testChild);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsOtherChildData_AllowsSingleClass()
    {
        // Arrange
        var message = @"3. klasse mødes kl. 8:00
3. klasse skal på tur";

        // Act
        var result = _filter.ContainsOtherChildData(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RemoveOtherChildReferences_RemovesLinesWithOtherChildren()
    {
        // Arrange
        var message = @"Emil Hansen skal huske madpakke
Lars Nielsen skal huske svømmetøj
Hele klassen mødes kl. 8";

        // Act
        var filtered = _filter.RemoveOtherChildReferences(message, _testChild);

        // Assert
        var lines = filtered.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("Emil Hansen", lines[0]);
        Assert.Contains("Hele klassen", lines[1]);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsMessagesWithOtherChildData()
    {
        // Arrange
        var message = "Lars Nielsen skal have sin bog med";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsScriptInjection()
    {
        // Arrange
        var message = "<script>alert('xss')</script>";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsJavaScriptUrls()
    {
        // Arrange
        var message = "Click here: javascript:alert('xss')";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsEventHandlers()
    {
        // Arrange
        var message = "<img src=x onerror='alert(1)'>";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsIframes()
    {
        // Arrange
        var message = "<iframe src='http://evil.com'></iframe>";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsSQLInjection()
    {
        // Arrange
        var message = "'; DROP TABLE users; --";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_RejectsVeryLongMessages()
    {
        // Arrange
        var message = new string('x', 10001);

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateMessageSafety_AcceptsSafeMessages()
    {
        // Arrange
        var message = "Emil Hansen skal huske sin bog i morgen";

        // Act
        var result = _filter.ValidateMessageSafety(message, _testChild);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FilterForChild_HandlesNullMessage()
    {
        // Act
        var result = _filter.FilterForChild(null!, _testChild);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FilterForChild_HandlesEmptyMessage()
    {
        // Act
        var result = _filter.FilterForChild(string.Empty, _testChild);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void RedactSensitiveInfo_PreservesSchoolEmails()
    {
        // Arrange
        var message = "Kontakt lærer@skolekom.dk eller admin@aula.dk";

        // Act
        var filtered = _filter.RedactSensitiveInfo(message);

        // Assert
        Assert.Contains("lærer@skolekom.dk", filtered);
        Assert.Contains("admin@aula.dk", filtered);
    }

    [Fact]
    public void FilterForChild_EnsuresConsistentChildNameFormat()
    {
        // Arrange
        var message = "Emil H. skal møde tidligt";

        // Act
        var filtered = _filter.FilterForChild(message, _testChild);

        // Assert
        Assert.Contains("Emil Hansen", filtered);
    }

    [Theory]
    [InlineData("Elev: Anna Jensen skal have ekstra tid", true)]
    [InlineData("Elev: Emil Hansen har klaret det godt", false)]
    [InlineData("Barn: Sophie Nielsen mangler tilladelse", true)]
    [InlineData("Barn: Emil har leveret opgaven", false)]
    public void ContainsOtherChildData_HandlesVariousPatterns(string message, bool expectedResult)
    {
        // Act
        var result = _filter.ContainsOtherChildData(message, _testChild);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void RemoveOtherChildReferences_PreservesEmptyLines()
    {
        // Arrange
        var message = @"Emil Hansen info

Lars Nielsen info

Generel info";

        // Act
        var filtered = _filter.RemoveOtherChildReferences(message, _testChild);

        // Assert
        Assert.Contains("Emil Hansen", filtered);
        Assert.DoesNotContain("Lars Nielsen", filtered);
        Assert.Contains("Generel info", filtered);
    }
}
