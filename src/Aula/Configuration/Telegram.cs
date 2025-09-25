namespace Aula.Configuration;

public class Telegram
{
    public bool Enabled { get; set; } = false;
    public string BotName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public bool EnableInteractiveBot { get; set; } = true;
}