namespace Aula.Configuration;

public enum AuthenticationType
{
	Standard,	// Traditional alphanumeric password
	Pictogram	// Image-based authentication
}

public class UniLogin
{
	public string Username { get; set; } = string.Empty;
	public AuthenticationType AuthType { get; set; } = AuthenticationType.Standard;
	public string Password { get; set; } = string.Empty;  // For standard auth
	public string[]? PictogramSequence { get; set; }  // For pictogram auth (e.g., ["image1", "image2", "image3", "image4"])
}
