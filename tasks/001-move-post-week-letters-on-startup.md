# Task 001: Move PostWeekLettersOnStartup Configuration

**Status:** COMPLETED ✅
**Priority:** Medium
**Created:** 2025-01-24
**Completed:** 2025-01-24

## Problem
The `PostWeekLettersOnStartup` configuration option is currently duplicated in both Slack and Telegram configuration sections, which violates DRY principles and makes configuration confusing.

## Current State
```json
{
  "Slack": {
    "PostWeekLettersOnStartup": false
  },
  "Telegram": {
    "PostWeekLettersOnStartup": false
  }
}
```

## Proposed Solution
Move this to a general `Features` configuration section:
```json
{
  "Features": {
    "PostWeekLettersOnStartup": false
  }
}
```

## Implementation Steps
1. Add `PostWeekLettersOnStartup` to `ConfigFeatures` class
2. Update Program.cs to read from `Features` section instead of individual channel configs
3. Remove the property from `ConfigSlack` and `ConfigTelegram` classes
4. Update configuration validation
5. Update appsettings.json template
6. Run tests to ensure no breaking changes

## Files to Modify
- src/Aula/Configuration/Config.cs
- src/Aula/Program.cs (lines 109-130)
- appsettings.json
- src/Aula.Tests/Configuration/ConfigurationValidatorTests.cs

## Testing
- Verify application starts correctly with new configuration
- Ensure week letters are posted on startup when enabled
- Test that both Slack and Telegram receive posts