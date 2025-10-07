using MinUddannelse.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace MinUddannelse.Tests.Configuration;

/// <summary>
/// Focused tests for ConfigurationValidator to increase coverage.
/// These tests target specific validation methods and scenarios.
/// </summary>
public class ConfigurationValidatorCoverageTests
{
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILogger> _mockLogger;
    private readonly ConfigurationValidator _validator;

    public ConfigurationValidatorCoverageTests()
    {
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLogger = new Mock<ILogger>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_mockLogger.Object);
        _validator = new ConfigurationValidator(_mockLoggerFactory.Object);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithCompleteValidConfig_ReturnsSuccess()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "emma", Password = "pass123" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" },
            GoogleServiceAccount = new GoogleServiceAccount
            {
                ProjectId = "test-project",
                PrivateKeyId = "key-id",
                PrivateKey = "-----BEGIN PRIVATE KEY-----\ntest\n-----END PRIVATE KEY-----",
                ClientEmail = "test@test.iam.gserviceaccount.com",
                ClientId = "123456"
            },
            Scheduling = new MinUddannelse.Configuration.Scheduling
            {
                IntervalSeconds = 10,
                TaskExecutionWindowMinutes = 5,
                InitialOccurrenceOffsetMinutes = 1
            },
            WeekLetter = new WeekLetter
            {
                RetryIntervalHours = 6,
                MaxRetryDurationHours = 24
            }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingMinUddannelse_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = null!,
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("MinUddannelse configuration section is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithEmptyChildren_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig { Children = new List<Child>() },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("At least one child must be configured in MinUddannelse.Children section", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithChildMissingFirstName_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Child FirstName is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithChildMissingLastName_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Child LastName is required for Emma", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithNoValidChildCredentials_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = null
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("At least one child must have valid UniLogin credentials configured", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingOpenAi_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = null!,
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("OpenAi configuration section is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingOpenAiApiKey_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("OpenAi.ApiKey is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingOpenAiModel_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("OpenAi.Model is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingSupabase_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = null!
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Supabase configuration section is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingSupabaseUrl_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "", Key = "key123" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Supabase.Url is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithMissingSupabaseKey_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "" }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Supabase.Key is required", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithPartialGoogleConfig_ReturnsErrors()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" },
            GoogleServiceAccount = new GoogleServiceAccount
            {
                ProjectId = "test-project",
                PrivateKeyId = "", // Missing
                PrivateKey = "",   // Missing
                ClientEmail = "test@test.com",
                ClientId = ""      // Missing
            }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("GoogleServiceAccount.PrivateKeyId is required when Google Calendar is configured", result.Errors);
        Assert.Contains("GoogleServiceAccount.PrivateKey is required when Google Calendar is configured", result.Errors);
        Assert.Contains("GoogleServiceAccount.ClientId is required when Google Calendar is configured", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithInvalidSchedulingIntervalSeconds_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" },
            Scheduling = new MinUddannelse.Configuration.Scheduling
            {
                IntervalSeconds = 0,
                TaskExecutionWindowMinutes = 5,
                InitialOccurrenceOffsetMinutes = 1
            }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("Scheduling.IntervalSeconds must be greater than 0", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithInvalidWeekLetterRetryInterval_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" },
            WeekLetter = new WeekLetter
            {
                RetryIntervalHours = 0,
                MaxRetryDurationHours = 24
            }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("WeekLetter.RetryIntervalHours must be greater than 0", result.Errors);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithInvalidWeekLetterMaxDuration_ReturnsError()
    {
        var config = new Config
        {
            MinUddannelse = new MinUddannelseConfig
            {
                Children = new List<Child>
                {
                    new Child
                    {
                        FirstName = "Emma",
                        LastName = "Test",
                        UniLogin = new UniLogin { Username = "test", Password = "pass" }
                    }
                }
            },
            OpenAi = new OpenAi { ApiKey = "sk-test", Model = "gpt-3.5-turbo" },
            Supabase = new MinUddannelse.Configuration.Supabase { Url = "https://test.supabase.co", Key = "key123" },
            WeekLetter = new WeekLetter
            {
                RetryIntervalHours = 12,
                MaxRetryDurationHours = 6  // Less than interval
            }
        };

        var result = await _validator.ValidateConfigurationAsync(config);

        Assert.False(result.IsValid);
        Assert.Contains("WeekLetter.MaxRetryDurationHours must be greater than or equal to RetryIntervalHours", result.Errors);
    }
}
