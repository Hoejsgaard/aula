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
        // Use reflection to create a Message object since properties are read-only
        var message = Activator.CreateInstance(typeof(Message), true) as Message;
        
        // Set the Type property using reflection
        var typeProperty = typeof(Message).GetProperty(nameof(Message.Type));
        typeProperty?.SetValue(message, MessageType.Text);
        
        // Set the Text property using reflection
        var textProperty = typeof(Message).GetProperty(nameof(Message.Text));
        textProperty?.SetValue(message, text);
        
        // Set the MessageId property using reflection
        var messageIdProperty = typeof(Message).GetProperty(nameof(Message.MessageId));
        messageIdProperty?.SetValue(message, messageId);
        
        // Create a Chat object
        var chat = Activator.CreateInstance(typeof(Chat), true) as Chat;
        var chatIdProperty = typeof(Chat).GetProperty(nameof(Chat.Id));
        chatIdProperty?.SetValue(chat, chatId);
        var chatTypeProperty = typeof(Chat).GetProperty(nameof(Chat.Type));
        chatTypeProperty?.SetValue(chat, chatType);
        
        // Set the Chat property using reflection
        var chatProperty = typeof(Message).GetProperty(nameof(Message.Chat));
        chatProperty?.SetValue(message, chat);
        
        // Create a User object
        var user = Activator.CreateInstance(typeof(User), true) as User;
        var userIdProperty = typeof(User).GetProperty(nameof(User.Id));
        userIdProperty?.SetValue(user, 123L);
        var firstNameProperty = typeof(User).GetProperty(nameof(User.FirstName));
        firstNameProperty?.SetValue(user, firstName);
        var usernameProperty = typeof(User).GetProperty(nameof(User.Username));
        usernameProperty?.SetValue(user, username);
        
        // Set the From property using reflection
        var fromProperty = typeof(Message).GetProperty(nameof(Message.From));
        fromProperty?.SetValue(message, user);
        
        return message!;
    }
    
    public static Message CreateNonTextMessage(
        long chatId = 123456789L,
        MessageType messageType = MessageType.Photo,
        ChatType chatType = ChatType.Private,
        int messageId = 1)
    {
        var message = Activator.CreateInstance(typeof(Message), true) as Message;
        
        var typeProperty = typeof(Message).GetProperty(nameof(Message.Type));
        typeProperty?.SetValue(message, messageType);
        
        var messageIdProperty = typeof(Message).GetProperty(nameof(Message.MessageId));
        messageIdProperty?.SetValue(message, messageId);
        
        var chat = Activator.CreateInstance(typeof(Chat), true) as Chat;
        var chatIdProperty = typeof(Chat).GetProperty(nameof(Chat.Id));
        chatIdProperty?.SetValue(chat, chatId);
        var chatTypeProperty = typeof(Chat).GetProperty(nameof(Chat.Type));
        chatTypeProperty?.SetValue(chat, chatType);
        
        var chatProperty = typeof(Message).GetProperty(nameof(Message.Chat));
        chatProperty?.SetValue(message, chat);
        
        var user = Activator.CreateInstance(typeof(User), true) as User;
        var userIdProperty = typeof(User).GetProperty(nameof(User.Id));
        userIdProperty?.SetValue(user, 123L);
        var firstNameProperty = typeof(User).GetProperty(nameof(User.FirstName));
        firstNameProperty?.SetValue(user, "Test");
        var usernameProperty = typeof(User).GetProperty(nameof(User.Username));
        usernameProperty?.SetValue(user, "testuser");
        
        var fromProperty = typeof(Message).GetProperty(nameof(Message.From));
        fromProperty?.SetValue(message, user);
        
        return message!;
    }
}