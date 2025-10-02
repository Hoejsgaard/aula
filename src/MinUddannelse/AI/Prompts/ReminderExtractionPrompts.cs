namespace MinUddannelse.AI.Prompts;
using System.Globalization;
using MinUddannelse.AI.Prompts;
using MinUddannelse.Models;
using MinUddannelse.Repositories.DTOs;

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

    public static string GetWeekLetterEventExtractionPrompt(string weekLetterContent, DateTime currentTime)
    {
        var weekNumber = ISOWeek.GetWeekOfYear(currentTime);
        var year = currentTime.Year;

        // Calculate the Monday of the current week to provide exact date context
        var currentMonday = currentTime.Date.AddDays(-(int)currentTime.DayOfWeek + (int)DayOfWeek.Monday);

        return $@"You must respond with ONLY valid JSON. No explanations, no markdown, no text outside the JSON.

Extract actionable events from this Danish school week letter for week {weekNumber}/{year}.

Current context:
- Today: {currentTime:yyyy-MM-dd} ({currentTime:dddd})
- Week {weekNumber} dates:
  * Mandag: {currentMonday:yyyy-MM-dd}
  * Tirsdag: {currentMonday.AddDays(1):yyyy-MM-dd}
  * Onsdag: {currentMonday.AddDays(2):yyyy-MM-dd}
  * Torsdag: {currentMonday.AddDays(3):yyyy-MM-dd}
  * Fredag: {currentMonday.AddDays(4):yyyy-MM-dd}

Week Letter: ""{weekLetterContent}""

IMPORTANT: When you see Danish day names like ""Torsdag"", map them to the exact dates above.
For example: ""Torsdag"" in week {weekNumber} = {currentMonday.AddDays(3):yyyy-MM-dd}

Return a JSON array of events. If no events found, return: []

JSON format:
[
  {{
    ""type"": ""deadline"",
    ""title"": ""concise event title"",
    ""description"": ""detailed description"",
    ""date"": ""yyyy-MM-dd"",
    ""confidence"": 0.8
  }}
]

Event types: deadline, permission_form, event, supply_needed
Only include events with confidence >= 0.6.
Response must be valid JSON only.";
    }
}
