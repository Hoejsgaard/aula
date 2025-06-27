# Supabase Setup Instructions

## 1. Create Supabase Account
1. Go to [supabase.com](https://supabase.com)
2. Sign up for a free account
3. Create a new project

## 2. Get Your Credentials
After creating your project, you'll need these values for `appsettings.json`:

### Supabase URL
- Go to Project Settings → API
- Copy the "Project URL" (looks like: `https://abcdefghijklmnop.supabase.co`)
- Replace `YOUR_SUPABASE_PROJECT_URL` in appsettings.json

### Anon Key
- In the same API settings page
- Copy the "anon public" key (starts with `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.`)
- Replace `YOUR_SUPABASE_ANON_KEY` in appsettings.json

### Service Role Key (Optional but Recommended)
- Copy the "service_role" key (also starts with `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.`)
- Replace `YOUR_SUPABASE_SERVICE_ROLE_KEY` in appsettings.json
- This key has admin privileges - keep it secure!

## 3. Create Database Tables
In the Supabase SQL Editor, run this SQL to create the required tables:

```sql
-- Reminders table for manual reminder management
CREATE TABLE reminders (
  id SERIAL PRIMARY KEY,
  text TEXT NOT NULL,
  remind_date DATE NOT NULL,
  remind_time TIME NOT NULL,
  created_at TIMESTAMP DEFAULT NOW(),
  is_sent BOOLEAN DEFAULT FALSE,
  child_name TEXT,
  created_by TEXT DEFAULT 'bot'
);

-- Posted week letters tracking to avoid duplicates
CREATE TABLE posted_letters (
  id SERIAL PRIMARY KEY,
  child_name TEXT NOT NULL,
  week_number INTEGER NOT NULL,
  year INTEGER NOT NULL,
  content_hash TEXT NOT NULL,
  posted_at TIMESTAMP DEFAULT NOW(),
  posted_to_slack BOOLEAN DEFAULT FALSE,
  posted_to_telegram BOOLEAN DEFAULT FALSE,
  UNIQUE(child_name, week_number, year)
);

-- App state for misc configuration and tracking
CREATE TABLE app_state (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL,
  updated_at TIMESTAMP DEFAULT NOW()
);

-- Retry attempts for failed week letter fetching
CREATE TABLE retry_attempts (
  id SERIAL PRIMARY KEY,
  child_name TEXT NOT NULL,
  week_number INTEGER NOT NULL,
  year INTEGER NOT NULL,
  attempt_count INTEGER DEFAULT 1,
  last_attempt TIMESTAMP DEFAULT NOW(),
  next_attempt TIMESTAMP,
  max_attempts INTEGER DEFAULT 48,
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
  last_run TIMESTAMP,
  next_run TIMESTAMP,
  created_at TIMESTAMP DEFAULT NOW(),
  updated_at TIMESTAMP DEFAULT NOW()
);

-- Insert default scheduled tasks
INSERT INTO scheduled_tasks (name, description, cron_expression, retry_interval_hours, max_retry_hours) VALUES
('WeeklyLetterCheck', 'Check for new week letters every Sunday at 4 PM', '0 16 * * 0', 1, 48),
('MorningReminders', 'Send pending reminders every morning at 7 AM', '0 7 * * *', NULL, NULL);
```

## 4. Set Row Level Security (Optional)
If you want extra security, you can enable Row Level Security:

```sql
-- Enable RLS on all tables
ALTER TABLE reminders ENABLE ROW LEVEL SECURITY;
ALTER TABLE posted_letters ENABLE ROW LEVEL SECURITY;
ALTER TABLE app_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE retry_attempts ENABLE ROW LEVEL SECURITY;

-- Create policies to allow service role access
CREATE POLICY "Service role can do everything" ON reminders FOR ALL USING (auth.role() = 'service_role');
CREATE POLICY "Service role can do everything" ON posted_letters FOR ALL USING (auth.role() = 'service_role');
CREATE POLICY "Service role can do everything" ON app_state FOR ALL USING (auth.role() = 'service_role');
CREATE POLICY "Service role can do everything" ON retry_attempts FOR ALL USING (auth.role() = 'service_role');
```

## 5. Configuration Summary
After setup, your `appsettings.json` should have:

```json
"Supabase": {
  "Url": "https://your-project-id.supabase.co",
  "Key": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.your-anon-key...",
  "ServiceRoleKey": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.your-service-role-key..."
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

### Example Reminder Entry:
- **text**: "TestChild1 has Haver til maver today - no books needed!"
- **remind_date**: 2024-01-15
- **remind_time**: 07:30:00
- **child_name**: TestChild1
- **is_sent**: false