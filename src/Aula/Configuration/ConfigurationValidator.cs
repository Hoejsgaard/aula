using Microsoft.Extensions.Logging;

namespace Aula.Configuration;

public class ConfigurationValidator : IConfigurationValidator
{
    private readonly ILogger _logger;

    public ConfigurationValidator(ILoggerFactory loggerFactory)
    {
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(nameof(ConfigurationValidator));
    }

    public void ValidateConfiguration(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        ValidateMinUddannelse(config.MinUddannelse);
        ValidateUniLogin(config.UniLogin);
        ValidateOpenAi(config.OpenAi);
        ValidateSupabase(config.Supabase);

        // Optional features - validate if enabled
        ValidateSlack(config.Slack);
        ValidateTelegram(config.Telegram);
        ValidateGoogleServiceAccount(config.GoogleServiceAccount);
        ValidateFeatures(config.Features);
        ValidateTimers(config.Timers);

        _logger.LogInformation("Configuration validation completed successfully");
    }

    private void ValidateMinUddannelse(MinUddannelse minUddannelse)
    {
        if (minUddannelse == null)
        {
            throw new InvalidOperationException("MinUddannelse configuration section is required");
        }

        if (minUddannelse.Children == null || !minUddannelse.Children.Any())
        {
            throw new InvalidOperationException("At least one child must be configured in MinUddannelse.Children section");
        }

        foreach (var child in minUddannelse.Children)
        {
            if (string.IsNullOrWhiteSpace(child.FirstName))
            {
                throw new InvalidOperationException("Child FirstName is required");
            }
            if (string.IsNullOrWhiteSpace(child.LastName))
            {
                throw new InvalidOperationException("Child LastName is required");
            }
        }
    }

    private void ValidateUniLogin(UniLogin uniLogin)
    {
        if (uniLogin == null)
        {
            throw new InvalidOperationException("UniLogin configuration section is required");
        }

        if (string.IsNullOrWhiteSpace(uniLogin.Username))
        {
            throw new InvalidOperationException("UniLogin.Username is required");
        }

        if (string.IsNullOrWhiteSpace(uniLogin.Password))
        {
            throw new InvalidOperationException("UniLogin.Password is required");
        }
    }

    private void ValidateOpenAi(OpenAi openAi)
    {
        if (openAi == null)
        {
            throw new InvalidOperationException("OpenAi configuration section is required");
        }

        if (string.IsNullOrWhiteSpace(openAi.ApiKey))
        {
            throw new InvalidOperationException("OpenAi.ApiKey is required");
        }

        if (string.IsNullOrWhiteSpace(openAi.Model))
        {
            throw new InvalidOperationException("OpenAi.Model is required");
        }
    }

    private void ValidateSupabase(Supabase supabase)
    {
        if (supabase == null)
        {
            throw new InvalidOperationException("Supabase configuration section is required");
        }

        if (string.IsNullOrWhiteSpace(supabase.Url))
        {
            throw new InvalidOperationException("Supabase.Url is required");
        }

        if (string.IsNullOrWhiteSpace(supabase.Key))
        {
            throw new InvalidOperationException("Supabase.Key is required");
        }
    }

    private void ValidateSlack(Slack slack)
    {
        if (slack == null)
        {
            _logger.LogWarning("Slack configuration section is missing - Slack features will be disabled");
            return;
        }

        if (slack.EnableInteractiveBot || !string.IsNullOrWhiteSpace(slack.WebhookUrl))
        {
            if (slack.EnableInteractiveBot)
            {
                if (string.IsNullOrWhiteSpace(slack.ApiToken))
                {
                    throw new InvalidOperationException("Slack.ApiToken is required when EnableInteractiveBot is true");
                }
            }

            if (!string.IsNullOrWhiteSpace(slack.WebhookUrl))
            {
                if (!Uri.TryCreate(slack.WebhookUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "https" && uri.Scheme != "http"))
                {
                    throw new InvalidOperationException("Slack.WebhookUrl must be a valid HTTP/HTTPS URL");
                }
            }
        }
        else
        {
            _logger.LogInformation("Slack features are disabled - no webhook URL or interactive bot configured");
        }
    }

