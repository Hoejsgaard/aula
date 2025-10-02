using Newtonsoft.Json;

namespace Aula.Content.Processing;

public static class JsonFormatter
{
    public static string Prettify(string json)
    {
        return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
    }
}
