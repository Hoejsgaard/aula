using Newtonsoft.Json;

namespace Aula;

public static class JsonFormatter
{
	public static string Prettify(string json)
	{
		return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
	}
}