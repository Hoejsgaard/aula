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

		if (minUddannelse.Children == null || minUddannelse.Children.Count == 0)
		{
			errors.Add("At least one child must be configured in MinUddannelse.Children section");
			return;
		}

		var hasAtLeastOneValidChild = false;
		foreach (var child in minUddannelse.Children)
		{
			if (string.IsNullOrWhiteSpace(child.FirstName))
			{
				errors.Add($"Child FirstName is required");
			}
			if (string.IsNullOrWhiteSpace(child.LastName))
			{
				errors.Add($"Child LastName is required for {child.FirstName}");
			}

			// Check per-child UniLogin credentials
			if (child.UniLogin != null &&
				!string.IsNullOrWhiteSpace(child.UniLogin.Username) &&
				!string.IsNullOrWhiteSpace(child.UniLogin.Password))
			{
				hasAtLeastOneValidChild = true;
			}
		}

		if (!hasAtLeastOneValidChild)
		{
			errors.Add("At least one child must have valid UniLogin credentials configured");
		}
	}

	private void ValidateUniLogin(UniLogin uniLogin, List<string> errors)
	{
		// Per-child authentication: root UniLogin is no longer required
		// Keeping this method for backward compatibility but it doesn't validate anything
		// The actual validation happens in ValidateMinUddannelse for per-child credentials
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
			// Allow reasonable range: past 5 years to next year
			var minYear = _timeProvider.CurrentYear - 5;
			var maxYear = _timeProvider.CurrentYear + 1;
			if (features.MockCurrentYear < minYear || features.MockCurrentYear > maxYear)
			{
				errors.Add($"Features.MockCurrentYear must be between {minYear} and {maxYear} when UseMockData is true");
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
