# Task 003: Move Polling Frequency to Configuration

**Status:** PENDING
**Priority:** Medium
**Created:** 2025-01-24

## Problem
The scheduling service polling frequency is hardcoded to 10 seconds in SchedulingService.cs. This should be configurable to allow different environments and use cases.

## Current State
```csharp
// In SchedulingService.StartAsync()
await Task.Delay(TimeSpan.FromSeconds(10), _cancellationToken);
```

## Proposed Solution
Add polling configuration to appsettings.json:
```json
{
  "Scheduling": {
    "PollingIntervalSeconds": 10,
    "EnableScheduling": true
  }
}
```

## Implementation Steps
1. Add `ConfigScheduling` class with `PollingIntervalSeconds` property
2. Add `Scheduling` property to main `Config` class
3. Update SchedulingService constructor to accept configuration
4. Replace hardcoded `TimeSpan.FromSeconds(10)` with configured value
5. Add validation for minimum/maximum polling intervals
6. Update configuration validator
7. Add tests for new configuration

## Files to Modify
- src/Aula/Configuration/Config.cs (add ConfigScheduling class)
- src/Aula/Scheduling/SchedulingService.cs
- src/Aula/Configuration/ConfigurationValidator.cs
- appsettings.json
- src/Aula.Tests/Scheduling/SchedulingServiceTests.cs

## Configuration Validation Rules
- Minimum: 1 second (for testing)
- Maximum: 3600 seconds (1 hour)
- Default: 10 seconds
- Must be positive integer

## Testing
- Verify polling works with different intervals
- Test validation of invalid values
- Ensure no performance degradation with very short intervals
- Test graceful handling of configuration changes