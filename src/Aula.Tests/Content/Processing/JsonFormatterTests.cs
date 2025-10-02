using Xunit;
using Aula.Content.WeekLetters;
using Aula.Content.Processing;
using Aula;
using Newtonsoft.Json;

namespace Aula.Tests.Content.Processing;

public class JsonFormatterTests
{
    [Fact]
    public void Prettify_WithValidJson_ReturnsFormattedJson()
    {
        // Arrange
        var compactJson = "{\"name\":\"test\",\"value\":123,\"nested\":{\"array\":[1,2,3]}}";

        // Act
        var result = JsonFormatter.Prettify(compactJson);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(compactJson, result);
        Assert.Contains("\"name\"", result);
        Assert.Contains("\"test\"", result);
        Assert.Contains("123", result);
        Assert.Contains("\"nested\"", result);
        Assert.Contains("[", result);
        Assert.Contains("]", result);
        // Should contain newlines and indentation
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Prettify_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => JsonFormatter.Prettify(null!));
    }

    [Fact]
    public void Prettify_WithEmptyString_ReturnsNull()
    {
        // Act
        var result = JsonFormatter.Prettify("");

        // Assert
        Assert.Equal("null", result);
    }

    [Fact]
    public void Prettify_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = "{\"name\":\"test\",\"value\":}"; // Missing value

        // Act & Assert
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonFormatter.Prettify(invalidJson));
    }

    [Fact]
    public void Prettify_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var malformedJson = "not json at all";

        // Act & Assert
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JsonFormatter.Prettify(malformedJson));
    }

    [Fact]
    public void Prettify_WithAlreadyFormattedJson_ReturnsFormattedJson()
    {
        // Arrange
        var formattedJson = @"{
  ""name"": ""test"",
  ""value"": 123
}";

        // Act
        var result = JsonFormatter.Prettify(formattedJson);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"name\"", result);
        Assert.Contains("\"test\"", result);
        Assert.Contains("123", result);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Prettify_WithComplexNestedStructure_FormatsCorrectly()
    {
        // Arrange
        var complexJson = "{\"users\":[{\"id\":1,\"name\":\"John\",\"roles\":[\"admin\",\"user\"]},{\"id\":2,\"name\":\"Jane\",\"metadata\":{\"lastLogin\":\"2023-01-01\",\"preferences\":{\"theme\":\"dark\"}}}]}";

        // Act
        var result = JsonFormatter.Prettify(complexJson);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(complexJson, result);
        Assert.Contains("\"users\"", result);
        Assert.Contains("\"John\"", result);
        Assert.Contains("\"Jane\"", result);
        Assert.Contains("\"admin\"", result);
        Assert.Contains("\"lastLogin\"", result);
        Assert.Contains("\"preferences\"", result);
        Assert.Contains("\"theme\"", result);
        Assert.Contains("\"dark\"", result);
        // Should have proper indentation
        Assert.Contains("  ", result);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Prettify_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var jsonWithSpecialChars = "{\"message\":\"Hello\\nWorld\\t!\",\"unicode\":\"åäö\",\"escaped\":\"quote: \\\"test\\\"\"}";

        // Act
        var result = JsonFormatter.Prettify(jsonWithSpecialChars);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Hello\\nWorld\\t!", result);
        Assert.Contains("åäö", result);
        Assert.Contains("\\\"test\\\"", result);
    }

    [Fact]
    public void Prettify_WithNumbers_PreservesNumericTypes()
    {
        // Arrange
        var jsonWithNumbers = "{\"integer\":42,\"decimal\":3.14159,\"negative\":-100,\"zero\":0,\"scientific\":0.000123}";

        // Act
        var result = JsonFormatter.Prettify(jsonWithNumbers);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("42", result);
        Assert.Contains("3.14159", result);
        Assert.Contains("-100", result);
        Assert.Contains("0", result);
        Assert.Contains("0.000123", result);
    }

    [Fact]
    public void Prettify_WithBooleanAndNull_PreservesTypes()
    {
        // Arrange
        var jsonWithTypes = "{\"isTrue\":true,\"isFalse\":false,\"nullValue\":null}";

        // Act
        var result = JsonFormatter.Prettify(jsonWithTypes);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("true", result);
        Assert.Contains("false", result);
        Assert.Contains("null", result);
    }

    [Fact]
    public void Prettify_WithEmptyObjects_HandlesCorrectly()
    {
        // Arrange
        var jsonWithEmpty = "{\"emptyObject\":{},\"emptyArray\":[],\"normalField\":\"value\"}";

        // Act
        var result = JsonFormatter.Prettify(jsonWithEmpty);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("{}", result);
        Assert.Contains("[]", result);
        Assert.Contains("\"value\"", result);
    }

    [Fact]
    public void Prettify_WithVeryLargeJson_HandlesPerformantly()
    {
        // Arrange
        var largeArray = "[" + string.Join(",", Enumerable.Range(1, 1000).Select(i => $"{{\"id\":{i},\"name\":\"item{i}\"}}")) + "]";

        // Act
        var result = JsonFormatter.Prettify(largeArray);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("\"id\"", result);
        Assert.Contains("\"name\"", result);
        Assert.Contains("item1", result);
        Assert.Contains("item1000", result);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("{\"incomplete\":")]
    public void Prettify_WithIncompleteJson_ThrowsException(string incompleteJson)
    {
        // Act & Assert
        var exception = Assert.ThrowsAny<Exception>(() => JsonFormatter.Prettify(incompleteJson));
        Assert.True(exception is Newtonsoft.Json.JsonException ||
                   exception is Newtonsoft.Json.JsonReaderException ||
                   exception is Newtonsoft.Json.JsonWriterException ||
                   exception is Newtonsoft.Json.JsonSerializationException);
    }

    [Fact]
    public void Prettify_WithStringValue_ReturnsFormattedString()
    {
        // Arrange
        var stringJson = "\"string without object\"";

        // Act
        var result = JsonFormatter.Prettify(stringJson);

        // Assert
        Assert.Equal("\"string without object\"", result);
    }

    [Fact]
    public void Prettify_WithWhitespaceOnly_ReturnsNull()
    {
        // Arrange
        var whitespaceJson = "   \t\n   ";

        // Act
        var result = JsonFormatter.Prettify(whitespaceJson);

        // Assert
        Assert.Equal("null", result);
    }
}
