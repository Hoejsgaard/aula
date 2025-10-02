-- ═══════════════════════════════════════════════════════════════
-- DATABASE MIGRATION FOR TASK 004: Intelligent Automatic Reminder Extraction
-- ═══════════════════════════════════════════════════════════════
--
-- Description: Adds columns to support automatic reminder extraction from week letters
-- Created: 2025-10-02
--
-- IMPORTANT: Run these scripts in Supabase SQL Editor in the correct order
-- ═══════════════════════════════════════════════════════════════

-- 1. EXTEND posted_letters TABLE
-- Add columns to track reminder extraction status
ALTER TABLE posted_letters
ADD COLUMN IF NOT EXISTS auto_reminders_extracted BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS auto_reminders_last_updated TIMESTAMP WITH TIME ZONE;

-- Add index for performance on auto-reminder queries
CREATE INDEX IF NOT EXISTS idx_posted_letters_auto_reminders
ON posted_letters(child_name, auto_reminders_extracted);

-- 2. EXTEND reminders TABLE
-- Add columns to support auto-extracted reminders with metadata
ALTER TABLE reminders
ADD COLUMN IF NOT EXISTS source VARCHAR(50) DEFAULT 'manual',
ADD COLUMN IF NOT EXISTS week_letter_id INTEGER,
ADD COLUMN IF NOT EXISTS event_type VARCHAR(50),
ADD COLUMN IF NOT EXISTS event_title VARCHAR(200),
ADD COLUMN IF NOT EXISTS extracted_date_time TIMESTAMP WITH TIME ZONE,
ADD COLUMN IF NOT EXISTS confidence_score DECIMAL(3,2);

-- Add foreign key constraint to link reminders to week letters
ALTER TABLE reminders
ADD CONSTRAINT fk_reminders_week_letter_id
FOREIGN KEY (week_letter_id) REFERENCES posted_letters(id) ON DELETE CASCADE;

-- Add indexes for performance
CREATE INDEX IF NOT EXISTS idx_reminders_source ON reminders(source);
CREATE INDEX IF NOT EXISTS idx_reminders_week_letter_id ON reminders(week_letter_id);
CREATE INDEX IF NOT EXISTS idx_reminders_event_type ON reminders(event_type);

-- 3. ADD CHECK CONSTRAINTS for data validation
ALTER TABLE reminders
ADD CONSTRAINT chk_reminders_source
CHECK (source IN ('manual', 'auto_extracted', 'schedule_conflict'));

ALTER TABLE reminders
ADD CONSTRAINT chk_reminders_event_type
CHECK (event_type IS NULL OR event_type IN ('deadline', 'permission_form', 'event', 'supply_needed'));

ALTER TABLE reminders
ADD CONSTRAINT chk_reminders_confidence_score
CHECK (confidence_score IS NULL OR (confidence_score >= 0.1 AND confidence_score <= 1.0));

-- 4. UPDATE RLS POLICIES (if using Row Level Security)
-- Ensure the new columns respect existing security policies

-- For posted_letters - allow updates to auto_reminders columns
DROP POLICY IF EXISTS "posted_letters_auto_reminders_policy" ON posted_letters;
CREATE POLICY "posted_letters_auto_reminders_policy" ON posted_letters
    FOR UPDATE USING (true)
    WITH CHECK (true);

-- For reminders - allow auto-extracted reminders with same security as manual ones
DROP POLICY IF EXISTS "reminders_auto_extracted_policy" ON reminders;
CREATE POLICY "reminders_auto_extracted_policy" ON reminders
    FOR ALL USING (true)
    WITH CHECK (true);

-- 5. VERIFY MIGRATION SUCCESS
-- Run these queries to verify the migration worked correctly

-- Check posted_letters table structure
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'posted_letters'
  AND column_name IN ('auto_reminders_extracted', 'auto_reminders_last_updated')
ORDER BY column_name;

-- Check reminders table structure
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_name = 'reminders'
  AND column_name IN ('source', 'week_letter_id', 'event_type', 'event_title', 'extracted_date_time', 'confidence_score')
ORDER BY column_name;

-- Check constraints
SELECT constraint_name, constraint_type
FROM information_schema.table_constraints
WHERE table_name IN ('posted_letters', 'reminders')
  AND constraint_name LIKE '%auto%' OR constraint_name LIKE '%chk_%';

-- Check indexes
SELECT indexname, tablename
FROM pg_indexes
WHERE tablename IN ('posted_letters', 'reminders')
  AND indexname LIKE '%auto%' OR indexname LIKE '%reminders_%';

-- ═══════════════════════════════════════════════════════════════
-- ROLLBACK SCRIPT (if needed)
-- ═══════════════════════════════════════════════════════════════
--
-- UNCOMMENT AND RUN ONLY IF YOU NEED TO UNDO THE MIGRATION
--
-- -- Remove constraints
-- ALTER TABLE reminders DROP CONSTRAINT IF EXISTS chk_reminders_confidence_score;
-- ALTER TABLE reminders DROP CONSTRAINT IF EXISTS chk_reminders_event_type;
-- ALTER TABLE reminders DROP CONSTRAINT IF EXISTS chk_reminders_source;
-- ALTER TABLE reminders DROP CONSTRAINT IF EXISTS fk_reminders_week_letter_id;
--
-- -- Remove indexes
-- DROP INDEX IF EXISTS idx_reminders_event_type;
-- DROP INDEX IF EXISTS idx_reminders_week_letter_id;
-- DROP INDEX IF EXISTS idx_reminders_source;
-- DROP INDEX IF EXISTS idx_posted_letters_auto_reminders;
--
-- -- Remove columns from reminders
-- ALTER TABLE reminders DROP COLUMN IF EXISTS confidence_score;
-- ALTER TABLE reminders DROP COLUMN IF EXISTS extracted_date_time;
-- ALTER TABLE reminders DROP COLUMN IF EXISTS event_title;
-- ALTER TABLE reminders DROP COLUMN IF EXISTS event_type;
-- ALTER TABLE reminders DROP COLUMN IF EXISTS week_letter_id;
-- ALTER TABLE reminders DROP COLUMN IF EXISTS source;
--
-- -- Remove columns from posted_letters
-- ALTER TABLE posted_letters DROP COLUMN IF EXISTS auto_reminders_last_updated;
-- ALTER TABLE posted_letters DROP COLUMN IF EXISTS auto_reminders_extracted;
--
-- -- Remove policies
-- DROP POLICY IF EXISTS "reminders_auto_extracted_policy" ON reminders;
-- DROP POLICY IF EXISTS "posted_letters_auto_reminders_policy" ON posted_letters;