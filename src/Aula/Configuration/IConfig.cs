namespace Aula.Configuration;

public interface IConfig
{
    UniLogin UniLogin { get; init; }
    MinUddannelse MinUddannelse { get; init; }
    GoogleServiceAccount GoogleServiceAccount { get; init; }
    OpenAi OpenAi { get; init; }
    Supabase Supabase { get; init; }
    Scheduling Scheduling { get; init; }
    WeekLetter WeekLetter { get; init; }
}
