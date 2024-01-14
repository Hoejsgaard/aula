using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Aula
{
	public static class JsonFormatter
	{
		public static string Prettify(string json)
		{
			return JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented);
		}
	}
}
