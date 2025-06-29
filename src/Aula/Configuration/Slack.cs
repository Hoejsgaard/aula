namespace Aula.Configuration;

public class Slack
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public bool EnableInteractiveBot { get; set; } = false;
    public string ChannelId { get; set; } = string.Empty;
    public bool PostWeekLettersOnStartup { get; set; } = true; // Default to true for backward compatibility
}