namespace Aula.Configuration;

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