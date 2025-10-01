namespace Aula.Configuration;

public interface IConfig
{
    UniLogin UniLogin { get; init; }
    MinUddannelse MinUddannelse { get; init; }
    Slack Slack { get; init; }
    GoogleServiceAccount GoogleServiceAccount { get; init; }
    OpenAi OpenAi { get; init; }
    Supabase Supabase { get; init; }
    Features Features { get; init; }
    Timers Timers { get; init; }
}
