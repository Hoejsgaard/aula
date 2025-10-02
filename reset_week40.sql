-- Reset week 40 data for both children

-- First, check what we have
SELECT 'BEFORE RESET - Posted Letters:' as info;
SELECT child_name, week_number, year, auto_reminders_extracted, id
FROM posted_letters
WHERE child_name IN ('Søren Johannes', 'Hans Martin')
  AND week_number = 40
  AND year = 2025;

SELECT 'BEFORE RESET - Reminders:' as info;
SELECT COUNT(*) as reminder_count, child_name
FROM reminders
WHERE source = 'auto_extracted'
  AND child_name IN ('Søren Johannes', 'Hans Martin')
  AND week_letter_id IN (
    SELECT id FROM posted_letters
    WHERE child_name IN ('Søren Johannes', 'Hans Martin')
      AND week_number = 40
      AND year = 2025
  )
GROUP BY child_name;

-- Reset AutoRemindersExtracted flag for both children for week 40/2025
UPDATE posted_letters
SET auto_reminders_extracted = false,
    auto_reminders_last_updated = NULL
WHERE child_name IN ('Søren Johannes', 'Hans Martin')
  AND week_number = 40
  AND year = 2025;

-- Delete existing auto-extracted reminders for week 40
DELETE FROM reminders
WHERE source = 'auto_extracted'
  AND week_letter_id IN (
    SELECT id FROM posted_letters
    WHERE child_name IN ('Søren Johannes', 'Hans Martin')
      AND week_number = 40
      AND year = 2025
  );

-- Verify the reset
SELECT 'AFTER RESET - Posted Letters:' as info;
SELECT child_name, week_number, year, auto_reminders_extracted, id
FROM posted_letters
WHERE child_name IN ('Søren Johannes', 'Hans Martin')
  AND week_number = 40
  AND year = 2025;

SELECT 'AFTER RESET - Reminders:' as info;
SELECT COUNT(*) as reminder_count, child_name
FROM reminders
WHERE source = 'auto_extracted'
  AND child_name IN ('Søren Johannes', 'Hans Martin')
  AND week_letter_id IN (
    SELECT id FROM posted_letters
    WHERE child_name IN ('Søren Johannes', 'Hans Martin')
      AND week_number = 40
      AND year = 2025
  )
GROUP BY child_name;