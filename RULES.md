# Aula Project Rules

This document outlines the key development rules and practices for the Aula project.

## Logging

- Use ILoggerFactory injection instead of ILogger<T> in constructors
- Use LogInformation or higher (avoid LogDebug)
- Let middleware handle exceptions instead of try/catch->log patterns

## Code Style

- Favor clarity over brevity
- Use expressive names (e.g., minUddannelseClient instead of client)
- Avoid side effects - functions should do one ting
- Comment only when the "why" isn't obvious - never for the "what"
- No XML documentation or verbose comments

## Development Workflow

- Always run these commands after code changes:
  - `dotnet build`
  - `dotnet test`
  - `dotnet format`
- Do not commit changes unless all of the above pass
- Add missing tests before introducing new logic
- Fix all tests when refactoring interfaces

## Git Commit Guidelines

- Use semantic prefixes: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`
- Tell the story: why was this needed, what does it accomplish
- Be direct and professional, avoid fluff
- Use temporary file for commit messages

## Common Scopes

`unilogin`, `minuddannelse`, `aula`, `auth`, `api`, `secrets`, `infra`, `tests`
