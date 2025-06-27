namespace Aula;

public interface IConfig
{
    UniLogin UniLogin { get; set; }
    Slack Slack { get; set; }
    List<Child> Children { get; set; }
    GoogleServiceAccount GoogleServiceAccount { get; set; }
    Telegram Telegram { get; set; }
    OpenAi OpenAi { get; set; }
    Supabase Supabase { get; set; }
}

public class Config : IConfig
{
    public UniLogin UniLogin { get; set; } = new();
    public Slack Slack { get; set; } = new();
    public List<Child> Children { get; set; } = new();
    public GoogleServiceAccount GoogleServiceAccount { get; set; } = new();
    public Telegram Telegram { get; set; } = new();
    public OpenAi OpenAi { get; set; } = new();
    public Supabase Supabase { get; set; } = new();
}

public class UniLogin
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class Slack
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public bool EnableInteractiveBot { get; set; } = false;
    public string ChannelId { get; set; } = string.Empty;
    public bool PostWeekLettersOnStartup { get; set; } = true; // Default to true for backward compatibility
}

public class Child
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string GoogleCalendarId { get; set; } = string.Empty;
}

public class GoogleServiceAccount
{
    public string Type { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PrivateKeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string AuthUri { get; set; } = string.Empty;
    public string TokenUri { get; set; } = string.Empty;
    public string AuthProviderX509CertUrl { get; set; } = string.Empty;
    public string ClientX509CertUrl { get; set; } = string.Empty;
    public string UniverseDomain { get; set; } = string.Empty;
}

public class Telegram
{
    public bool Enabled { get; set; } = false;
    public string BotName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public bool PostWeekLettersOnStartup { get; set; } = false; // Default to false
}

public class OpenAi
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4"; // Default to GPT-4, can be overridden in config
}

public class Supabase
{
    public string Url { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
}