namespace Aula.Configuration;

public class Config : IConfig
{
	public UniLogin UniLogin { get; init; } = new(); // Keep for backward compatibility, but not used
	public MinUddannelse MinUddannelse { get; init; } = new();
	public Slack Slack { get; init; } = new();
	public GoogleServiceAccount GoogleServiceAccount { get; init; } = new();
	public Telegram Telegram { get; init; } = new();
	public OpenAi OpenAi { get; init; } = new();
	public Supabase Supabase { get; init; } = new();
	public Features Features { get; init; } = new();
	public Timers Timers { get; init; } = new();
}
