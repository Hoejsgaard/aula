using Microsoft.Extensions.Logging;
using Moq;
using Aula.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Aula.Tests.Configuration;

public class ConfigurationValidatorTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ITimeProvider> _mockTimeProvider;
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockTimeProvider = new Mock<ITimeProvider>();

        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        _mockTimeProvider.Setup(x => x.Now).Returns(DateTime.Now);

        _validator = new ConfigurationValidator(_mockLoggerFactory.Object, _mockTimeProvider.Object);
    }

    [Fact]
    public void Constructor_WithValidLoggerFactory_InitializesCorrectly()
    {
        // Arrange & Act
        var validator = new ConfigurationValidator(_mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigurationValidator(null!));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Arrange & Act
        var validator = new ConfigurationValidator(_mockLoggerFactory.Object, null);

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _validator.ValidateConfigurationAsync(null!));
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithValidCompleteConfig_ReturnsSuccess()
    {
        // Arrange
        var config = CreateValidConfig();

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptyChildren_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>()
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("At least one child must be configured in MinUddannelse.Children section", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithChildMissingFirstName_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "", LastName = "Test" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Child FirstName is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithChildMissingLastName_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Child LastName is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptyUniLoginUsername_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "Child" } }
            },
            UniLogin = new UniLogin { Username = "", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("UniLogin.Username is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptyUniLoginPassword_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "Child" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("UniLogin.Password is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptyOpenAiApiKey_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "Child" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("OpenAi.ApiKey is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptySupabaseUrl_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "Child" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "", Key = "test" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Supabase.Url is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptySupabaseKey_ReturnsFailure()
    {
        // Arrange
        var config = new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child> { new Child { FirstName = "Test", LastName = "Child" } }
            },
            UniLogin = new UniLogin { Username = "test", Password = "test" },
            OpenAi = new OpenAi { ApiKey = "test" },
            Supabase = new Aula.Configuration.Supabase { Url = "https://test.com", Key = "" }
        };

        // Act
        var result = await _validator.ValidateConfigurationAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Supabase.Key is required", result.Errors);
    }

    [Fact]
    public void ConfigurationValidator_ImplementsIConfigurationValidatorInterface()
    {
        // Arrange & Act & Assert
        Assert.True(typeof(IConfigurationValidator).IsAssignableFrom(typeof(ConfigurationValidator)));
    }

    [Fact]
    public void ConfigurationValidator_HasCorrectNamespace()
    {
        // Arrange
        var validatorType = typeof(ConfigurationValidator);

        // Act & Assert
        Assert.Equal("Aula.Configuration", validatorType.Namespace);
    }

    [Fact]
    public void ConfigurationValidator_IsPublicClass()
    {
        // Arrange
        var validatorType = typeof(ConfigurationValidator);

        // Act & Assert
        Assert.True(validatorType.IsPublic);
        Assert.False(validatorType.IsAbstract);
        Assert.False(validatorType.IsSealed);
    }

    [Fact]
    public void ConfigurationValidator_HasCorrectPublicMethods()
    {
        // Arrange
        var validatorType = typeof(ConfigurationValidator);

        // Act & Assert
        Assert.NotNull(validatorType.GetMethod("ValidateConfigurationAsync"));
    }

    [Fact]
    public void ConfigurationValidator_ConstructorParametersHaveCorrectTypes()
    {
        // Arrange
        var validatorType = typeof(ConfigurationValidator);
        var constructor = validatorType.GetConstructors()[0];

        // Act
        var parameters = constructor.GetParameters();

        // Assert
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(ILoggerFactory), parameters[0].ParameterType);
        Assert.Equal(typeof(ITimeProvider), parameters[1].ParameterType);
    }

    private static Config CreateValidConfig()
    {
        return new Config
        {
            MinUddannelse = new MinUddannelse
            {
                Children = new List<Child>
                {
                    new Child { FirstName = "Test", LastName = "Child" }
                }
            },
            UniLogin = new UniLogin
            {
                Username = "test-user",
                Password = "test-password"
            },
            OpenAi = new OpenAi
            {
                ApiKey = "test-api-key"
            },
            Supabase = new Aula.Configuration.Supabase
            {
                Url = "https://test.supabase.co",
                Key = "test-key"
            }
        };
    }
}