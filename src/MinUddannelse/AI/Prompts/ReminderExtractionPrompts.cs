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

Extract ONLY actionable events that require parent/student preparation from this Danish school week letter for week {weekNumber}/{year}.

Current context:
- Today: {currentTime:yyyy-MM-dd} ({currentTime:dddd})
- Week {weekNumber} dates:
  * Mandag: {currentMonday:yyyy-MM-dd}
  * Tirsdag: {currentMonday.AddDays(1):yyyy-MM-dd}
  * Onsdag: {currentMonday.AddDays(2):yyyy-MM-dd}
  * Torsdag: {currentMonday.AddDays(3):yyyy-MM-dd}
  * Fredag: {currentMonday.AddDays(4):yyyy-MM-dd}

Week Letter: ""{weekLetterContent}""

CRITICAL FILTERING RULES - Only create reminders for events that require ACTION:

✅ INCLUDE (require parent/student preparation):
- Tests, exams, or assessments (staveprøve, matematik test, etc.)
- Photo sessions (skolefoto, klassefoto)
- Permission forms or deadlines (tilmeldingsblanket, betaling)
- Required supplies or materials (medbring computer, sportsudstyr)
- Field trips and excursions (udflugter, ekskursioner, museumsbesøg)
- Sports events and competitions (idrætsdag, sportsstævne, løb)
- Special events requiring presence (skoleforestilling, koncert)
- Parent meetings or conferences (forældremøde)

❌ EXCLUDE (regular classroom activities - NO reminders needed):
- Normal curriculum work (læse bøger, matematik opgaver)
- Regular classroom projects (billedkunst, dansk projekter)
- General teaching activities (fortsætte med emne X)
- Routine subjects (historie, kristendom, regular idræt/PE lessons)
- Books being finished/started (afslutning af bog X, start på bog Y)
- Regular sports activities (fodboldforløb, svømning generelt)

IMPORTANT:
- When you see Danish day names like ""Torsdag"", map them to the exact dates above
- Generate comprehensive reminder descriptions in DANISH
- Include event name, time, and any preparation details in the description
- Extract full context and specifics from the week letter
- Make descriptions rich and informative
- DO NOT use ""Husk"" or ""i dag"" in descriptions - just the activity details
- Include times when mentioned (use ""kl."" format)
- Add preparation details when mentioned in the letter

For description examples:
- ""Motionsdag kl. 8.00 - husk turtaske med 10-bid, drikkedunk, madpakke og tøj der passer til vejret""
- ""Staveprøve kl. 12.45 - medbring opladt computer og hovedtelefoner""
- ""Fotograf fra 10.35-11.15 - alle børn skal være til stede""
- ""Skolefoto kl. 9.00 - børn skal være pænt påklædt""
- ""Udflugter til Experimentarium kl. 9.30 - medbring madpakke og drikkedunk""

CRITICAL:
- The description should be a complete, standalone reminder text
- Include all relevant details from the week letter
- Use natural Danish phrasing
- Focus on what parents/students need to know and prepare

Return a JSON array of events. If no events found, return: []

JSON format:
[
  {{
    ""type"": ""deadline"",
    ""title"": ""Kort dansk titel"",
    ""description"": ""Husk [actionable reminder text in Danish]"",
    ""date"": ""yyyy-MM-dd"",
    ""confidence"": 0.8
  }}
]

Event types: deadline, permission_form, event, supply_needed
Only include events with confidence >= 0.8 (high confidence only for actionable items).
Response must be valid JSON only.";
    }
}
