# CLAUDE.md

This file provides guidance for Claude Code (claude.ai/code) when working with code in this repository.

---
## 🤖 AI ASSISTANCE NOTICE

**Significant portions of this codebase have been developed, improved, and maintained with assistance from Claude AI (claude.ai/code). This includes comprehensive test coverage improvements (813 tests), architectural modernization through 4 major phases, code quality enhancements, and maintenance of 68.66% line coverage. Claude has been instrumental in achieving the current A- architecture rating and maintaining high code quality standards.**

---

## Project Overview

**Aula** is a sophisticated .NET 9.0 console application that serves as an AI-powered family assistant for Danish school communications. It automates the fetching, processing, and distribution of school information while providing intelligent, interactive assistance to busy parents.

### Core Purpose
- **Automate School Communications**: Fetch weekly letters from Aula (Danish school platform)
- **Multi-Platform Distribution**: Post to Slack, Telegram with smart formatting
- **AI-Powered Assistant**: Answer questions about children's school activities in Danish/English
- **Smart Reminders**: Manual and automatic reminder system with natural language processing
- **Calendar Integration**: Google Calendar synchronization for family scheduling

## Commands

### Build and Test
```bash
# Build the solution
dotnet build src/Aula.sln

# Run tests (813 passing tests, 68.66% line coverage)
dotnet test src/Aula.Tests

# Format code
dotnet format src/Aula.sln

# Run the main application
cd src/Aula && dotnet run

# Run specific test
dotnet test src/Aula.Tests --filter "TestMethodName"
```

### Development Workflow
Always run these commands after code changes:
1. `dotnet build src/Aula.sln`
2. `dotnet test src/Aula.Tests`  
3. `dotnet format src/Aula.sln`

**Critical Rule**: Do not commit changes unless all commands pass without errors.

## Architecture Overview

### Project Structure
```
src/Aula/
├── Bots/           # Interactive bot implementations (Slack/Telegram)
├── Channels/       # Modern channel abstraction layer (IChannel, ChannelManager)
├── Configuration/  # Strongly-typed configuration with comprehensive validation
├── Integration/    # External service clients (MinUddannelse, Google Calendar, UniLogin)
├── Scheduling/     # Database-driven task scheduling with cron expression support
├── Services/       # Core business logic with repository pattern
├── Tools/          # AI tools management and function calling coordination
├── Utilities/      # Shared utilities, converters, and content extractors
└── Program.cs      # Clean startup configuration with dependency injection
```

### Core Components

#### **Modern Channel Architecture (Phase 4 ✅)**
- **IChannel Interface**: Platform-agnostic channel abstraction with capability system
- **ChannelManager**: Multi-channel coordination, broadcasting, and lifecycle management
- **Smart Formatting**: Auto-detection and conversion between markdown/HTML/platform-specific formats
- **Platform Implementations**: SlackChannel, TelegramChannel with feature-specific capabilities

#### **AI Integration (GPT-3.5-turbo)**
- **OpenAiService**: Cost-optimized LLM communication (~95% cost reduction from GPT-4)
- **ConversationManager**: Context preservation and conversation history management
- **PromptBuilder**: Template-based prompt construction for consistent interactions
- **AiToolsManager**: Function calling coordination for tool-augmented responses

#### **Database Layer (Repository Pattern)**
- **SupabaseService**: Clean orchestrator/facade for all database operations
- **Repository Pattern**: Specialized repositories (WeekLetterRepository, ReminderRepository, AppStateRepository, etc.)
- **Transaction Management**: Proper boundaries and error handling preservation

#### **Scheduling System**
- **Database-Driven**: Cron expression support with NCrontab integration
- **Real-Time Responsiveness**: 10-second polling for immediate reminder delivery
- **Retry Logic**: Configurable retry attempts with exponential backoff
- **Graceful Degradation**: Service continues operation despite individual task failures

