# Claude AI Context - Aula Family Assistant

This document provides operational context for Claude AI development. All code produced must meet production standards.

## ❌ STOP - MANDATORY PRE-RESPONSE CHECKLIST ❌

**BEFORE TYPING ANY RESPONSE:**
1. **READ** [@.claude/MANDATORY-READ-FIRST.md](.claude/MANDATORY-READ-FIRST.md) for protocol compliance
2. **REVIEW** [@.claude/workflows/WORKFLOW-ENFORCEMENT.md](.claude/workflows/WORKFLOW-ENFORCEMENT.md) for classification rules
3. **CLASSIFY** the user's message (Question/Task/Hybrid)
4. **FOLLOW** the prescribed workflow - NO EXCEPTIONS
5. **EVIDENCE**: Start response with timestamp and classification:
   ```
   [HH:MM:SS] [Workflow: Question|Task|Hybrid] [Agents: @name|None] [MCP: tool|None]
   ```

**VIOLATIONS = IMMEDIATE FAILURE. No "I think", no shortcuts, no skipping steps. INVESTIGATE before acting. TRACE execution paths. VALIDATE assumptions with code/docs.**

## Project Context

**Aula** is a .NET 9.0 console application that automates Danish school communications for families.

- **Core Function**: Fetch weekly letters from Aula/MinUddannelse, distribute via Slack/Telegram, provide AI assistance
- **Stack**: .NET 9.0, C#, Supabase PostgreSQL, OpenAI GPT-3.5-turbo
- **Authentication**: Per-child UniLogin authentication with SAML flow
- **Architecture**: Repository pattern, dependency injection, modern channel abstraction

## Essential Commands

```bash
# Build and test
dotnet build src/Aula.sln
dotnet test src/Aula.Tests
dotnet format src/Aula.sln

# Run application
cd src/Aula && dotnet run

# Generate coverage report
dotnet test src/Aula.Tests --collect:"XPlat Code Coverage"
```

**Critical Rule**: Never commit unless build, tests, and format pass without errors.

## Project Structure

```
src/Aula/
├── Agents/         # Per-child intelligent agents with event-driven architecture
├── Bots/           # Slack/Telegram bot implementations
├── Channels/       # Channel abstraction (IChannel, ChannelManager)
├── Configuration/  # Strongly-typed config with validation
├── Integration/    # External services (MinUddannelse, UniLogin SAML authentication)
├── Scheduling/     # Cron-based task scheduling with SchedulingService
├── Services/       # Core business logic with repositories
├── Utilities/      # Shared utility methods (WeekLetterUtilities)
└── Program.cs      # DI and startup configuration
```

## Recent Refactoring (Sprint 2 - October 2025)

**Authentication Consolidation** (Task 012):
- Unified 3 duplicate SAML authentication implementations → `UniLoginAuthenticatorBase`
- Extracted 11 duplicate utility methods → `WeekLetterUtilities`
- Broke down 274-line complex method → 80-line orchestration + 8 focused helpers
- **Result**: -174 lines (-16%), -70% cyclomatic complexity, 938 tests passing

**Architecture Improvements**:
- Template Method pattern for SAML flows
- Proper IDisposable implementation with `using` statements
- Input validation on utility methods
- Single Responsibility Principle applied throughout

## Workflows & Standards

### Core Workflows (@.claude/workflows/core/)
- `structured-development-cycle.md` - 5-phase development process
- `git-workflow.md` - Git rules, commit standards, mandatory review
- `architectural-principles.md` - Design patterns and principles
- `validation-standards.md` - Testing and documentation standards
- `code-quality-gates.md` - Build, test, and quality checkpoints
- `agent-coordination.md` - When to use which expert agent

## Expert Agents (@.claude/agents/)

**5 Specialized Agents** (all inherit MCP awareness from `base-agent.md`):
- `@architect` - System design, patterns, architecture decisions
- `@backend` - .NET, APIs, data layers, business logic
- `@infrastructure` - Terraform, Azure, deployment, pipelines
- `@security` - Authentication, multi-tenant isolation, compliance
- `@technical-writer` - Documentation, API specs, guides

## MCP Capabilities

