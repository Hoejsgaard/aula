using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Html2Markdown;

namespace Aula
{
 public class Html2SlackMarkdownConverter
 {
	 private readonly Converter _converter;

	 public Html2SlackMarkdownConverter()
	 {
		 _converter = new Converter();
	 }
	 public string Convert(string? html)
	 {
		 return _converter.Convert(html ?? "").Replace("**", "*");
  }
 }
}
