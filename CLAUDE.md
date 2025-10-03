# Claude AI Context - MinUddannelse

This document provides operational context for Claude AI development. All code must meet production standards.

## ❌ MANDATORY PRE-RESPONSE PROTOCOL ❌

**BEFORE EVERY RESPONSE:**
1. **READ** [.claude/MANDATORY-READ-FIRST.md](.claude/MANDATORY-READ-FIRST.md)
2. **REVIEW** [.claude/workflows/WORKFLOW-ENFORCEMENT.md](.claude/workflows/WORKFLOW-ENFORCEMENT.md)
3. **CLASSIFY** the request (Question/Task/Hybrid)
4. **FOLLOW** the prescribed workflow
5. **RENDER** timestamp header: `[HH:MM:SS UTC] [Workflow: Type] [Agents: ...] [MCP: ...]`

**Violations = immediate failure. Investigate before acting. Validate assumptions.**

## Project Overview

**MinUddannelse** is a .NET 9.0 console application automating Danish school communications for families.

**Core Function:** Fetch weekly letters from MinUddannelse portal, distribute via Slack/Telegram, extract reminders, provide AI assistance, sync to Google Calendar.

**Stack:**
- .NET 9.0, C#
- UniLogin SAML authentication (Standard password and Pictogram login)
- OpenAI GPT-4o-mini for AI processing
- Supabase PostgreSQL for persistence
- Event-driven architecture with per-child agents

**Key Concepts:**
- Multi-child support with separate UniLogin credentials
- Per-child ChildAgent instances with event subscriptions
- Automated scheduling with cron expressions
- Smart reminder extraction from week letters
- Channel abstraction for Slack/Telegram distribution

## Essential Commands

```bash
# Build and test
dotnet build src/MinUddannelse.sln
dotnet test src/MinUddannelse.Tests
dotnet format src/MinUddannelse.sln

# Run application
cd src/MinUddannelse && dotnet run
```

**Critical Rule:** Never commit unless build, tests, and format pass.

## Project Structure

```
src/MinUddannelse/
├── Agents/           # ChildAgent - per-child event-driven agents
├── AI/               # OpenAI integration, prompts, reminder extraction
├── Bots/             # Slack/Telegram bot implementations
├── Client/           # MinUddannelseClient for portal API
├── Configuration/    # Strongly-typed config models
├── Content/          # Week letter models and utilities
├── Events/           # Event bus for agent coordination
├── GoogleCalendar/   # Google Calendar API integration
├── Models/           # Child, Message, Reminder domain models
├── Repositories/     # Supabase data access (messages, reminders, tasks)
├── Scheduling/       # Cron-based task scheduler
├── Security/         # UniLogin SAML authentication
└── Program.cs        # DI configuration and startup
```

## Workflows

**Location:** `.claude/workflows/`

Core workflows:
- `structured-development-cycle.md` - 5-phase development process
- `git-workflow.md` - Git rules, commit standards
- `code-quality-gates.md` - Build, test, quality gates
- `agent-coordination.md` - When to use which expert agent
- `WORKFLOW-ENFORCEMENT.md` - Response classification rules
- `MANDATORY-READ-FIRST.md` - Pre-response protocol

## Expert Agents

**Location:** `.claude/agents/`

All agents inherit from `base-agent.md`:
- `@architect` - System design, patterns, architecture decisions
- `@backend` - .NET, APIs, data layers, business logic
- `@infrastructure` - Terraform, Azure, deployment
- `@security` - Authentication, authorization, compliance
- `@technical-writer` - Documentation, API specs, guides

## MCP Capabilities

Configured in `.mcp.json`:
1. **serena** - Semantic code navigation and symbol editing
2. **context7** - Real-time library documentation
3. **terraform** - Infrastructure documentation
4. **sequential-thinking** - Complex problem breakdown
5. **claude-reviewer** - Mandatory pre-commit code review

## Testing Standards

**Unit Tests Only:**
- ❌ NO integration tests
- ❌ NO reflection (GetMethod, Invoke)
- ❌ NO testing private methods
- ✅ Mock dependencies
- ✅ Test public API only
- ✅ Clear test intent in names

**Test Project:** `src/MinUddannelse.Tests`

## Configuration

Settings in `appsettings.json` (see `appsettings.example.json`):

**Children:** Array of child configurations:
- UniLogin credentials (Username, Password OR PictogramSequence)
- Channel settings (Slack, Telegram, GoogleCalendar)
- Child metadata (FirstName, LastName, Colour)

**Services:**
- OpenAI API key and model settings
- Supabase connection (Url, Key, ServiceRoleKey)
- Google Service Account credentials

**Scheduling:**
- Cron intervals for week letter checks
- Reminder execution windows
- Retry policies

## Key Constraints

### NEVER
- ❌ **Push to remote** (git push forbidden - only local commits)
- ❌ **Change remote URLs** (git remote set-url forbidden)
- ❌ Merge to master locally (use GitHub PRs)
- ❌ Start implementation without investigation
- ❌ Use "I think", "probably", "should work"
- ❌ Make assumptions without reading code
- ❌ Skip validation after changes

### ALWAYS
- ✅ Investigate first: read code, trace execution
- ✅ Follow structured development workflow
- ✅ Request AI review before commits (mcp__claude-reviewer)
- ✅ Use expert agents for domain expertise
- ✅ Validate with 95%+ confidence backed by evidence
- ✅ Render timestamp at start of every response
- ✅ Write production-grade code only

## Documentation Standards

**Emoji Rules:**
- ONLY ❌, ✅, ⚠️ allowed in any files
- NO decorative emojis

**Code Comment Rules:**
- MINIMIZE comments - code should be self-explanatory
- NO redundant comments stating obvious code behavior
- NO XML documentation on public methods/classes
- ONLY comment non-obvious side effects or complex business logic

## Investigation Protocol

**MANDATORY BEFORE ACTION:**
- ✅ Read existing code/config related to task
- ✅ Trace execution paths end-to-end
- ✅ Check actual file contents, not assumptions
- ✅ Verify against official documentation
- ✅ State confidence levels with evidence
- ❌ No guesswork or "should work" statements

## Current Features

**Working:**
- ✅ Multi-child UniLogin authentication (Standard + Pictogram)
- ✅ Automated week letter fetching and distribution
- ✅ Slack/Telegram bot integration with interactive commands
- ✅ AI-powered reminder extraction from week letters
- ✅ Google Calendar event synchronization
- ✅ Event-driven ChildAgent architecture
- ✅ Cron-based scheduling system

**In Development:**
- Enhanced reminder parsing accuracy
- Improved retry strategies for delayed week letters
- Agent coordination refinements

---

**Reference:** This document orchestrates workflow and agent references. Implementation details are in linked files.
