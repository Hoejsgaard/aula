using Microsoft.Extensions.Logging;

namespace Aula.Configuration;

public class ConfigurationValidator : IConfigurationValidator
{
    private readonly ILogger _logger;
    private readonly ITimeProvider _timeProvider;

    public ConfigurationValidator(ILoggerFactory loggerFactory, ITimeProvider? timeProvider = null)
    {
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(nameof(ConfigurationValidator));
        _timeProvider = timeProvider ?? new SystemTimeProvider();
    }

    public async Task<ValidationResult> ValidateConfigurationAsync(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate required sections
        ValidateMinUddannelse(config.MinUddannelse, errors);
        ValidateUniLogin(config.UniLogin, errors);
        ValidateOpenAi(config.OpenAi, errors);
        ValidateSupabase(config.Supabase, errors);

        // Optional features - validate if enabled
        ValidateSlack(config.Slack, errors, warnings);
        ValidateTelegram(config.Telegram, errors, warnings);
        ValidateGoogleServiceAccount(config.GoogleServiceAccount, errors, warnings);
        ValidateFeatures(config.Features, errors, warnings);
        ValidateTimers(config.Timers, errors, warnings);

        // Simulate async work if needed in future
        await Task.CompletedTask;

        if (errors.Count == 0)
        {
            _logger.LogInformation("Configuration validation completed successfully");
            return ValidationResult.Success(warnings);
        }
        else
        {
            _logger.LogError("Configuration validation failed with {ErrorCount} errors", errors.Count);
            return ValidationResult.Failure(errors, warnings);
        }
    }

    private void ValidateMinUddannelse(MinUddannelse minUddannelse, List<string> errors)
    {
        if (minUddannelse == null)
        {
            errors.Add("MinUddannelse configuration section is required");
            return;
        }

        if (minUddannelse.Children == null || !minUddannelse.Children.Any())
        {
            errors.Add("At least one child must be configured in MinUddannelse.Children section");
            return;
        }

        foreach (var child in minUddannelse.Children)
        {
            if (string.IsNullOrWhiteSpace(child.FirstName))
            {
                errors.Add("Child FirstName is required");
            }
            if (string.IsNullOrWhiteSpace(child.LastName))
            {
                errors.Add("Child LastName is required");
            }
        }
    }

