# Task 004: Add AI Screening for Automatic Reminders

**Status:** PENDING
**Priority:** HIGH (Crown Jewel Feature)
**Created:** 2025-01-24

## Vision
Automatically extract actionable items from week letters and create smart reminders, reducing mental load on parents and ensuring nothing important is missed.

## Feature Description
Use OpenAI to analyze week letter content and automatically create reminders for important events, deadlines, and preparations needed for school activities.

## Example Scenarios
1. **"Light backpack with food only"** → Reminder night before: "Pack light backpack with food only for tomorrow"
2. **"Bikes to destination X"** → Morning reminder: "Check bike and helmet for today's trip"
3. **"Special clothing needed"** → Night before: "Prepare special clothing for tomorrow"
4. **"Permission slip due Friday"** → Multiple reminders: Monday, Wednesday, Thursday
5. **"Bring 20 kr for ice cream"** → Morning reminder: "Give child 20 kr for ice cream"

## Implementation Design

### Phase 1: Extraction Engine
1. Create specialized OpenAI prompt for extracting actionable items
2. Define structured output format for detected reminders
3. Implement confidence scoring for extracted items

### Phase 2: Reminder Templates
```csharp
public class ReminderTemplate
{
    public string Pattern { get; set; }
    public string ReminderText { get; set; }
    public TimingStrategy Timing { get; set; }
    public int Priority { get; set; }
}
```

### Phase 3: Smart Timing Algorithm
- Night before (18:00) for preparation items
- Morning of (07:00) for day-of reminders
- Multiple reminders for deadlines
- Configurable lead times based on item type

### Phase 4: Parent Feedback Loop
- Track reminder effectiveness
- Allow parents to adjust timing preferences
- Learn from dismissed vs. useful reminders

## Technical Implementation

### New Classes Needed
- `WeekLetterAnalyzer` - Main analysis service
- `ReminderExtractor` - Extract actionable items from text
- `ReminderScheduler` - Smart scheduling of reminders
- `ReminderTemplate` - Template matching system

### OpenAI Integration
```csharp
var prompt = @"
Analyze this week letter and extract actionable items for parents.
For each item, provide:
1. The action required
2. When it should be done (night before, morning of, specific date)
3. Confidence level (high/medium/low)
4. Category (preparation, payment, permission, transport, clothing)

Week letter content:
{weekLetterContent}

Return as structured JSON.
";
```

### Database Schema
```sql
CREATE TABLE automatic_reminders (
    id SERIAL PRIMARY KEY,
    week_letter_id INT REFERENCES posted_letters(id),
    extracted_text TEXT,
    reminder_text TEXT,
    category VARCHAR(50),
    confidence_level VARCHAR(20),
    remind_date DATE,
    remind_time TIME,
    is_confirmed BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT NOW()
);
```

## Configuration
```json
{
  "AutomaticReminders": {
    "Enabled": true,
    "ConfidenceThreshold": 0.7,
    "DefaultNightBeforeTime": "18:00",
    "DefaultMorningTime": "07:00",
    "RequireConfirmation": true,
    "Categories": {
      "Preparation": { "LeadTimeHours": 14 },
      "Payment": { "LeadTimeHours": 1 },
      "Permission": { "MultipleReminders": true },
      "Transport": { "LeadTimeHours": 1 },
      "Clothing": { "LeadTimeHours": 14 }
    }
  }
}
```

## Success Metrics
- 80%+ accuracy in extracting actionable items
- <5% false positive rate
- Average parent interaction time reduced by 50%
- 90%+ of important items captured

## Testing Strategy
1. Create test dataset of week letters with known actionable items
2. Measure extraction accuracy
3. A/B test timing strategies
4. Gather parent feedback on usefulness

## Files to Create/Modify
- src/Aula/Services/WeekLetterAnalyzer.cs (new)
- src/Aula/Services/ReminderExtractor.cs (new)
- src/Aula/Services/ReminderScheduler.cs (new)
- src/Aula/Services/OpenAiService.cs (extend)
- src/Aula/Configuration/Config.cs (add AutomaticReminders)
- Database migration for automatic_reminders table

## Rollout Plan
1. Beta test with subset of users
2. Manual review of extracted reminders initially
3. Gradual increase in automation based on confidence
4. Full automation with opt-in confirmation mode