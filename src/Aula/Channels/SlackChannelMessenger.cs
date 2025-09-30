using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Aula.Configuration;

namespace Aula.Channels;

/// <summary>
/// Slack-specific implementation of IChannelMessenger that sends messages
/// via Slack's Web API without depending on bot implementations.
/// </summary>
public class SlackChannelMessenger : IChannelMessenger
{
	private readonly HttpClient _httpClient;
	private readonly Config _config;
	private readonly ILogger _logger;

	public string PlatformType => "Slack";

	public SlackChannelMessenger(HttpClient httpClient, Config config, ILoggerFactory loggerFactory)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_config = config ?? throw new ArgumentNullException(nameof(config));
		_logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<SlackChannelMessenger>();
	}

	public async Task SendMessageAsync(string message)
	{
		await SendMessageAsync(_config.Slack.ChannelId, message);
	}

	public async Task SendMessageAsync(string channelId, string message)
	{
		try
		{
			_logger.LogInformation("Sending Slack message to channel {ChannelId}: {MessageLength} characters", channelId, message.Length);

			var payload = new
			{
				channel = channelId,
				text = message,
				unfurl_links = false,
				unfurl_media = false
			};

			var json = JsonSerializer.Serialize(payload);
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var apiUrl = $"{_config.Slack.ApiBaseUrl.TrimEnd('/')}/chat.postMessage";
			using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
			{
				Content = content
			};
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Slack.ApiToken);
			request.Headers.Add("User-Agent", "Aula Bot 1.0");

			var response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("Failed to send Slack message: HTTP {StatusCode}", response.StatusCode);
				throw new HttpRequestException($"Slack API returned {response.StatusCode}");
			}

			var responseText = await response.Content.ReadAsStringAsync();

			// Parse Slack API response to check for errors
			try
			{
				var responseJson = JsonSerializer.Deserialize<JsonElement>(responseText);
				if (responseJson.TryGetProperty("ok", out var okElement) && okElement.GetBoolean())
				{
					_logger.LogInformation("Slack message sent successfully");
				}
				else
				{
					var error = responseJson.TryGetProperty("error", out var errorElement)
						? errorElement.GetString()
						: "Unknown error";
					_logger.LogError("Slack API returned error: {Error}", error);
					throw new InvalidOperationException($"Slack API error: {error}");
				}
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to parse Slack API response: {Response}", responseText);
				throw new InvalidOperationException("Invalid JSON response from Slack API", ex);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error sending Slack message to channel {ChannelId}", channelId);
			throw;
		}
	}
}
