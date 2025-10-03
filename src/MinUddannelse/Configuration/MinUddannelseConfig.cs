namespace MinUddannelse.Configuration;

public class MinUddannelseConfig
{
    public string ApiBaseUrl { get; set; } = "https://www.minuddannelse.net";
    public string LoginPath { get; set; } = "/KmdIdentity/Login";
    public string WeekLettersPath { get; set; } = "/api/stamdata/ugeplan/getUgeBreve";
    public string StudentDataPath { get; set; } = "/api/stamdata/elev/getElev";
    public string SamlLoginUrl { get; set; } = "https://www.minuddannelse.net/KmdIdentity/Login?domainHint=unilogin-idp-prod&toFa=False";

    public List<Child> Children { get; set; } = new();
}
