# Testing Playbook for Retry Notification Feature

## The Problem
I kept making the same mistakes testing the retry notification feature. Here's the proper process:

## Proper Testing Steps

1. **Fix the bug** (already done - retry notification implementation)

2. **Clear the week letters for week of interest** (currently week 41)
   - Use Supabase API to delete from posted_letters table for current week

3. **Clear unsent reminders**
   - Use Supabase API to delete from reminders table
   - Use Supabase API to delete from retry_attempts table

4. **Start the app**
   - Kill any existing dotnet processes
   - Start fresh with `dotnet run`

5. **Set trigger week letter reminder in 2 minutes**
   - Update scheduled_tasks table via Supabase API
   - Set WeeklyLetterCheck next_run to current time + 2 minutes

6. **Await actual results:**
   - **Expected when week letters found:** fetch, post, AI analysis
   - **Expected when week letters NOT found:** fetch (null), post of retry info and retry trigger (database)

## Key Commands

### Clear posted letters for current week (41/2025):
```bash
curl -X DELETE \
  'YOUR_SUPABASE_URL/rest/v1/posted_letters?week_number=eq.41&year=eq.2025' \
  -H 'apikey: YOUR_SUPABASE_SERVICE_ROLE_KEY' \
  -H 'Authorization: Bearer YOUR_SUPABASE_SERVICE_ROLE_KEY'
```

### Clear all reminders:
```bash
curl -X DELETE \
  'YOUR_SUPABASE_URL/rest/v1/reminders?id=gte.0' \
  -H 'apikey: YOUR_SUPABASE_SERVICE_ROLE_KEY' \
  -H 'Authorization: Bearer YOUR_SUPABASE_SERVICE_ROLE_KEY'
```

### Clear retry attempts:
```bash
curl -X DELETE \
  'YOUR_SUPABASE_URL/rest/v1/retry_attempts?id=gte.0' \
  -H 'apikey: YOUR_SUPABASE_SERVICE_ROLE_KEY' \
  -H 'Authorization: Bearer YOUR_SUPABASE_SERVICE_ROLE_KEY'
```

### Set WeeklyLetterCheck to run in 2 minutes:
```bash
curl -X PATCH \
  'YOUR_SUPABASE_URL/rest/v1/scheduled_tasks?name=eq.WeeklyLetterCheck' \
  -H 'apikey: YOUR_SUPABASE_SERVICE_ROLE_KEY' \
  -H 'Authorization: Bearer YOUR_SUPABASE_SERVICE_ROLE_KEY' \
  -H 'Content-Type: application/json' \
  -d '{"next_run": "CURRENT_TIME_PLUS_2_MINUTES_UTC"}'
```

## Don't Be Retarded Like Before

- **NO ASSUMPTIONS**: Wait for actual logs and results
- **FOLLOW THE STEPS IN ORDER**: Don't skip or change sequence
- **TEST BOTH SCENARIOS**: Week letters found vs not found
- **CHECK DATABASE**: Verify retry_attempts records are created
- **WATCH LOGS**: Look for actual execution, not just scheduling
- **NEVER CHANGE PROGRAM.CS**: You can under no circumstances change program for the fun of testing
- **NO C# FOR SCRIPTS**: Don't use C# for scripts! You can orchestrate the api calls ad hoc, or make batch/python/whatever ... as long as it's not C#

## Current Context
- We are testing week 41/2025 (current week)
- Both children: Søren Johannes and Hans Martin
- Expecting retry notifications if no week letters found
- Notifications should be sent ONCE per child, not 24 times