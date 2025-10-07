namespace MinUddannelse.Configuration;

public enum AuthenticationType
{
    Standard,
    Pictogram
}

public class UniLogin
{
    public string Username { get; set; } = string.Empty;
    public AuthenticationType AuthType { get; set; } = AuthenticationType.Standard;
    public string Password { get; set; } = string.Empty;
    public string[]? PictogramSequence { get; set; }
}
