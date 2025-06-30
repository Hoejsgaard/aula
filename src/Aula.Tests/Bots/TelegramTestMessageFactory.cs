using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Aula.Tests.Bots;

public static class TelegramTestMessageFactory
{
    public static Message CreateTextMessage(
        long chatId = 123456789L,
        string? text = "Test message",
        ChatType chatType = ChatType.Private,
        int messageId = 1,
        string firstName = "Test",
        string? username = "testuser")
    {
        // Create JSON representation and deserialize using Newtonsoft.Json (same as Telegram.Bot)
        var messageJson = $$"""
        {
            "message_id": {{messageId}},
            "date": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
            "chat": {
                "id": {{chatId}},
                "type": "{{chatType.ToString().ToLower()}}"
            },
            "from": {
                "id": 123,
                "is_bot": false,
                "first_name": "{{firstName}}"{{(string.IsNullOrEmpty(username) ? "" : $@", ""username"": ""{username}""")}}
            }{{(text == null ? "" : $@", ""text"": ""{text}""")}}
        }
        """;

        return JsonConvert.DeserializeObject<Message>(messageJson)!;
    }

    public static Message CreateNonTextMessage(
        long chatId = 123456789L,
        MessageType messageType = MessageType.Photo,
        ChatType chatType = ChatType.Private,
        int messageId = 1)
    {
        var messageJson = $$"""
        {
            "message_id": {{messageId}},
            "date": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
            "chat": {
                "id": {{chatId}},
                "type": "{{chatType.ToString().ToLower()}}"
            },
            "from": {
                "id": 123,
                "is_bot": false,
                "first_name": "Test",
                "username": "testuser"
            },
            "photo": [{"file_id": "test", "file_unique_id": "test", "width": 100, "height": 100, "file_size": 1000}]
        }
        """;

        return JsonConvert.DeserializeObject<Message>(messageJson)!;
    }
}