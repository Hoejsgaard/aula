using Newtonsoft.Json.Linq;

namespace Aula.Integration;

public class AulaClient : UniLoginClient
{
    private const string AulaApi = "https://www.aula.dk/api/v17/";

    public AulaClient(string username, string password) : base(username, password,
        "https://www.aula.dk/auth/login.php?type=unilogin", "https://www.aula.dk/portal/")
    {
    }

    public async Task<JObject> GetProfile()
    {
        var response = await HttpClient.GetAsync(AulaApi + "?method=profiles.getProfilesByLogin");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JObject.Parse(json);
    }

    public async Task<JObject> GetProfileContext()
    {
        var response = await HttpClient.GetAsync(AulaApi + "?method=profiles.getProfileContext");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JObject.Parse(json);
    }
}
