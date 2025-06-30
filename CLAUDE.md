# CLAUDE.md

This file provides guidance for Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build and Test
```bash
# Build the solution
dotnet build src/Aula.sln

# Run tests (582 tests, 53% line coverage, 43% branch coverage)
dotnet test src/Aula.Tests

# Format code
dotnet format src/Aula.sln

# Run the main application
cd src/Aula && dotnet run

# Run specific test
dotnet test src/Aula.Tests --filter "TestMethodName"
```

### Development Workflow
Always run these commands after code changes (per RULES.md):
1. `dotnet build src/Aula.sln`
2. `dotnet test src/Aula.Tests`  
3. `dotnet format src/Aula.sln`

Do not commit changes unless all commands pass.

## Architecture

### Project Structure
- **src/Aula/**: Main console application - fetches data from Aula (Danish school platform) and posts to Slack/Telegram
- **src/Aula.Tests/**: Unit tests using xUnit and Moq (582 tests, 53% line coverage)
- **src/Aula.Api/**: Azure Functions API project (separate deployment)

### Core Components
- **Program.cs**: Entry point that configures DI, starts interactive bots, and optionally posts weekly letters on startup
- **AgentService**: Core service that handles Aula login and data retrieval via MinUddannelseClient
- **SlackInteractiveBot/TelegramInteractiveBot**: Interactive bots that answer questions about children's school activities using OpenAI
- **OpenAiService**: LLM integration for responding to user queries about school data (switched to gpt-3.5-turbo for cost optimization)
- **DataManager**: Manages children's data and weekly letters with 1-hour memory cache
- **ConversationContextManager**: Handles conversation context for interactive bots
- **SchedulingService**: Database-driven task scheduling with cron expressions

### Key Integrations
- **Aula Platform**: Danish school communication system accessed via UniLogin authentication
- **Slack**: Webhook posting + interactive bot with Socket Mode (5-second polling)
- **Telegram**: Bot API for posting + interactive conversations  
- **OpenAI**: GPT-3.5-turbo for answering questions about school activities (cost-optimized)
- **Google Calendar**: Schedule integration via service account
- **Supabase**: PostgreSQL database for reminders, posted letters, scheduling, and app state

### Configuration
Configuration is handled through `appsettings.json` with sections for:
- UniLogin credentials
- Slack (webhook URL, bot token, channel settings)
- Telegram (bot token, channel ID)
- Google Calendar (service account, calendar IDs per child)
- OpenAI API settings (gpt-3.5-turbo, 2000 max tokens)
- Supabase database connection
- Features (preloading, parallel processing, caching)
- Timers (polling intervals, cleanup schedules)

## Code Style Rules (from RULES.md)

### Logging
- Use ILoggerFactory injection instead of ILogger<T> in constructors
- Use LogInformation or higher (avoid LogDebug)
- Let middleware handle exceptions instead of try/catch->log patterns

### Code Style
- Favor clarity over brevity
- Use expressive names (e.g., minUddannelseClient instead of client)
- Avoid side effects - functions should do one thing
- Comment only when the "why" isn't obvious - never for the "what"
- No XML documentation or verbose comments

### Git Commits
- Use semantic prefixes: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`
- Common scopes: `unilogin`, `minuddannelse`, `aula`, `auth`, `api`, `secrets`, `infra`, `tests`
- Do NOT add "🤖 Generated with Claude Code" or similar attribution comments

## Target Framework
- .NET 9.0
- EnableNETAnalyzers and TreatWarningsAsErrors are enabled
- Tests use xUnit with Moq for mocking

## Current Development Roadmap (2025-06-30)

### Recently Completed (2025-06-30)
✅ **Code Quality Improvements**: Eliminated duplicate code, improved testability
✅ **Shared Utilities**: WeekLetterContentExtractor, ReminderCommandHandler, ConversationContextManager
✅ **OpenAI Cost Optimization**: Switched from GPT-4 to GPT-3.5-turbo (~95% cost reduction)
✅ **Test Coverage**: Grew from 87 to 567 tests with comprehensive utility testing
✅ **Test Coverage Analysis**: Detailed analysis completed - 50.18% line coverage, 42.18% branch coverage, identified critical gaps and realistic 75% target
✅ **Phase 1 Test Coverage**: Completed - 50.18% → 52.78% line coverage, 582 tests, Program.cs 0% → 95%, AulaClient & GoogleCalendar now tested

### Priority Development Tasks

#### 1. Test Coverage Improvement (HIGH PRIORITY)
**Current State**: 582 tests, 52.78% line coverage, 42.96% branch coverage
**Goals**: Reach 75% line coverage / 65% branch coverage through 3-phase approach

**✅ Phase 1 Completed** (Target: 65% overall):
- ✅ Program.cs: 0% → 95% coverage - startup logic & service registration tested
- ✅ AulaClient: 0% → tested - constructor validation, error handling
- ✅ GoogleCalendar: 0% → tested - parameter validation, Google API integration
- **Result**: 50.18% → 52.78% line coverage (+2.6pp), 567 → 582 tests

**Phase 2 Action Items** (Target: 70% overall):
- Improve SlackInteractiveBot message processing coverage (21% → 60%)
- Enhance integration layer testing (UniLogin, MinUddannelse, Agent services)
- Add comprehensive error handling and edge case testing

**Phase 3 Action Items** (Target: 75% overall):
- Polish OpenAiService coverage (60% → 75%)
- Improve AiToolsManager tool coordination testing (35% → 60%)
- Add channel abstraction testing (message senders)

**Remaining Critical Gaps**:
- SlackInteractiveBot (21% coverage) - Main interactive functionality
- Integration layer (27-37% coverage) - UniLoginClient, MinUddannelseClient, AgentService

#### 2. Week Letter Automation Enhancement (HIGH PRIORITY)
**Current State**: Weekly fetching Sundays at 4 PM, basic scheduling, retry logic
**Goals**:
- Implement smart default scheduling (Sunday 4 PM local time if none configured)
- Add CRUD operations for schedule management via LLM
- Enhance timing configuration and timezone handling

**Action Items**:
- Add startup schedule initialization if none exists
- Implement LLM-powered schedule modification commands
- Add timezone-aware scheduling configuration
- Improve retry logic with exponential backoff

#### 3. Crown Jewel Feature: Intelligent Automatic Reminders (HIGH PRIORITY)
**Vision**: Extract actionable items from week letters and create automatic reminders
**Examples**:
- "Light backpack with food only" → Reminder night before
- "Bikes to destination X" → Morning reminder to check bike/helmet
- "Special clothing needed" → Reminder to prepare items
- "Permission slip due" → Multiple reminders until completed

**Implementation Plan**:
- Extend OpenAI integration with specialized reminder extraction prompts
- Create structured reminder templates for common school scenarios
- Add automatic reminder scheduling based on week letter content
- Implement smart timing (night before, morning of, etc.)
- Add parent feedback loop for reminder effectiveness

#### 4. Channel Architecture Modernization (MEDIUM PRIORITY)
**Current State**: Hardcoded Slack and Telegram implementations
**Goals**:
- Abstract channels as configurable set rather than two hardcoded options
- Enable easy addition of new channels (Discord, Teams, email, etc.)
- Standardize message handling and formatting

**Action Items**:
- Create IChannel interface with standardized methods
- Implement ChannelManager for multi-channel coordination
- Refactor configuration to support dynamic channel sets
- Abstract message formatting and interactive capabilities

#### 5. Configuration Restructuring (MEDIUM PRIORITY)
**Goals**:
- Move children under MinUddannelse configuration section
- Improve configuration validation and error handling
- Add configuration migration support

**Action Items**:
- Restructure appsettings.json schema
- Move `Children` array under `MinUddannelse` section  
- Add configuration validation at startup
- Update configuration classes and dependency injection

#### 6. Calendar Integration Testing & Enhancement (MEDIUM PRIORITY)
**Current State**: Google Calendar integration present but untested since refactoring
**Goals**:
- Verify calendar functionality works with current architecture
- Enhance calendar integration with reminder synchronization
- Update documentation and configuration examples

**Action Items**:
- Test current Google Calendar service integration
- Add calendar sync for automatic reminders
- Create calendar integration tests
- Document setup process and troubleshooting

#### 7. Documentation & Infrastructure (LOW PRIORITY)
**Goals**:
- Update Supabase documentation to match current schema
- Improve setup documentation and examples
- Add architecture diagrams and flow charts

**Action Items**:
- Document current Supabase table schema and usage
- Create setup guide for new developers
- Add troubleshooting guide for common issues
- Document the crown jewel automatic reminder feature

## Development Philosophy

### Testing Strategy
- **Refactor first, test afterwards** - Never compromise code quality by forcing tests onto problematic code
- **Focus on shared utilities** - Extract common patterns before duplicating test code
- **Comprehensive test coverage** - Aim for edge cases, error handling, and proper mocking
- **Integration testing** - Test critical paths end-to-end

### Architecture Principles
- **Shared abstractions** - Use interfaces and dependency injection consistently
- **Configuration-driven** - Make behavior configurable rather than hardcoded
- **Fail gracefully** - Handle external service failures without crashing
- **Cost-conscious** - Optimize expensive operations (OpenAI calls, database queries)

### AI Integration Best Practices
- **Cost optimization** - Use appropriate model for task complexity
- **Token management** - Implement conversation trimming and caching
- **Multi-language** - Support both English and Danish interactions
- **Structured prompts** - Use templated, efficient prompt construction
- **Context preservation** - Maintain conversation history intelligently

## Current Feature Status

### ✅ Fully Implemented
- Week letter fetching and posting to channels
- Interactive Q&A about school activities
- Manual reminder commands (add/list/delete)
- Conversation context management
- Multi-child support with name-based lookups
- Content deduplication and caching
- Database-driven scheduling
- Cost-optimized OpenAI integration

### 🚧 In Progress  
- Test coverage improvements
- Interactive bot refactoring for better testability

### 📋 Planned
- Intelligent automatic reminder extraction from week letters
- Channel architecture abstraction
- Configuration restructuring
- Enhanced calendar integration
- Comprehensive documentation updates

### 🎯 Vision: The Perfect School Assistant
The end goal is an AI-powered family assistant that:
- Automatically extracts and schedules reminders from school communications
- Provides intelligent, contextual responses about children's activities
- Seamlessly integrates with family calendars and communication channels
- Learns from family patterns to provide increasingly helpful automation
- Reduces mental load on parents while ensuring nothing important is missed

This system should feel like having a highly organized, never-forgetting family assistant that understands the complexities of school life and family logistics.