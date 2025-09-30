namespace Aula.Configuration;

public class OpenAi
{
	public string ApiKey { get; set; } = string.Empty;
	public string Model { get; set; } = "gpt-4";
	public int MaxTokens { get; set; } = 2000;
	public double Temperature { get; set; } = 0.7;
	public int CacheExpirationMinutes { get; set; } = 30;
}
