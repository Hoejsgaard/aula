using Newtonsoft.Json;

namespace Aula.Utilities;

public static class JsonFormatter
{
    public static string Prettify(string json)
    {
        return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
    }
}