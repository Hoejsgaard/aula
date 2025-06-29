# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build and Test
```bash
# Build the solution
dotnet build src/Aula.sln

# Run tests
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
- **src/Aula.Tests/**: Unit tests using xUnit and Moq
- **src/Aula.Api/**: Azure Functions API project (separate deployment)

### Core Components
- **Program.cs**: Entry point that configures DI, starts interactive bots, and optionally posts weekly letters on startup
- **AgentService**: Core service that handles Aula login and data retrieval via MinUddannelseClient
- **SlackInteractiveBot/TelegramInteractiveBot**: Interactive bots that answer questions about children's school activities using OpenAI
- **OpenAiService**: LLM integration for responding to user queries about school data
- **DataManager**: Manages children's data and weekly letters
- **ConversationContextManager**: Handles conversation context for interactive bots

### Key Integrations
- **Aula Platform**: Danish school communication system accessed via UniLogin authentication
- **Slack**: Webhook posting + interactive bot with Socket Mode
- **Telegram**: Bot API for posting + interactive conversations  
- **OpenAI**: GPT models for answering questions about school activities
- **Google Calendar**: Schedule integration via service account

### Configuration
Configuration is handled through `appsettings.json` with sections for:
- UniLogin credentials
- Slack (webhook URL, bot token, channel settings)
- Telegram (bot token, channel ID)
- Google Calendar (service account, calendar IDs per child)
- OpenAI API settings

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

## Target Framework
- .NET 9.0
- EnableNETAnalyzers and TreatWarningsAsErrors are enabled
- Tests use xUnit with Moq for mocking

## Current Development State (as of 2025-06-29)

### Recently Completed Code Quality Improvements
The codebase underwent a comprehensive cleanup focused on eliminating duplicate code and improving testability:

#### Shared Utilities Created
- **WeekLetterContentExtractor**: Static utility for extracting content from week letters (JObject/dynamic)
- **ReminderCommandHandler**: Consolidated ~220 lines of duplicate reminder code from both interactive bots
- **ConversationContextManager<TKey>**: Generic conversation context management with expiration logic
- **IMessageSender**: Interface abstraction with Slack/Telegram implementations

#### Refactoring Completed
- **OpenAiService.AskQuestionAboutWeekLetterAsync**: Decomposed 120+ line method into 13 focused functions
- **Dead code removal**: ~200+ lines removed from SlackInteractiveBot, TelegramInteractiveBot
- **Import cleanup**: Removed unused using statements across all files

#### Tests Added (202 total tests, up from 87)
- **WeekLetterContentExtractorTests**: 14 tests covering JSON/dynamic extraction, error handling
- **ConversationContextTests**: 10 tests covering properties, expiration, ToString formatting  
- **ConversationContextManagerTests**: 15 tests covering generic operations, lifecycle management
- **ReminderCommandHandlerTests**: 38 tests covering regex patterns, date parsing, child name extraction

### Next Priority: Refactoring Interactive Bots Before Testing

#### Issues Identified in TelegramInteractiveBot & SlackInteractiveBot
Both classes have code quality issues that make testing problematic:

1. **Duplicate ConversationContext classes** - Should use shared ConversationContextManager
2. **Manual conversation context management** - Duplicates functionality we already abstracted
3. **Long constructors with many dependencies** - Hard to mock and test
4. **Mixed responsibilities** - Bot management, conversation tracking, message handling combined

#### Recommended Refactoring Approach
1. **Remove duplicate ConversationContext classes** and use ConversationContextManager<long> for Telegram, ConversationContextManager<string> for Slack
2. **Extract message handling logic** into separate, testable classes
3. **Simplify constructors** by reducing direct dependencies
4. **Improve separation of concerns** between bot lifecycle and message processing

#### After Refactoring: Add Tests
Once refactored, create comprehensive tests for:
- **TelegramInteractiveBot**: Message handling, conversation context, command processing
- **SlackInteractiveBot**: Similar coverage plus Socket Mode polling logic  
- **OpenAiService**: Improve existing tests with proper HTTP client mocking

### Development Philosophy
- **Refactor first, test afterwards** - Never compromise code quality by forcing tests onto problematic code
- **Focus on shared utilities** - Extract common patterns before duplicating test code
- **Comprehensive test coverage** - Aim for edge cases, error handling, and proper mocking