### Key Integrations
- **Aula Platform**: Danish school communication system via MinUddannelse client
- **UniLogin**: Secure authentication for Danish educational services
- **Multi-Platform Messaging**: Slack (Socket Mode) + Telegram Bot API
- **OpenAI**: GPT-3.5-turbo for conversational AI with function calling
- **Google Calendar**: Service account integration for family scheduling
- **Supabase**: PostgreSQL database with comprehensive RLS security policies

## Configuration Architecture

### Strongly-Typed Configuration
Settings are handled through `appsettings.json` with comprehensive validation:

```json
{
  "UniLogin": {
    "Username": "your-username",
    "Password": "your-password"
  },
  "Slack": {
    "Enabled": true,
    "WebhookUrl": "https://hooks.slack.com/...",
    "ApiToken": "xoxb-...",
    "EnableInteractiveBot": true,
    "ChannelId": "#family"
  },
  "Telegram": {
    "Enabled": true,
    "Token": "bot-token",
    "ChannelId": "@family_channel"
  },
  "OpenAi": {
    "ApiKey": "sk-...",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 2000
  },
  "Supabase": {
    "Url": "https://project.supabase.co",
    "Key": "your-supabase-key"
  },
  "Features": {
    "UseMockData": false,
    "WeekLetterPreloading": true,
    "SeedHistoricalData": false
  },
  "MinUddannelse": {
    "Children": [
      {
        "FirstName": "Emma",
        "LastName": "Doe",
        "GoogleCalendarId": "calendar-id",
        "Color": "#FF5733"
      }
    ]
  }
}
```

### Configuration Validation
- **Startup Validation**: Comprehensive validation with clear error messages
- **Graceful Degradation**: Optional features log warnings instead of throwing errors
- **Smart Validation**: Only validates required properties when features are enabled
- **TimeProvider Injection**: Testable time-dependent validation for robust testing

## Code Style & Development Guidelines

### Testing Rules (1444 Tests, 60% Coverage → Target: 75%)
- **UNIT TESTS ONLY**: Only write unit tests that use mocking and dependency injection
- **NO INTEGRATION TESTS**: Integration tests are explicitly out of scope
- **NO DATABASE REPOSITORY TESTS**: Repository classes that depend on Supabase.Client or other database clients require integration testing and are explicitly excluded from unit testing
- **NO REFLECTION**: Never use reflection (GetMethod, GetField, Invoke, BindingFlags) in tests
- **PUBLIC API ONLY**: Test only public methods and properties
- **DEPENDENCY INJECTION**: Use constructor injection and mocking to isolate units under test
- **CLEAR INTENT**: Test names should clearly describe what behavior is being verified

### Test Coverage Improvement Plan (55% → 75% Target)
**Current Status**: 3,104/5,174 lines covered (60%) - Need +776 lines for 75%

**Recent Progress (2025-07-04)**:
- ✅ **TelegramChannel.cs**: Added 36 tests, covered 254 lines (0% → ~95%)
- ✅ **BotBase.cs**: Added 21 tests, covered 98 lines (0% → ~85%) 
- **Total Improvement**: +57 tests, +209 lines covered, +5% coverage

**Next Priority Targets**:
1. **SlackChannel.cs**: 20% coverage, ~198 uncovered lines
2. **MinUddannelseClient.cs**: 25% coverage, ~72 uncovered lines
3. **ConfigurationValidator.cs**: 45% coverage, ~224 uncovered lines
4. **Error Handling Paths**: Exception scenarios, validation failures
5. **Conditional Logic**: If/else branches, switch statements

**Coverage Commands**:
```bash
# Generate coverage
dotnet test src/Aula.Tests --collect:"XPlat Code Coverage"

# Generate HTML report
DOTNET_ROOT=/home/runeh/.dotnet ./tools/reportgenerator -reports:TestResults/*/coverage.cobertura.xml -targetdir:coverage -reporttypes:Html
```

### Logging Standards
- Use ILoggerFactory injection instead of ILogger<T> in constructors
- Use LogInformation or higher (avoid LogDebug)
- Let middleware handle exceptions instead of try/catch->log patterns
- Provide contextual information in log messages