Six MCP servers configured in `.mcp.json`:
1. **Serena** - Semantic code navigation and symbol editing
2. **Terraform** - Infrastructure documentation
3. **Context7** - Real-time library documentation
4. **Sequential-thinking** - Complex problem breakdown
5. **Claude-Reviewer** - Mandatory pre-commit code review
6. **GitHub** - Repository operations

## Testing Standards

### Unit Testing Rules
- **UNIT TESTS ONLY**: Use mocking and dependency injection
- **NO INTEGRATION TESTS**: Explicitly out of scope
- **NO REFLECTION**: Never use GetMethod, GetField, Invoke in tests
- **PUBLIC API ONLY**: Test only public methods and properties
- **CLEAR INTENT**: Test names describe what behavior is being verified

### Coverage Goals
- Current: 68.66% line coverage (1533 tests)
- Target: 75% coverage
- Focus on testable business logic, not database repositories

## Key Constraints

### NEVER Do These
- ❌ **PUSH TO REMOTE (git push) - FORBIDDEN! ONLY COMMIT LOCALLY!**
- ❌ **CHANGE REMOTE URLs (git remote set-url) - FORBIDDEN!**
- ❌ Merge to master locally (use GitHub PRs only)
- ❌ Start implementation without investigation phase
- ❌ Skip validation after changes
- ❌ Use "I think", "probably", "should work" (investigate and validate)
- ❌ Make assumptions without reading actual code
- ❌ Write POC-quality code (production standards only)

### ALWAYS Do These
- ✅ INVESTIGATE first: read existing code, trace execution paths
- ✅ Follow structured development workflow with mandatory investigation
- ✅ Request AI review before commits using MCP claude-reviewer
- ✅ Use expert agents for domain expertise
- ✅ Validate with 95%+ confidence backed by evidence
- ✅ Trace data flow end-to-end before implementing
- ✅ Render timestamp at start of every response
- ✅ Write production-grade code only

## Investigation & Validation Standards

**MANDATORY INVESTIGATION BEFORE ACTION:**
- ✅ Read existing code/config that relates to the task
- ✅ Trace execution paths and data flow end-to-end
- ✅ Check actual file contents, not assumptions
- ✅ Verify against official documentation
- ✅ State exact confidence levels with evidence
- ✅ Say "I haven't tested this" if uncertain
- ✅ Say "I failed" if something doesn't work
- ❌ No guesswork or "should work" statements
- ❌ No flattery or promotional language

## Documentation Standards

### Emoji Usage Rules
- **ONLY ❌, ✅, and ⚠️ emojis** allowed in ANY files (code, markdown, config, etc.)
- **Purpose**: Clear visual indicators for do/don't patterns
- **Usage**:
  - ❌ for prohibited actions, errors, incorrect patterns
  - ✅ for required actions, success states, correct patterns
  - ⚠️ for warnings, cautions, important notices
- **NO decorative emojis** in code, commit messages, or documentation

### Code Comment Rules
- **MINIMIZE COMMENTS**: Code should be self-explanatory through clear naming and structure
- **NO redundant comments**: Don't state what the code obviously does
- **NO XML documentation on public methods/classes**: Only add XML docs to public APIs (controllers, HTTP endpoints)
- **ACCEPTABLE comments**:
  - Document non-obvious side effects
  - Explain complex business logic or intent when not clear from code
  - TODO markers for future work (sparingly)
  - License headers or regulatory compliance notes
- **PREFER**: Refactoring code to be clearer over adding explanatory comments

## Configuration

Settings via `appsettings.json`:
- UniLogin credentials per child in `MinUddannelse.Children[]`
- Slack/Telegram tokens and channels
- OpenAI API key and model settings
- Supabase connection details

See existing `appsettings.json` for structure.

## Current Development Focus

### Working Features
- ✅ Per-child UniLogin authentication with SAML
- ✅ Week letter fetching and distribution
- ✅ Slack/Telegram bot integration
- ✅ AI-powered Q&A in Danish/English
- ✅ Smart reminder system
- ✅ Multi-child support

### High-Priority Tasks
1. **Intelligent Automatic Reminders**: Extract actionable items from week letters
2. **Default Schedule Initialization**: Auto-create weekly check schedule
3. **Human-Aware Retry Policy**: Smart retry for delayed week letters

---
*This document orchestrates references. Implementation details are in linked workflow files.*