    private void ValidateTelegram(Telegram telegram)
    {
        if (telegram == null)
        {
            _logger.LogWarning("Telegram configuration section is missing - Telegram features will be disabled");
            return;
        }

        if (telegram.Enabled)
        {
            if (string.IsNullOrWhiteSpace(telegram.Token))
            {
                throw new InvalidOperationException("Telegram.Token is required when Telegram.Enabled is true");
            }
            if (string.IsNullOrWhiteSpace(telegram.ChannelId))
            {
                throw new InvalidOperationException("Telegram.ChannelId is required when Telegram.Enabled is true");
            }
        }
        else
        {
            _logger.LogInformation("Telegram features are disabled");
        }
    }

    private void ValidateGoogleServiceAccount(GoogleServiceAccount googleServiceAccount)
    {
        if (googleServiceAccount == null)
        {
            _logger.LogWarning("GoogleServiceAccount configuration section is missing - Google Calendar features will be disabled");
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
                throw new InvalidOperationException("GoogleServiceAccount.ProjectId is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKeyId))
            {
                throw new InvalidOperationException("GoogleServiceAccount.PrivateKeyId is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.PrivateKey))
            {
                throw new InvalidOperationException("GoogleServiceAccount.PrivateKey is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.ClientEmail))
            {
                throw new InvalidOperationException("GoogleServiceAccount.ClientEmail is required when Google Calendar is configured");
            }
            if (string.IsNullOrWhiteSpace(googleServiceAccount.ClientId))
            {
                throw new InvalidOperationException("GoogleServiceAccount.ClientId is required when Google Calendar is configured");
            }
        }
        else
        {
            _logger.LogInformation("Google Calendar features are disabled - no service account configured");
        }
    }

    private void ValidateFeatures(Features features)
    {
        if (features == null)
        {
            _logger.LogWarning("Features configuration section is missing - using default feature settings");
            return;
        }

        if (features.UseMockData)
        {
            _logger.LogInformation("Mock data mode is enabled - application will use stored historical data");
            if (features.MockCurrentWeek <= 0 || features.MockCurrentWeek > 53)
            {
                throw new InvalidOperationException("Features.MockCurrentWeek must be between 1 and 53 when UseMockData is true");
            }
            if (features.MockCurrentYear < 2020 || features.MockCurrentYear > DateTime.Now.Year + 1)
            {
                throw new InvalidOperationException($"Features.MockCurrentYear must be between 2020 and {DateTime.Now.Year + 1} when UseMockData is true");
            }
        }
    }

    private void ValidateTimers(Timers timers)
    {
        if (timers == null)
        {
            _logger.LogWarning("Timers configuration section is missing - using default timer settings");
            return;
        }

        if (timers.SchedulingIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Timers.SchedulingIntervalSeconds must be greater than 0");
        }
        if (timers.SlackPollingIntervalSeconds <= 0)
        {
            throw new InvalidOperationException("Timers.SlackPollingIntervalSeconds must be greater than 0");
        }
        if (timers.CleanupIntervalHours <= 0)
        {
            throw new InvalidOperationException("Timers.CleanupIntervalHours must be greater than 0");
        }

        if (timers.SlackPollingIntervalSeconds < 1)
        {
            _logger.LogWarning("Timers.SlackPollingIntervalSeconds is less than 1 second - this may cause excessive API calls");
        }
        if (timers.AdaptivePolling)
        {
            if (timers.MaxPollingIntervalSeconds <= timers.MinPollingIntervalSeconds)
            {
                throw new InvalidOperationException("Timers.MaxPollingIntervalSeconds must be greater than MinPollingIntervalSeconds when AdaptivePolling is enabled");
            }
        }
    }
}