    private void ValidateUniLogin(UniLogin uniLogin, List<string> errors)
    {
        if (uniLogin == null)
        {
            errors.Add("UniLogin configuration section is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(uniLogin.Username))
        {
            errors.Add("UniLogin.Username is required");
        }

        if (string.IsNullOrWhiteSpace(uniLogin.Password))
        {
            errors.Add("UniLogin.Password is required");
        }
    }

    private void ValidateOpenAi(OpenAi openAi, List<string> errors)
    {
        if (openAi == null)
        {
            errors.Add("OpenAi configuration section is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(openAi.ApiKey))
        {
            errors.Add("OpenAi.ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(openAi.Model))
        {
            errors.Add("OpenAi.Model is required");
        }
    }

    private void ValidateSupabase(Supabase supabase, List<string> errors)
    {
        if (supabase == null)
        {
            errors.Add("Supabase configuration section is required");
            return;
        }

        if (string.IsNullOrWhiteSpace(supabase.Url))
        {
            errors.Add("Supabase.Url is required");
        }

        if (string.IsNullOrWhiteSpace(supabase.Key))
        {
            errors.Add("Supabase.Key is required");
        }
    }

    private void ValidateSlack(Slack slack, List<string> errors, List<string> warnings)
    {
        if (slack == null)
        {
            warnings.Add("Slack configuration section is missing - Slack features will be disabled");
            return;
        }

        if (slack.EnableInteractiveBot || !string.IsNullOrWhiteSpace(slack.WebhookUrl))
        {
            if (slack.EnableInteractiveBot)
            {
                if (string.IsNullOrWhiteSpace(slack.ApiToken))
                {
                    errors.Add("Slack.ApiToken is required when EnableInteractiveBot is true");
                }
            }

            if (!string.IsNullOrWhiteSpace(slack.WebhookUrl))
            {
                if (!Uri.TryCreate(slack.WebhookUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "https" && uri.Scheme != "http"))
                {
                    errors.Add("Slack.WebhookUrl must be a valid HTTP/HTTPS URL");
                }
            }

            // Validate API base URL
            if (!string.IsNullOrWhiteSpace(slack.ApiBaseUrl))
            {
                if (!Uri.TryCreate(slack.ApiBaseUrl, UriKind.Absolute, out var apiUri) ||
                    (apiUri.Scheme != "https" && apiUri.Scheme != "http"))
                {
                    errors.Add("Slack.ApiBaseUrl must be a valid HTTP/HTTPS URL");
                }
            }
        }
        else
        {
            warnings.Add("Slack features are disabled - no webhook URL or interactive bot configured");
        }
    }

    private void ValidateTelegram(Telegram telegram, List<string> errors, List<string> warnings)
    {
        if (telegram == null)
        {
            warnings.Add("Telegram configuration section is missing - Telegram features will be disabled");
            return;
        }

        if (telegram.Enabled)
        {
            if (string.IsNullOrWhiteSpace(telegram.Token))
            {
                errors.Add("Telegram.Token is required when Telegram.Enabled is true");
            }
            if (string.IsNullOrWhiteSpace(telegram.ChannelId))
            {
                errors.Add("Telegram.ChannelId is required when Telegram.Enabled is true");
            }
        }
        else
        {
            warnings.Add("Telegram features are disabled");
        }
    }

    private void ValidateGoogleServiceAccount(GoogleServiceAccount googleServiceAccount, List<string> errors, List<string> warnings)
    {
        if (googleServiceAccount == null)
        {
            warnings.Add("GoogleServiceAccount configuration section is missing - Google Calendar features will be disabled");
            return;
        }

        var hasAnyGoogleConfig = !string.IsNullOrWhiteSpace(googleServiceAccount.ProjectId) ||
                                !string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKeyId) ||
                                !string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKey) ||
                                !string.IsNullOrWhiteSpace(googleServiceAccount.ClientEmail) ||
                                !string.IsNullOrWhiteSpace(googleServiceAccount.ClientId);

        if (hasAnyGoogleConfig)
        {
            if (string.IsNullOrWhiteSpace(googleServiceAccount.ProjectId))
            {
                errors.Add("GoogleServiceAccount.ProjectId is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKeyId))
            {
                errors.Add("GoogleServiceAccount.PrivateKeyId is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKey))
            {
                errors.Add("GoogleServiceAccount.PrivateKey is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.ClientEmail))
            {
                errors.Add("GoogleServiceAccount.ClientEmail is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.ClientId))
            {
                errors.Add("GoogleServiceAccount.ClientId is required when Google Calendar is configured");
            }
        }
        else
        {
            warnings.Add("Google Calendar features are disabled - no service account configured");
        }
    }

    private void ValidateFeatures(Features features, List<string> errors, List<string> warnings)
    {
        if (features == null)
        {
            warnings.Add("Features configuration section is missing - using default feature settings");
            return;
        }

        if (features.UseMockData)
        {
            warnings.Add("Mock data mode is enabled - application will use stored historical data");
            if (features.MockCurrentWeek <= 0 || features.MockCurrentWeek > 53)
            {
                errors.Add("Features.MockCurrentWeek must be between 1 and 53 when UseMockData is true");
            }
            if (features.MockCurrentYear < 2020 || features.MockCurrentYear > _timeProvider.CurrentYear + 1)
            {
                errors.Add($"Features.MockCurrentYear must be between 2020 and {_timeProvider.CurrentYear + 1} when UseMockData is true");
            }
        }
    }

    private void ValidateTimers(Timers timers, List<string> errors, List<string> warnings)
    {
        if (timers == null)
        {
            warnings.Add("Timers configuration section is missing - using default timer settings");
            return;
        }

        if (timers.SchedulingIntervalSeconds <= 0)
        {
            errors.Add("Timers.SchedulingIntervalSeconds must be greater than 0");
        }
        if (timers.SlackPollingIntervalSeconds <= 0)
        {
            errors.Add("Timers.SlackPollingIntervalSeconds must be greater than 0");
        }
        else if (timers.SlackPollingIntervalSeconds < 1)
        {
            warnings.Add("Timers.SlackPollingIntervalSeconds is less than 1 second - this may cause excessive API calls");
        }

        if (timers.CleanupIntervalHours <= 0)
        {
            errors.Add("Timers.CleanupIntervalHours must be greater than 0");
        }
        if (timers.AdaptivePolling)
        {
            if (timers.MaxPollingIntervalSeconds <= timers.MinPollingIntervalSeconds)
            {
                errors.Add("Timers.MaxPollingIntervalSeconds must be greater than MinPollingIntervalSeconds when AdaptivePolling is enabled");
            }
        }
    }
}