### Code Quality Rules
- **Clarity Over Brevity**: Use expressive names (e.g., minUddannelseClient vs client)
- **Single Responsibility**: Functions should do one thing well
- **No Side Effects**: Avoid unexpected behaviors in methods
- **Minimal Comments**: Comment only when the "why" isn't obvious, never explain "what"
- **No XML Documentation**: Keep documentation focused and concise

### Git Commit Guidelines
- Use semantic prefixes: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`
- Common scopes: `unilogin`, `minuddannelse`, `aula`, `auth`, `api`, `scheduling`, `channels`, `tests`
- Write clear, concise commit messages that explain the "why"
- **IMPORTANT**: Do NOT add attribution comments in commits or code

## Current Development Status

### ✅ Fully Implemented Features
- **Week Letter Automation**: MinUddannelse integration with content extraction and multi-platform posting
- **Interactive AI Assistant**: Conversational interface supporting Danish/English with context preservation
- **Smart Reminder System**: Natural language reminder creation with 10-second responsiveness
- **Multi-Child Support**: Family-aware responses with name-based child lookups
- **Content Deduplication**: Hash-based tracking to prevent duplicate posts
- **Database Infrastructure**: Supabase integration with comprehensive security policies
- **Configuration Management**: Startup validation with graceful degradation
- **Channel Architecture**: Modern abstraction supporting easy addition of new platforms

### 🔧 Current Technical Debt
1. **Compiler Warnings**: 3 nullable reference type warnings in test code
2. **Missing Features**: No default schedule initialization for new setups

### 🚧 High-Priority Development Tasks

#### 1. Crown Jewel Feature: Intelligent Automatic Reminders (HIGH PRIORITY)
**Vision**: Extract actionable items from week letters and create automatic reminders
**Examples**:
- "Light backpack with food only" → Reminder night before
- "Bikes to destination X" → Morning reminder to check bike/helmet  
- "Special clothing needed" → Reminder to prepare items
- "Permission slip due" → Multiple reminders until completed

**Implementation Strategy**:
- Extend OpenAI integration with specialized reminder extraction prompts
- Create structured reminder templates for common school scenarios
- Add automatic reminder scheduling based on week letter content analysis
- Implement smart timing algorithms (night before, morning of, etc.)
- Add parent feedback loop for reminder effectiveness tuning

#### 2. Default Schedule Initialization (HIGH PRIORITY)
**Goal**: Auto-create "Sunday 4 PM" weekly letter check schedule if none exists
**Benefit**: Zero-configuration user experience for new installations
**Implementation**: Startup validation that creates default `WeeklyLetterCheck` task

#### 3. Human-Aware Retry Policy Enhancement (MEDIUM PRIORITY)
**Problem**: Current 1-hour linear retry doesn't match human publishing behavior
**Reality**: Week letters expected Sunday 4 PM but humans cause predictable delays:
- **Common**: 1–4 hours late (Sunday evening)
- **Occasional**: 6–18 hours late (Monday morning)
- **Rare**: 24–48 hours late (Tuesday)
- **Never**: >48 hours (week is over, no longer relevant)

**Smart Retry Strategy**:
```json
{
  "WeekLetterRetry": {
    "Phase1": { "hours": 6, "intervalMinutes": 60 },    // 0-6h: hourly
    "Phase2": { "hours": 18, "intervalHours": 3 },      // 6-24h: every 3h  
    "Phase3": { "hours": 24, "intervalHours": 6 },      // 24-48h: every 6h
    "MaxDelayHours": 48                                  // Stop Tuesday
  }
}
```

### 📋 Future Enhancements
- **Multi-Family Support**: User profiles and isolated configurations
- **Advanced Calendar Integration**: Two-way sync with conflict resolution
- **Observability**: Structured logging with correlation IDs and metrics
- **LLM Schedule Management**: Natural language schedule modifications
- **Enhanced Content Analysis**: More sophisticated week letter parsing

## Mock Data Support (Summer Testing)

### Problem Solved
During summer holidays, no fresh week letters are available, making development/testing difficult.

### Configuration-Driven Mock Mode
```json
{
  "Features": {
    "UseMockData": true,           // Enable mock mode
    "MockCurrentWeek": 19,         // Simulate week 19 as "current"
    "MockCurrentYear": 2025        // Simulate year 2025 as "current"
  }
}
```

**How it works**:
1. When `UseMockData: true`, MinUddannelseClient skips live API calls
2. Returns stored week letters from database for configured week/year
3. Application operates normally, unaware it's using historical data
4. Enables year-round development without requiring live MinUddannelse data

## Infrastructure & Hosting Considerations

### Critical Hosting Requirement
⚠️ **MinUddannelse IP Blocking**: Cannot host on major cloud providers (AWS, Azure, GCP) due to IP range blocking by Danish school systems.

**Acceptable Hosting Options**:
- Home networks with residential IP addresses
- Small VPS providers with non-major-cloud IP ranges
- Business networks with appropriate IP classifications

### Database Architecture (Supabase)
- **5 Production Tables**: All with comprehensive RLS security policies
- **Schema Validation**: Tables match documentation exactly
- **Security**: Production-ready Row Level Security implemented
- **Backup Strategy**: Automated Supabase backups with point-in-time recovery

## Target Framework & Dependencies

### Core Framework
- **.NET 9.0**: Latest LTS with modern C# features
- **Nullable Reference Types**: Enabled with strict analysis
- **Code Quality**: TreatWarningsAsErrors, EnableNETAnalyzers enabled

### Key Dependencies
- **OpenAI**: Betalgo.OpenAI (8.7.2) for GPT-3.5-turbo integration
- **Database**: Supabase (1.1.1) with PostgreSQL backend
- **Communication**: Slack.Webhooks, SlackAPI, Telegram.Bot (19.0.0)
- **Scheduling**: NCrontab (3.3.3) for cron expression parsing
- **Google**: Google.Apis.Calendar.v3 for calendar synchronization
- **Testing**: xUnit with Moq for comprehensive unit testing (812 tests)

## Architecture History & Quality Achievements

### Major Architectural Improvements Completed (2025-07-03)
- **✅ Phase 1**: Configuration enhancement and interface organization
- **✅ Phase 2**: Program.cs logic extraction and enhanced validation  
- **✅ Phase 3**: Circular dependency resolution and shared bot infrastructure
- **✅ Phase 4**: Service refactoring and channel architecture modernization

### Current Architecture Rating: A-
**Achieved through systematic debt elimination**:
- **Service Layer**: Repository pattern with clean separation of concerns
- **Channel Architecture**: Platform-agnostic abstraction with capability filtering
- **Test Quality**: 813 unit tests with proper mocking and dependency injection
- **Code Quality**: Comprehensive linting, nullable reference types, warning-as-errors

### Quality Metrics
- **Test Coverage**: 60% line coverage, 51% branch coverage  
- **Test Count**: 1,444 passing tests (98.6% success rate, 21 integration test failures)
- **Build Quality**: Zero compilation errors, only 3 nullable warnings
- **Architecture**: Clean dependency injection, proper abstractions

## Vision: The Perfect School Assistant

The end goal is an AI-powered family assistant that:
- **Automatically extracts and schedules reminders** from school communications
- **Provides intelligent, contextual responses** about children's activities
- **Seamlessly integrates** with family calendars and communication channels
- **Learns from family patterns** to provide increasingly helpful automation
- **Reduces mental load on parents** while ensuring nothing important is missed

This system should feel like having a highly organized, never-forgetting family assistant that understands the complexities of school life and family logistics.

## Getting Started

1. **Clone Repository**: `git clone <repository-url>`
2. **Install .NET 9.0**: Ensure latest .NET SDK is installed
3. **Configure Settings**: Copy `appsettings.example.json` to `appsettings.json` and configure
4. **Setup Database**: Create Supabase project and configure tables (see SUPABASE_SETUP.md)
5. **Run Tests**: `dotnet test src/Aula.Tests` to verify setup
6. **Build & Run**: `dotnet build src/Aula.sln && cd src/Aula && dotnet run`

For detailed setup instructions, see the docs/ folder (to be created).