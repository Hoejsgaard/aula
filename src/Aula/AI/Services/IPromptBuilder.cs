using OpenAI.ObjectModels.RequestModels;
using System.Collections.Generic;

namespace Aula.AI.Services;

public interface IPromptBuilder
{
    ChatMessage CreateSystemInstructionsMessage(string childName, ChatInterface chatInterface);
    ChatMessage CreateWeekLetterContentMessage(string childName, string weekLetterContent);
    List<ChatMessage> CreateSummarizationMessages(string childName, string className, string weekNumber, string weekLetterContent, ChatInterface chatInterface);
    List<ChatMessage> CreateKeyInformationExtractionMessages(string childName, string className, string weekNumber, string weekLetterContent);
    List<ChatMessage> CreateMultiChildMessages(Dictionary<string, string> childrenContent, ChatInterface chatInterface);
    string GetChatInterfaceInstructions(ChatInterface chatInterface);
}
