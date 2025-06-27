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