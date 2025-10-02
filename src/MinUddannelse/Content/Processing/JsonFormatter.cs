using Newtonsoft.Json;

namespace MinUddannelse.Content.Processing;

public static class JsonFormatter
{
    public static string Prettify(string json)
    {
        return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
    }
}
