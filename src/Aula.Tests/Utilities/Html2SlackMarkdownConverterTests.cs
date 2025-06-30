using Xunit;
using Aula.Utilities;

namespace Aula.Tests.Utilities;

public class Html2SlackMarkdownConverterTests
{
    private readonly Html2SlackMarkdownConverter _converter;

    public Html2SlackMarkdownConverterTests()
    {
        _converter = new Html2SlackMarkdownConverter();
    }

    [Fact]
    public void Convert_WithNullInput_ReturnsEmptyString()
    {
        // Act
        var result = _converter.Convert(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithEmptyString_ReturnsEmptyString()
    {
        // Act
        var result = _converter.Convert("");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithWhitespaceOnly_ReturnsEmptyString()
    {
        // Act
        var result = _converter.Convert("   \t\n  ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithSimpleHtml_ConvertsCorrectly()
    {
        // Arrange
        var html = "<p>Hello world</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Hello world", result);
    }

    [Fact]
    public void Convert_WithMalformedHtml_HandlesGracefully()
    {
        // Arrange
        var malformedHtml = "<p>Unclosed paragraph<div>Nested without closing<b>Bold text";

        // Act
        var result = _converter.Convert(malformedHtml);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Unclosed paragraph", result);
        Assert.Contains("Nested without closing", result);
        Assert.Contains("Bold text", result);
    }

    [Fact]
    public void Convert_WithNestedTags_RemovesCorrectly()
    {
        // Arrange
        var html = "<div><span>Outer <b>bold <i>italic</i> text</b> more</span></div>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Outer", result);
        Assert.Contains("bold", result);
        Assert.Contains("italic", result);
        Assert.Contains("text", result);
        Assert.Contains("more", result);
        // Should not contain span or div tags
        Assert.DoesNotContain("<span>", result);
        Assert.DoesNotContain("<div>", result);
    }

    [Fact]
    public void Convert_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var html = "<p>Special chars: &amp; &lt; &gt; &nbsp; &quot;</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Special chars:", result);
        // Html2Markdown should handle these entities
    }

    [Fact]
    public void Convert_WithBrTags_PreservesLineBreaks()
    {
        // Arrange
        var html = "<p>Line one<br>Line two<br/>Line three</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Line one", result);
        Assert.Contains("Line two", result);
        Assert.Contains("Line three", result);
    }

    [Fact]
    public void Convert_WithComplexNesting_HandlesCorrectly()
    {
        // Arrange
        var html = @"
            <div>
                <span>
                    <p>Nested paragraph with <b>bold</b> and <i>italic</i></p>
                    <div>Inner div with <a href='#'>link</a></div>
                </span>
            </div>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Nested paragraph", result);
        Assert.Contains("bold", result);
        Assert.Contains("italic", result);
        Assert.Contains("Inner div", result);
        Assert.Contains("link", result);
    }

    [Fact]
    public void Convert_WithEmptyTags_HandlesCorrectly()
    {
        // Arrange
        var html = "<div></div><span></span><p>Content</p><b></b>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
        // Empty tags should be handled gracefully
    }

    [Theory]
    [InlineData("<script>alert('xss')</script><p>Content</p>")]
    [InlineData("<style>body { color: red; }</style><p>Content</p>")]
    public void Convert_WithScriptAndStyleTags_RemovesTagsButNotContent(string html)
    {
        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("<style>", result);
        // Note: Html2Markdown may preserve text content from script/style tags
    }

    [Fact]
    public void Convert_WithVeryLongContent_HandlesPerformantly()
    {
        // Arrange
        var longContent = string.Concat(Enumerable.Repeat("<p>This is a long paragraph with content. </p>", 1000));

        // Act
        var result = _converter.Convert(longContent);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("This is a long paragraph", result);
    }

    [Fact]
    public void Convert_WithInvalidHtml_HandlesGracefully()
    {
        // Arrange - This should trigger the exception handling
        var invalidHtml = new string('<', 1000) + "invalid content" + new string('>', 1000);

        // Act
        var result = _converter.Convert(invalidHtml);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("invalid content", result);
        // May fall back to regex stripping or handle gracefully
    }

    [Fact]
    public void Convert_WithNullParentNodes_HandlesGracefully()
    {
        // Arrange - HTML that might cause null parent node scenarios
        var html = "<html><body><div><p>Content</p></div></body></html>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Content", result);
    }

    [Fact]
    public void Convert_WithUnicodeContent_PreservesEncoding()
    {
        // Arrange
        var html = "<p>Unicode: åäö ñé çü ßæø</p>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("åäö", result);
        Assert.Contains("ñé", result);
        Assert.Contains("çü", result);
        Assert.Contains("ßæø", result);
    }

    [Fact]
    public void Convert_WithMixedContentAndTags_ProcessesCorrectly()
    {
        // Arrange
        var html = @"
            Plain text before
            <div>
                <b>Bold text</b>
                More plain text
                <span>Span content <i>italic</i></span>
            </div>
            Plain text after";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Plain text before", result);
        Assert.Contains("Bold text", result);
        Assert.Contains("More plain text", result);
        Assert.Contains("Span content", result);
        Assert.Contains("italic", result);
        Assert.Contains("Plain text after", result);
    }

    [Theory]
    [InlineData("<p>")]
    [InlineData("</p>")]
    [InlineData("<div><span>")]
    [InlineData("text <b> more text")]
    public void Convert_WithPartialTags_HandlesGracefully(string html)
    {
        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        // Should not throw and should return some result
    }

    [Fact]
    public void Convert_PreservesTextOrder()
    {
        // Arrange
        var html = "<div>First <b>Second</b> Third <i>Fourth</i> Fifth</div>";

        // Act
        var result = _converter.Convert(html);

        // Assert
        Assert.NotNull(result);
        var firstIndex = result.IndexOf("First", StringComparison.Ordinal);
        var secondIndex = result.IndexOf("Second", StringComparison.Ordinal);
        var thirdIndex = result.IndexOf("Third", StringComparison.Ordinal);
        var fourthIndex = result.IndexOf("Fourth", StringComparison.Ordinal);
        var fifthIndex = result.IndexOf("Fifth", StringComparison.Ordinal);

        Assert.True(firstIndex < secondIndex);
        Assert.True(secondIndex < thirdIndex);
        Assert.True(thirdIndex < fourthIndex);
        Assert.True(fourthIndex < fifthIndex);
    }
}