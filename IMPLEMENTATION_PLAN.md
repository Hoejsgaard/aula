# Aula Bot Enhancement Implementation Plan

## Current Status
- ✅ Interactive bot working (Slack + Telegram)
- ✅ Multi-child question handling implemented
- ✅ Basic week letter fetching and posting

## Phase 1: Scheduled Posting & Persistence

### 1. Supabase Setup
- Create Supabase project
- Design database schema:
  - `reminders` table: id, text, date, time, created_at, is_sent
  - `posted_letters` table: child_name, week_number, year, hash, posted_at
  - `app_state` table: key, value (for misc state storage)

### 2. Scheduled Weekly Posting
- Add timer for Sunday 16:00 weekly posting
- Check for new/updated week letters for all children
- Only post if week letter hash differs from last posted
- Post to all configured channels (Slack, Telegram)

### 3. Retry Logic
- If week letter missing/unchanged on Sunday 16:00
- Retry every hour for up to 48 hours
- Track retry attempts in database
- Give up after 48 hours with notification

### 4. Manual Reminder System
- Database table for storing reminders
- Bot commands to add reminders: "remind me tomorrow at 8:00 that TestChild1 has Haver til maver"
- Morning notification system (configurable time, e.g., 7:00 AM)
- Supabase web interface for manual reminder management

## Phase 2: Smart Features (Future)

### 5. Automatic Reminder Extraction
- Parse week letters for important events
- Keywords: "haver til maver", "ingen bøger", "let pakket", "ekskursion"
- Auto-create reminders for detected events
- Machine learning/AI parsing for better detection

### 6. Enhanced Notifications
- Different reminder types (morning, evening, day-before)
- SMS notifications for critical reminders
- Calendar integration

## Technical Implementation

### Database Schema (Supabase)
```sql
-- Reminders table
CREATE TABLE reminders (
  id SERIAL PRIMARY KEY,
  text TEXT NOT NULL,
  remind_date DATE NOT NULL,
  remind_time TIME NOT NULL,
  created_at TIMESTAMP DEFAULT NOW(),
  is_sent BOOLEAN DEFAULT FALSE,
  child_name TEXT
);

-- Posted week letters tracking
CREATE TABLE posted_letters (
  id SERIAL PRIMARY KEY,
  child_name TEXT NOT NULL,
  week_number INTEGER NOT NULL,
  year INTEGER NOT NULL,
  content_hash TEXT NOT NULL,
  posted_at TIMESTAMP DEFAULT NOW(),
  UNIQUE(child_name, week_number, year)
);

-- App state for misc configuration
CREATE TABLE app_state (
  key TEXT PRIMARY KEY,
  value TEXT NOT NULL,
  updated_at TIMESTAMP DEFAULT NOW()
);
```

### Configuration Changes
- Add Supabase connection string to appsettings.json
- Configure reminder notification times
- Add retry attempt limits and intervals

### New Services Needed
- `ReminderService`: Manage reminders, notifications
- `SchedulingService`: Handle weekly posting and retries
- `SupabaseService`: Database operations
- `NotificationService`: Send morning/evening reminders

## Bot Commands to Add
- `remind me [date] at [time] that [text]`
- `list reminders`
- `delete reminder [id]`
- `check for new letters`
- `force post letters`

## Success Criteria
1. Weekly letters posted automatically every Sunday if available
2. Retry logic works for delayed week letter publishing
3. Manual reminders can be added via bot or web interface
4. Morning notifications sent reliably
5. No duplicate week letter posts
6. System survives restarts/crashes with persistence

## Risk Mitigation
- Supabase free tier limits (monitor usage)
- Network connectivity issues (retry logic)
- Bot token expiration (error handling + notifications)
- Clock synchronization for scheduling (use UTC)