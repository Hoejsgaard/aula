namespace Aula.Configuration;

public interface IConfig
{
    UniLogin UniLogin { get; set; }
    MinUddannelse MinUddannelse { get; set; }
    Slack Slack { get; set; }
    GoogleServiceAccount GoogleServiceAccount { get; set; }
    Telegram Telegram { get; set; }
    OpenAi OpenAi { get; set; }
    Supabase Supabase { get; set; }
    Features Features { get; set; }
    Timers Timers { get; set; }
}