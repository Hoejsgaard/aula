# Supabase Setup Instructions

## 1. Create Supabase Account
1. Go to [supabase.com](https://supabase.com)
2. Sign up for a free account
3. Create a new project

## 2. Get Your Credentials
After creating your project, you'll need the following values for `appsettings.json`:

### Supabase URL
- Go to Project Settings → API
- Copy the "Project URL" (looks like: `https://abcdefghijklmnop.supabase.co`)
- Replace `YOUR_SUPABASE_PROJECT_URL` in `appsettings.json`

### Anon Key
- In the same API settings page
- Copy the "anon public" key (starts with `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.`)
- Replace `YOUR_SUPABASE_ANON_KEY` in `appsettings.json`

### Service Role Key (Optional but Recommended)
- Copy the "service_role" key (also starts with `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.`)
- **SECURITY WARNING**: Do NOT store this key in `appsettings.json` as it has admin privileges
- Store it in a secure environment variable `SUPABASE_SERVICE_ROLE_KEY` instead
- The application will read it from the environment variable at runtime

## 3. Create Database Tables
In the Supabase SQL Editor, run this SQL to create the required tables:

```sql
-- App state for misc configuration and tracking
CREATE TABLE app_state (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL,
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Posted week letters tracking to avoid duplicates
CREATE TABLE posted_letters (
  id SERIAL PRIMARY KEY,
  child_name TEXT NOT NULL,
  week_number INTEGER NOT NULL,
  year INTEGER NOT NULL,
  content_hash TEXT NOT NULL,
  posted_at TIMESTAMPTZ DEFAULT NOW(),
  posted_to_slack BOOLEAN DEFAULT FALSE,
  posted_to_telegram BOOLEAN DEFAULT FALSE,
  raw_content JSONB,
  auto_reminders_extracted BOOLEAN DEFAULT FALSE,
  auto_reminders_last_updated TIMESTAMP WITH TIME ZONE,
  UNIQUE(child_name, week_number, year)
);

-- Reminders table with AI extraction support
CREATE TABLE reminders (
  id SERIAL PRIMARY KEY,
  text TEXT NOT NULL,
  remind_date DATE NOT NULL,
  remind_time TIME NOT NULL,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  is_sent BOOLEAN DEFAULT FALSE,
  child_name TEXT,
  created_by TEXT DEFAULT 'bot',
  source VARCHAR(50) DEFAULT 'manual',
  week_letter_id INTEGER,
  event_type VARCHAR(50),
  event_title VARCHAR(200),
  extracted_date_time TIMESTAMP WITH TIME ZONE,
  confidence_score DECIMAL(3,2),
  CONSTRAINT fk_reminders_week_letter_id FOREIGN KEY (week_letter_id) REFERENCES posted_letters(id) ON DELETE CASCADE,
  CONSTRAINT chk_reminders_source CHECK (source IN ('manual', 'auto_extracted', 'schedule_conflict')),
  CONSTRAINT chk_reminders_event_type CHECK (event_type IS NULL OR event_type IN ('deadline', 'permission_form', 'event', 'supply_needed')),
  CONSTRAINT chk_reminders_confidence_score CHECK (confidence_score IS NULL OR (confidence_score >= 0.1 AND confidence_score <= 1.0))
);

-- Retry attempts for failed week letter fetching
CREATE TABLE retry_attempts (
  id SERIAL PRIMARY KEY,
  child_name TEXT NOT NULL,
  week_number INTEGER NOT NULL,
  year INTEGER NOT NULL,
  attempt_count INTEGER DEFAULT 1 CHECK (attempt_count >= 0),
  last_attempt TIMESTAMPTZ DEFAULT NOW(),
  next_attempt TIMESTAMPTZ,
  max_attempts INTEGER DEFAULT 48 CHECK (max_attempts >= 0),
  is_successful BOOLEAN DEFAULT FALSE
);

-- Scheduled tasks configuration
CREATE TABLE scheduled_tasks (
  id SERIAL PRIMARY KEY,
  name TEXT UNIQUE NOT NULL,
  description TEXT,
  cron_expression TEXT NOT NULL,
  enabled BOOLEAN DEFAULT TRUE,
  retry_interval_hours INTEGER DEFAULT 1,
  max_retry_hours INTEGER DEFAULT 48,
  last_run TIMESTAMPTZ,
  next_run TIMESTAMPTZ,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Insert default scheduled tasks
INSERT INTO scheduled_tasks (name, description, cron_expression, retry_interval_hours, max_retry_hours) VALUES
('WeeklyLetterCheck', 'Check for new week letters every Sunday at 4 PM', '0 16 * * 0', 1, 48),
('MorningReminders', 'Send pending reminders every morning at 7 AM', '0 7 * * *', NULL, NULL);

-- Create basic indexes for performance
CREATE INDEX IF NOT EXISTS idx_posted_letters_auto_reminders ON posted_letters(child_name, auto_reminders_extracted);
CREATE INDEX IF NOT EXISTS idx_reminders_source ON reminders(source);
CREATE INDEX IF NOT EXISTS idx_reminders_week_letter_id ON reminders(week_letter_id);
CREATE INDEX IF NOT EXISTS idx_reminders_event_type ON reminders(event_type);
```

## 3.1 Performance Optimization (Optional)
For optimal query performance, especially if you have many reminders, apply the additional performance indexes:

```bash
# Run the performance optimization patch
psql -h YOUR_SUPABASE_HOST -U postgres -d postgres -f doc/PERFORMANCE_INDEXES_PATCH.sql
```

Or copy and paste the contents of `doc/PERFORMANCE_INDEXES_PATCH.sql` into the Supabase SQL Editor.

These indexes optimize:
- ✅ **Pending reminder lookups** - Faster morning reminder checks
- ✅ **AI reminder replacement** - Faster deletion of old auto-extracted reminders
- ✅ **Child-specific queries** - Better performance for per-child reminder views

## 4. Enable Row Level Security (REQUIRED)
**⚠️ SECURITY REQUIREMENT**: Enable Row Level Security (RLS) on all tables to prevent unauthorized access:

```sql
-- Enable RLS on all tables
ALTER TABLE reminders ENABLE ROW LEVEL SECURITY;
ALTER TABLE posted_letters ENABLE ROW LEVEL SECURITY;
ALTER TABLE app_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE retry_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE scheduled_tasks ENABLE ROW LEVEL SECURITY;

-- Drop any existing policies (in case of re-setup)
DROP POLICY IF EXISTS "Service role can do everything" ON reminders;
DROP POLICY IF EXISTS "Service role can do everything" ON posted_letters;
DROP POLICY IF EXISTS "Service role can do everything" ON app_state;
DROP POLICY IF EXISTS "Service role can do everything" ON retry_attempts;
DROP POLICY IF EXISTS "service_role_full_access" ON reminders;
DROP POLICY IF EXISTS "service_role_full_access" ON posted_letters;
DROP POLICY IF EXISTS "service_role_full_access" ON app_state;
DROP POLICY IF EXISTS "service_role_full_access" ON retry_attempts;
DROP POLICY IF EXISTS "service_role_full_access" ON scheduled_tasks;

-- Note: service_role bypasses RLS automatically, so explicit policies are optional
-- These policies are included for completeness but service_role already has full access
CREATE POLICY "service_role_full_access" ON reminders
    FOR ALL
    USING (auth.role() = 'service_role');

CREATE POLICY "service_role_full_access" ON posted_letters
    FOR ALL
    USING (auth.role() = 'service_role');

CREATE POLICY "service_role_full_access" ON app_state
    FOR ALL
    USING (auth.role() = 'service_role');

CREATE POLICY "service_role_full_access" ON retry_attempts
    FOR ALL
    USING (auth.role() = 'service_role');

CREATE POLICY "service_role_full_access" ON scheduled_tasks
    FOR ALL
    USING (auth.role() = 'service_role');
```

### Verify RLS Configuration
After enabling RLS, verify it's working correctly:

```sql
-- Check RLS status on all tables
SELECT schemaname, tablename, rowsecurity
FROM pg_tables
WHERE schemaname = 'public'
AND tablename IN ('reminders', 'posted_letters', 'app_state', 'retry_attempts', 'scheduled_tasks');

-- List all policies (should show 5 policies)
SELECT schemaname, tablename, policyname, roles, cmd
FROM pg_policies
WHERE schemaname = 'public'
ORDER BY tablename;
```

Expected result: All tables should show `rowsecurity = true` and you should see 5 `service_role_full_access` policies.

## 5. Configuration Summary
After setup, your `appsettings.json` should have:

```json
"Supabase": {
  "Url": "https://your-project-id.supabase.co",
  "Key": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.your-anon-key..."
  // ServiceRoleKey is read from SUPABASE_SERVICE_ROLE_KEY environment variable
}
```

## 6. Testing Connection
Once configured, the bot will:
- Test the Supabase connection on startup
- Create initial app state entries if needed
- Log any connection issues

## 7. Managing Reminders via Supabase Dashboard
1. Go to your Supabase project dashboard
2. Click "Table Editor"
3. Select the "reminders" table
4. Add/edit/delete reminders manually
5. Set `is_sent` to `false` to re-send a reminder

### Example Reminder Entry
- **text**: "TestChild1 has Haver to maver today - no books needed!"
- **remind_date**: 2024-01-15
- **remind_time**: 07:30:00
- **child_name**: TestChild1
- **is_sent**: false