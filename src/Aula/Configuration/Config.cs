namespace Aula.Configuration;

public class Config : IConfig
{
    public UniLogin UniLogin { get; init; } = new(); // Keep for backward compatibility, but not used
    public MinUddannelseConfig MinUddannelse { get; init; } = new();
    public GoogleServiceAccount GoogleServiceAccount { get; init; } = new();
    public OpenAi OpenAi { get; init; } = new();
    public Supabase Supabase { get; init; } = new();
    public Scheduling Scheduling { get; init; } = new();
    public WeekLetter WeekLetter { get; init; } = new();
}
