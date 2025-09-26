# Development Tasks

This folder contains detailed task descriptions for planned improvements and bug fixes for the Aula application.

## Task Status Overview

| Task | Title | Priority | Status |
|------|-------|----------|--------|
| [001](001-move-post-week-letters-on-startup.md) | Move PostWeekLettersOnStartup Configuration | Medium | COMPLETED ✅ |
| [002](002-investigate-agent-service-authentication.md) | Investigate AgentService Authentication | High | COMPLETED ✅ |
| [003](003-move-polling-frequency-to-config.md) | Move Polling Frequency to Configuration | Medium | PENDING |
| [004](004-ai-screening-automatic-reminders.md) | AI Screening for Automatic Reminders | HIGH (Crown Jewel) | PENDING |
| [005](005-weekly-fetch-task-initialization.md) | ~~Weekly Fetch Task Database Initialization~~ | ~~High~~ | RESOLVED - WONTFIX ✅ |
| [006](006-pictogram-authentication-support.md) | Pictogram Authentication Support | High | COMPLETED ✅ |
| **[007](007-child-centric-architecture.md)** | **Child-Centric Architecture Transformation** | **CRITICAL** | **READY** ⚠️ |

## Task Categories

### Configuration Improvements
- **Task 001**: Clean up configuration duplication
- **Task 003**: Make polling frequency configurable

### Performance & Reliability
- **Task 002**: Fix authentication session timeout issue
- ~~**Task 005**: Ensure scheduled tasks are properly initialized~~ (Resolved - handled by setup docs)

### Feature Enhancements
- **Task 004**: Implement intelligent automatic reminder extraction (Crown Jewel Feature)
- **Task 006**: Support pictogram-based authentication for younger children

## How to Use These Tasks

1. Each task file contains:
   - Problem description
   - Current state analysis
   - Proposed solution
   - Implementation steps
   - Files to modify
   - Testing requirements

2. When starting a task:
   - Update status in the task file
   - Create a feature branch
   - Follow the implementation plan
   - Update tests as needed
   - Update this README when complete

3. Task Completion Checklist:
   - [ ] Implementation complete
   - [ ] Tests written and passing
   - [ ] Documentation updated
   - [ ] Configuration updated if needed
   - [ ] Task file status updated to COMPLETED
   - [ ] This README updated

## Priority Guidance

**CRITICAL (Do Now)**
- **Task 007: Child-Centric Architecture** - Fundamental architecture fix enabling proper child isolation
  - ⚠️ **MUST use ultrathink mode when implementing**
  - 7-week phased implementation with incremental testing
  - See detailed plan and appendices in task file

**HIGH Priority (Do Next)**
- Task 004: AI Screening for Automatic Reminders - Major feature that provides unique value

**MEDIUM Priority (Future)**
- Task 003: Move Polling Frequency to Configuration - Quality of life improvement

## Notes

- **NEW: Task 007** - Child-Centric Architecture is CRITICAL for proper data isolation
- Task 002 & 006 COMPLETED - TestChild1's pictogram authentication now works
- Task 005 resolved as WONTFIX - already handled by database setup instructions
- Task 004 is the "crown jewel" feature that would differentiate this application
- Task 003 - quality-of-life improvement for operators (polling frequency config)

## Appendices

Detailed architecture documentation available in:
- [`appendices/child-architecture/`](appendices/child-architecture/) - Comprehensive architecture analysis and diagrams