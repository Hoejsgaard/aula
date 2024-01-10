namespace Aula;

public class Config
{
	public AulaCredentials AulaCredentials { get; set; } = new AulaCredentials();
	public Slack Slack { get; set; } = new Slack();
	public List<Child> Children { get; set; } = new List<Child>();
}


public class AulaCredentials
{
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
}

public class Slack
{
	public string WebhookUrl { get; set; } = string.Empty;
}

public class Child
{
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public string Colour { get; set; } = string.Empty;
}