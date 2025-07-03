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
}