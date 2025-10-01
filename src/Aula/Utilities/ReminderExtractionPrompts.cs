namespace Aula.Utilities;

public static class ReminderExtractionPrompts
{
    public static string GetExtractionPrompt(string query, DateTime currentTime)
    {
        return $@"Extract reminder details from this natural language request:

Query: ""{query}""

Extract:
1. Description: What to remind about
2. DateTime: When to remind (convert to yyyy-MM-dd HH:mm format)
3. ChildName: If mentioned, the child's name (optional)

For relative dates (current time is {currentTime:yyyy-MM-dd HH:mm}):
- ""tomorrow"" = {currentTime.Date.AddDays(1):yyyy-MM-dd}
- ""today"" = {currentTime.Date:yyyy-MM-dd}
- ""next Monday"" = calculate the next Monday
- ""in 2 hours"" = {currentTime.AddHours(2):yyyy-MM-dd HH:mm}
- ""om 2 minutter"" = {currentTime.AddMinutes(2):yyyy-MM-dd HH:mm}
- ""om 30 minutter"" = {currentTime.AddMinutes(30):yyyy-MM-dd HH:mm}

Respond in this exact format:
DESCRIPTION: [extracted description]
DATETIME: [yyyy-MM-dd HH:mm]
CHILD: [child name or NONE]";
    }
}
