using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Text;
using Aula.AI.Services;

namespace Aula.AI.Prompts;

public class PromptBuilder : IPromptBuilder
{
    public ChatMessage CreateSystemInstructionsMessage(string childName, ChatInterface chatInterface)
    {
        var currentDateTime = DateTime.UtcNow;
        return ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
                                    $"This letter is specifically about {childName}'s class and activities. " +
                                    $"Today is {currentDateTime.DayOfWeek}, {currentDateTime.ToString("MMMM d, yyyy")}. " +
                                    "Answer based on the content of the letter. If the specific information isn't in the letter, " +
                                    "say 'I don't have that specific information in the weekly letter' and then provide any related " +
                                    "information that might be helpful. For example, if asked about Tuesday's activities but only " +
                                    "Monday's activities are mentioned, acknowledge that Tuesday isn't mentioned but share what's " +
                                    "happening on Monday. Be concise and direct in your answers. " +
                                    "CRITICAL: You can ONLY answer questions about the weekly letter content. You CANNOT create reminders, " +
                                    "set alarms, or take any actions. Never promise to remind users of anything. If asked to create " +
                                    "a reminder, explain they need to use specific reminder commands instead. " +
                                    "IMPORTANT: Always respond in the same language as the user's question. " +
                                    "If the question is in Danish, respond in Danish. If the question is in English, respond in English. " +
                                    $"IMPORTANT: When someone asks about a different child (not {childName}), respond with a simple: " +
                                    $"'Sorry, I don't have any data regarding a child named [name]' (or 'Beklager, jeg har ingen data om et barn ved navn [navn]' in Danish). " +
                                    $"Do not mention {childName} or provide any details when asked about other children. " +
                                    $"IMPORTANT: When answering general questions (like 'what happens on Thursday'), ALWAYS include {childName}'s name in your response " +
                                    $"to make it clear this information is specifically about {childName}. " +
                                    $"You are responding via {GetChatInterfaceInstructions(chatInterface)}");
    }

    public ChatMessage CreateWeekLetterContentMessage(string childName, string weekLetterContent)
    {
        return ChatMessage.FromSystem($"Here's the weekly letter content for {childName}'s class:\n\n{weekLetterContent}");
    }

    public List<ChatMessage> CreateSummarizationMessages(string childName, string className, string weekNumber, string weekLetterContent, ChatInterface chatInterface)
    {
        return new List<ChatMessage>
        {
            ChatMessage.FromSystem($"You are a helpful assistant that summarizes weekly school letters for parents. " +
                                  "Provide a brief summary of the key information in the letter, focusing on activities, " +
                                  "important dates, and things parents need to know. Be concise but thorough. " +
                                  $"You are responding via {GetChatInterfaceInstructions(chatInterface)}"),
            ChatMessage.FromUser($"Here's the week letter for {className} for week {weekNumber}:\n\n{weekLetterContent}\n\nPlease summarize this week letter.")
        };
    }

    public List<ChatMessage> CreateKeyInformationExtractionMessages(string childName, string className, string weekNumber, string weekLetterContent)
    {
        return new List<ChatMessage>
        {
            ChatMessage.FromSystem("You are a helpful assistant that extracts structured information from weekly school letters. " +
                                  "Extract information about activities for each day of the week, homework, and important dates. " +
                                  "Respond in valid JSON format with the following structure: " +
                                  "{ \"monday\": \"activities\", \"tuesday\": \"activities\", \"wednesday\": \"activities\", " +
                                  "\"thursday\": \"activities\", \"friday\": \"activities\", \"homework\": \"homework details\", " +
                                  "\"important_dates\": [\"date1\", \"date2\"] }"),
            ChatMessage.FromUser($"Here's the week letter for {childName} in class {className} for week {weekNumber}:\n\n{weekLetterContent}\n\nPlease extract the key information in JSON format.")
        };
    }

    public List<ChatMessage> CreateMultiChildMessages(Dictionary<string, string> childrenContent, ChatInterface chatInterface)
    {
        var combinedContent = new StringBuilder();
        combinedContent.AppendLine("Week letters for children:");
        combinedContent.AppendLine();

        foreach (var (childName, weekLetterContent) in childrenContent)
        {
            combinedContent.AppendLine($"=== {childName} ===");
            combinedContent.AppendLine(weekLetterContent);
            combinedContent.AppendLine();
        }

        var systemPrompt = $@"You are an AI assistant helping parents understand their children's school activities. 
You have been provided with week letters from school for multiple children. 
Answer questions about the children's activities clearly and helpfully.

CRITICAL: You can ONLY answer questions about the weekly letter content. You CANNOT create reminders, 
set alarms, or take any actions. Never promise to remind users of anything. If asked to create 
a reminder, explain they need to use specific reminder commands instead.

{GetChatInterfaceInstructions(chatInterface)}

When answering about multiple children, clearly indicate which child each piece of information relates to.
If a question is about a specific child, focus on that child but you can mention relevant information about other children if helpful.
If a question could apply to multiple children, provide information for all relevant children.

Current day context: Today is {DateTime.UtcNow.ToString("dddd, MMMM dd, yyyy")}";

        return new List<ChatMessage>
        {
            ChatMessage.FromSystem(systemPrompt),
            ChatMessage.FromUser($"Here are the week letters:\n\n{combinedContent}")
        };
    }

    public string GetChatInterfaceInstructions(ChatInterface chatInterface)
    {
        return chatInterface switch
        {
            ChatInterface.Slack => "Slack. Format your responses using Slack markdown (e.g., *bold*, _italic_, `code`). Don't use HTML tags.",
            ChatInterface.Telegram => "Telegram. Format your responses using HTML tags (e.g., <b>bold</b>, <i>italic</i>, <code>code</code>). Telegram supports these HTML tags: <b>, <i>, <u>, <s>, <a>, <code>, <pre>. Use <br/> for line breaks.",
            _ => "a chat interface. Use plain text formatting."
        };
    }
}
