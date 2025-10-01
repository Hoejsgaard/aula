# Task 011: ChildAgent Dependency Injection Refactoring

## Overview
Investigation and refactoring of ChildAgent dependency injection patterns to address architectural violations and incomplete child-centric migration.

## Initial Problem Analysis

### Service Locator Anti-Pattern
- **Issue**: ChildAgent accepts `IServiceProvider` as constructor parameter
- **Problem**: Creates hidden dependencies, makes unit testing difficult, violates SOLID principles
- **Evidence**: Multiple `_serviceProvider.GetRequiredService<T>()` calls throughout ChildAgent

### Interface Segregation Violation
- **Issue**: ChildAgent accepts entire `Config` object but only uses `Features` section
- **Problem**: Violates Interface Segregation Principle, creates unnecessary coupling

### Expert Agent Assessments
Both System Architect and Backend Engineer confirmed these as major violations of:
- Microsoft .NET DI Guidelines
- SOLID Principles (especially ISP and DIP)
- Testability best practices

## Critical Bugs Discovered

### 1. Incomplete Telegram Config Migration ❌
**Root Cause**: Legacy global Telegram configuration still exists after child-centric migration

**Evidence**:
```csharp
// In Config.cs - Should not exist
public Telegram Telegram { get; init; } = new();

// In TelegramInteractiveBot.cs - Uses global config
if (_config.Telegram.Enabled && !string.IsNullOrEmpty(_config.Telegram.Token))
_childrenByName = _config.MinUddannelse.Children.ToDictionary(...)

// In ChildAgent.cs - Expects per-child config
if (_child.Channels?.Telegram?.Enabled == true &&
    !string.IsNullOrEmpty(_child.Channels?.Telegram?.Token))
```

**Problem**:
- TelegramInteractiveBot uses global config pattern (legacy)
- ChildAgent expects per-child config pattern (new)
- Creates architectural conflict and potential bugs

### 2. Misplaced Timer Configuration ❌
**Root Cause**: Slack-specific timers stored in global Timers object

**Evidence**:
```csharp
// In ChildAwareSlackInteractiveBot.cs
int pollingInterval = _config.Timers.SlackPollingIntervalSeconds * 1000;
```

**Problem**: Violates separation of concerns - Slack-related config should be in Slack section

## Architectural Fix Plan

### Phase 1: Fix Configuration Structure Bugs
1. **Remove global Telegram class** from Config.cs
2. **Move SlackPollingIntervalSeconds** to ChildSlackConfig
3. **Audit other timer properties** for correct placement
4. **Remove top-level Timers class** if empty

### Phase 2: Fix TelegramInteractiveBot Architecture
1. **Refactor to child-aware pattern** like ChildAwareSlackInteractiveBot
2. **Accept ChildTelegramConfig** instead of global Config
3. **Remove global children iteration** (`_config.MinUddannelse.Children`)
4. **Update ChildAgent** to pass child-specific config

### Phase 3: Config Segregation
1. **ChildAgent**: Replace `Config` with `Features` only
2. **ChildAwareSlackInteractiveBot**: Use child's slack config directly
3. **Update Program.cs** service registration

### Phase 4: Service Locator Elimination
1. **ChildAgent**: Replace `IServiceProvider` with explicit dependencies
2. **Extract required services**: IChildAwareOpenAiService, IChildDataService, specific loggers
3. **Update constructors** to follow explicit DI pattern

## Implementation Priority
1. **Fix architectural bugs first** (Telegram/Timers config issues)
2. **Optimize DI patterns second** (Service Locator elimination)

## Expected Outcomes
- ✅ Proper child-centric architecture compliance
- ✅ Elimination of legacy global configuration patterns
- ✅ Improved testability and maintainability
- ✅ SOLID principle compliance
- ✅ Microsoft .NET DI guidelines adherence

## Testing Strategy
- Unit tests for each refactored component
- Integration tests to verify child-specific behavior
- Architectural tests to prevent regression

---
*Analysis completed by @architect and @backend expert agents*
*Task started: [Current Date]*