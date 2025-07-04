using Aula.Configuration;
using Xunit;

namespace Aula.Tests.Configuration;

public class OpenAiTests
{
    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var openAi = new OpenAi();

        // Assert
        Assert.NotNull(openAi.ApiKey);
        Assert.Equal(string.Empty, openAi.ApiKey);
        Assert.Equal("gpt-4", openAi.Model);
        Assert.Equal(2000, openAi.MaxTokens);
        Assert.Equal(0.7, openAi.Temperature);
        Assert.Equal(30, openAi.CacheExpirationMinutes);
    }

    [Fact]
    public void ApiKey_CanSetAndGetValue()
    {
        // Arrange
        var openAi = new OpenAi();
        var testApiKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";

        // Act
        openAi.ApiKey = testApiKey;

        // Assert
        Assert.Equal(testApiKey, openAi.ApiKey);
    }

    [Fact]
    public void Model_CanSetAndGetValue()
    {
        // Arrange
        var openAi = new OpenAi();
        var testModel = "gpt-3.5-turbo";

        // Act
        openAi.Model = testModel;

        // Assert
        Assert.Equal(testModel, openAi.Model);
    }

    [Fact]
    public void MaxTokens_CanSetAndGetValue()
    {
        // Arrange
        var openAi = new OpenAi();
        var testMaxTokens = 4000;

        // Act
        openAi.MaxTokens = testMaxTokens;

        // Assert
        Assert.Equal(testMaxTokens, openAi.MaxTokens);
    }

    [Fact]
    public void Temperature_CanSetAndGetValue()
    {
        // Arrange
        var openAi = new OpenAi();
        var testTemperature = 0.9;

        // Act
        openAi.Temperature = testTemperature;

        // Assert
        Assert.Equal(testTemperature, openAi.Temperature);
    }

    [Fact]
    public void CacheExpirationMinutes_CanSetAndGetValue()
    {
        // Arrange
        var openAi = new OpenAi();
        var testCacheExpiration = 60;

        // Act
        openAi.CacheExpirationMinutes = testCacheExpiration;

        // Assert
        Assert.Equal(testCacheExpiration, openAi.CacheExpirationMinutes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sk-1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("sk-proj-1234567890abcdefghijklmnopqrstuvwxyz")]
    [InlineData("very-long-api-key-with-many-characters")]
    public void ApiKey_AcceptsVariousFormats(string apiKey)
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.ApiKey = apiKey;

        // Assert
        Assert.Equal(apiKey, openAi.ApiKey);
    }

    [Theory]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4o")]
    [InlineData("text-davinci-003")]
    [InlineData("custom-model")]
    public void Model_AcceptsVariousModels(string model)
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.Model = model;

        // Assert
        Assert.Equal(model, openAi.Model);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(16000)]
    [InlineData(32000)]
    public void MaxTokens_AcceptsVariousValues(int maxTokens)
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.MaxTokens = maxTokens;

        // Assert
        Assert.Equal(maxTokens, openAi.MaxTokens);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Temperature_AcceptsVariousValues(double temperature)
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.Temperature = temperature;

        // Assert
        Assert.Equal(temperature, openAi.Temperature);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(1440)]
    public void CacheExpirationMinutes_AcceptsVariousValues(int minutes)
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.CacheExpirationMinutes = minutes;

        // Assert
        Assert.Equal(minutes, openAi.CacheExpirationMinutes);
    }

    [Fact]
    public void AllProperties_CanBeSetSimultaneously()
    {
        // Arrange
        var openAi = new OpenAi();
        var apiKey = "sk-test-key";
        var model = "gpt-3.5-turbo";
        var maxTokens = 1500;
        var temperature = 0.8;
        var cacheExpiration = 45;

        // Act
        openAi.ApiKey = apiKey;
        openAi.Model = model;
        openAi.MaxTokens = maxTokens;
        openAi.Temperature = temperature;
        openAi.CacheExpirationMinutes = cacheExpiration;

        // Assert
        Assert.Equal(apiKey, openAi.ApiKey);
        Assert.Equal(model, openAi.Model);
        Assert.Equal(maxTokens, openAi.MaxTokens);
        Assert.Equal(temperature, openAi.Temperature);
        Assert.Equal(cacheExpiration, openAi.CacheExpirationMinutes);
    }

    [Fact]
    public void OpenAi_ObjectInitializerSyntaxWorks()
    {
        // Arrange & Act
        var openAi = new OpenAi
        {
            ApiKey = "sk-test-initialization",
            Model = "gpt-4-turbo",
            MaxTokens = 3000,
            Temperature = 0.3,
            CacheExpirationMinutes = 90
        };

        // Assert
        Assert.Equal("sk-test-initialization", openAi.ApiKey);
        Assert.Equal("gpt-4-turbo", openAi.Model);
        Assert.Equal(3000, openAi.MaxTokens);
        Assert.Equal(0.3, openAi.Temperature);
        Assert.Equal(90, openAi.CacheExpirationMinutes);
    }

    [Fact]
    public void OpenAi_HasCorrectNamespace()
    {
        // Arrange
        var type = typeof(OpenAi);

        // Act & Assert
        Assert.Equal("Aula.Configuration", type.Namespace);
    }

    [Fact]
    public void OpenAi_IsPublicClass()
    {
        // Arrange
        var type = typeof(OpenAi);

        // Act & Assert
        Assert.True(type.IsPublic);
        Assert.False(type.IsAbstract);
        Assert.False(type.IsSealed);
    }

    [Fact]
    public void OpenAi_HasParameterlessConstructor()
    {
        // Arrange
        var type = typeof(OpenAi);

        // Act
        var constructor = type.GetConstructor(System.Type.EmptyTypes);

        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    [Fact]
    public void OpenAi_PropertiesAreIndependent()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.ApiKey = "changed-key";
        // Other properties should remain at their defaults

        // Assert
        Assert.Equal("changed-key", openAi.ApiKey);
        Assert.Equal("gpt-4", openAi.Model); // Should remain default
        Assert.Equal(2000, openAi.MaxTokens); // Should remain default
        Assert.Equal(0.7, openAi.Temperature); // Should remain default
        Assert.Equal(30, openAi.CacheExpirationMinutes); // Should remain default
    }

    [Fact]
    public void ApiKey_CanBeSetToNull()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.ApiKey = null!;

        // Assert
        Assert.Null(openAi.ApiKey);
    }

    [Fact]
    public void Model_CanBeSetToNull()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act
        openAi.Model = null!;

        // Assert
        Assert.Null(openAi.Model);
    }

    [Fact]
    public void OpenAi_SupportsCommonOpenAiKeyFormats()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act & Assert - Standard API key format
        openAi.ApiKey = "sk-1234567890abcdefghijklmnopqrstuvwxyz";
        Assert.Equal("sk-1234567890abcdefghijklmnopqrstuvwxyz", openAi.ApiKey);
        Assert.StartsWith("sk-", openAi.ApiKey);

        // Act & Assert - Project API key format
        openAi.ApiKey = "sk-proj-1234567890abcdefghijklmnopqrstuvwxyz";
        Assert.Equal("sk-proj-1234567890abcdefghijklmnopqrstuvwxyz", openAi.ApiKey);
        Assert.StartsWith("sk-proj-", openAi.ApiKey);
    }

    [Fact]
    public void OpenAi_SupportsCommonModelNames()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act & Assert - GPT-4 models
        openAi.Model = "gpt-4";
        Assert.Equal("gpt-4", openAi.Model);

        openAi.Model = "gpt-4-turbo";
        Assert.Equal("gpt-4-turbo", openAi.Model);

        openAi.Model = "gpt-4o";
        Assert.Equal("gpt-4o", openAi.Model);

        // Act & Assert - GPT-3.5 models
        openAi.Model = "gpt-3.5-turbo";
        Assert.Equal("gpt-3.5-turbo", openAi.Model);
    }

    [Fact]
    public void OpenAi_SupportsReasonableTemperatureRange()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act & Assert - Low creativity
        openAi.Temperature = 0.1;
        Assert.Equal(0.1, openAi.Temperature);

        // Act & Assert - Balanced
        openAi.Temperature = 0.7;
        Assert.Equal(0.7, openAi.Temperature);

        // Act & Assert - High creativity
        openAi.Temperature = 1.0;
        Assert.Equal(1.0, openAi.Temperature);
    }

    [Fact]
    public void OpenAi_SupportsCommonTokenLimits()
    {
        // Arrange
        var openAi = new OpenAi();

        // Act & Assert - Small responses
        openAi.MaxTokens = 500;
        Assert.Equal(500, openAi.MaxTokens);

        // Act & Assert - Medium responses
        openAi.MaxTokens = 2000;
        Assert.Equal(2000, openAi.MaxTokens);

        // Act & Assert - Large responses
        openAi.MaxTokens = 4000;
        Assert.Equal(4000, openAi.MaxTokens);
    }
}