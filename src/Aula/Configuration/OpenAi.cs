namespace Aula.Configuration;

public class OpenAi
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4"; // Default to GPT-4, can be overridden in config
}