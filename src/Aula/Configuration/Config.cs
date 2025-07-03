namespace Aula.Configuration;

public class Config : IConfig
{
    public UniLogin UniLogin { get; set; } = new();
    public MinUddannelse MinUddannelse { get; set; } = new();
    public Slack Slack { get; set; } = new();
    public GoogleServiceAccount GoogleServiceAccount { get; set; } = new();
    public Telegram Telegram { get; set; } = new();
    public OpenAi OpenAi { get; set; } = new();
    public Supabase Supabase { get; set; } = new();
    public Features Features { get; set; } = new();
    public Timers Timers { get; set; } = new();
}