using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Aula;

public class TelegramClient
{
	private readonly Html2SlackMarkdownConverter _markdownConverter;
	private readonly ITelegramBotClient _telegram;

	public TelegramClient(string token)
	{
		_telegram = new TelegramBotClient(token);
		_markdownConverter = new Html2SlackMarkdownConverter();
	}

	public async Task<bool> SendMessageToChannel(string channelId, string message)
	{
		try
		{
			await _telegram.SendTextMessageAsync(
				channelId,
				message,
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
	
	public async Task<bool> PostWeekLetter(string channelId, JObject weekLetter, Child child)
	{
		var @class = weekLetter["ugebreve"]?[0]?["klasseNavn"]?.ToString() ?? "";
		var week = weekLetter["ugebreve"]?[0]?["uge"]?.ToString() ?? "";
		var letterText = _markdownConverter.Convert(weekLetter["ugebreve"]?[0]?["indhold"]?.ToString() ?? "")
			.Replace("**", "*");
		var message = string.Format(@$"*Ugebrev for {child.FirstName} ({@class}) uge {week}*
{letterText}
			");

		return await SendMessageToChannel(channelId, message);
	}
}