# Historical Week Letter Population

This document describes the one-off feature for populating the database with historical week letters from the past 8 weeks.

## Purpose

During summer holidays, the MinUddannelse system typically doesn't have fresh week letters available. This makes it difficult to test the week letter functionality. The historical population feature solves this by:

1. Fetching week letters from the past 8 weeks
2. Storing them in the Supabase database
3. Enabling testing with real historical data

## How to Use

### 1. Enable the Feature

Add these settings to your `appsettings.json`:

```json
{
  "Features": {
    "UseStoredWeekLetters": true,
    "TestWeekNumber": null,
    "TestYear": null
  }
}
```

### 2. Run the Application

When you start the application with `UseStoredWeekLetters` set to `true`, it will automatically:

- ✅ Test the Supabase connection
- 📅 Fetch historical week letters for the past 8 weeks
- 💾 Store them in the database (avoiding duplicates)
- 📊 Report success statistics

### 3. Monitor the Process

Watch the console output for progress indicators:

```console
📅 Fetching historical week letters for past 8 weeks
📆 Processing week 25/2024 (date: 06/17/2024)
✅ Stored week's letter for Søren Johannes week 25/2024 (1234 chars)
⚠️ Week's letter for Hans Martin week 25/2024 has no content – skipping
🎉 Historical week letter population complete: 12/16 successful
```

## What Happens

### Data Fetched
- **Time Range**: Past 8 weeks from today
- **Children**: All configured children in your `appsettings.json`
- **Content**: Full week letter JSON including Danish content

### Smart Filtering
- ✅ Only stores week letters with actual content
- ❌ Skips placeholder messages like "Der er ikke skrevet nogen ugenoter"
- 🔄 Avoids duplicates by checking existing data first

### API Respectful
- 500ms delay between requests to avoid overwhelming the MinUddannelse API
- Proper error handling for network issues
- Graceful handling of missing week letters

## Testing with Stored Data

Once historical data is populated, you can:

1. **Test Interactive Bots**: Ask questions about stored week letters
2. **Test AI Features**: Use stored content for AI reminder extraction
3. **Summer Development**: Continue development without live week letters

## Configuration Options

### `UseStoredWeekLetters`
- **Type**: `boolean`
- **Default**: `false`
- **Purpose**: Enables the historical population on startup

### `TestWeekNumber` (Optional)
- **Type**: `int?`
- **Default**: `null`
- **Purpose**: Force a specific week number for testing
- **Example**: `25` to always use week 25

### `TestYear` (Optional)
- **Type**: `int?`
- **Default**: `null`
- **Purpose**: Force a specific year for testing
- **Example**: `2024` to always use year 2024

## Cleanup

### After Seeding Data

1. **Disable Auto-Population**: Set `UseStoredWeekLetters` to `false` or remove it
2. **Remove the Code**: Delete the `PopulateHistoricalWeekLetters` method from `Program.cs`
3. **Keep the Data**: The stored week letters remain in Supabase for testing

### Database Storage

Week letters are stored in the `posted_letters` table with:
- `child_name`: Child's first name
- `week_number`: ISO week number
- `year`: Year
- `raw_content`: Full JSON content
- `content_hash`: SHA256 hash for deduplication

## Error Handling

The system gracefully handles:
- ❌ **Login failures**: Skips population if MinUddannelse login fails
- ❌ **Network errors**: Logs errors but continues with next week letter
- ❌ **Missing content**: Skips weeks with no actual content
- ❌ **Database errors**: Logs issues but doesn't crash the application

## Performance

- **API Calls**: Up to 16 calls (8 weeks × 2 children) with 500ms delays
- **Duration**: Approximately 8–10 seconds for full population
- **Storage**: ~5–50KB per week's letter depending on content
- **One-time**: Only runs when `UseStoredWeekLetters` is `true`

## Example Output

```console
🗂️ Starting one-off historical week letter population
📅 Fetching historical week letters for past 8 weeks
📆 Processing week 26/2024 (date: 06/24/2024)
✅ Week's letter for Søren Johannes week 26/2024 already exists – skipping
✅ Stored week's letter for Hans Martin week 26/2024 (1567 chars)
📆 Processing week 25/2024 (date: 06/17/2024)
✅ Stored week's letter for Søren Johannes week 25/2024 (2134 chars)
⚠️ Week's letter for Hans Martin week 25/2024 has no content – skipping
🎉 Historical week letter population complete: 14/16 successful
📊 You can now test with stored week letters by setting Features.UseStoredWeekLetters = true
🔧 Remember to remove this PopulateHistoricalWeekLetters method once you're done seeding data
```

## Next Steps

After populating historical data, you can:

1. **Build the Crown Jewel Feature**: Use stored content for automatic reminder extraction
2. **Enhance Testing**: Create more sophisticated test scenarios
3. **Improve AI Features**: Train on real Danish school communication patterns

This feature bridges the gap between live data and development needs, enabling year-round development and testing! 🚀