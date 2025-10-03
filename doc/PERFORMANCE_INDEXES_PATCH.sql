-- Performance optimization indexes for Aula reminder system
-- Apply these after running the main SUPABASE_SETUP.md schema

-- Index for GetPendingRemindersAsync query performance
-- Query pattern: WHERE is_sent = false AND remind_date <= ?
-- This composite index enables fast lookup of unsent reminders
CREATE INDEX IF NOT EXISTS idx_reminders_pending_lookup
ON reminders(is_sent, remind_date)
WHERE is_sent = false;

-- Index for DeleteAutoExtractedRemindersByWeekLetterIdAsync query performance
-- Query pattern: WHERE week_letter_id = ? AND source = 'auto_extracted'
-- This composite index enables fast deletion of auto-extracted reminders
CREATE INDEX IF NOT EXISTS idx_reminders_week_letter_source_lookup
ON reminders(week_letter_id, source)
WHERE source = 'auto_extracted';

-- Optional: Index for child-specific reminder queries
-- Useful if querying reminders by child becomes common
CREATE INDEX IF NOT EXISTS idx_reminders_child_date
ON reminders(child_name, remind_date)
WHERE child_name IS NOT NULL;

-- Verify indexes were created
SELECT
    schemaname,
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
AND tablename = 'reminders'
AND indexname LIKE 'idx_reminders_%'
ORDER BY indexname;