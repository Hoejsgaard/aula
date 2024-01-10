using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Slack.Webhooks;
using Telegram.Bot;
using ParseMode = Telegram.Bot.Types.Enums.ParseMode;

namespace Aula
{
	public class TelegramClient
	{
		private readonly ITelegramBotClient _telegram;
		private readonly Html2SlackMarkdownConverter _markdownConverter;

		public TelegramClient(string token)
		{
			_telegram = new TelegramBotClient(token);
			_markdownConverter = new Html2SlackMarkdownConverter();
		}

		//Group id: -4070356654
		public async Task<bool> SendMessageToChannel(string channelId, string message)
		{
			try
			{
				await _telegram.SendTextMessageAsync(
					chatId: channelId,
					text: message,
					parseMode: ParseMode.Markdown
				);
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error sending message: {ex.Message}");
			}
			return false;
		}

		public async Task<bool> SendWeekLetterFancy(string channelId, JObject weekLetter)
		{
			var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
			var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
			var letterText = _markdownConverter.Convert(weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "").Replace("**", "*");
			string message = string.Format(@$"*Ugebrev for {@class} uge {2}*
{letterText}
			");

			return await SendMessageToChannel(channelId, message);
		}
	}
}
