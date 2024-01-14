namespace Aula;

public interface IConfig
{
	UniLogin UniLogin { get; set; }
	Slack Slack { get; set; }
	List<Child> Children { get; set; }
	GoogleServiceAccount GoogleServiceAccount { get; set; }
	Telegram Telegram { get; set; }
}

public class Config : IConfig
{
	public UniLogin UniLogin { get; set; } = new UniLogin();
	public Slack Slack { get; set; } = new Slack();
	public List<Child> Children { get; set; } = new List<Child>();
	public GoogleServiceAccount GoogleServiceAccount { get; set; } = new GoogleServiceAccount();
	public Telegram Telegram { get; set; } = new Telegram();
}


public class UniLogin
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
	public string GoogleCalendarId { get; set; } = string.Empty;
	
}


public class GoogleServiceAccount
{
	public string Type { get; set;} = string.Empty;
	public string ProjectId { get; set;} = string.Empty;
	public string PrivateKeyId { get; set;} = string.Empty;
	public string PrivateKey { get; set;} = string.Empty;
	public string ClientEmail { get; set;} = string.Empty;
	public string ClientId { get; set;} = string.Empty;
	public string AuthUri { get; set;} = string.Empty;
	public string TokenUri { get; set;} = string.Empty;
	public string AuthProviderX509CertUrl { get; set;} = string.Empty;
	public string ClientX509CertUrl { get; set;} = string.Empty;
	public string UniverseDomain { get; set;} = string.Empty;
}

public class Telegram
{
	public string BotName { get; set; } = string.Empty;
	public string Token { get; set; } = string.Empty;
	public string ChannelId { get; set; } = string.Empty;

}