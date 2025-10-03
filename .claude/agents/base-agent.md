---
name: "Base Agent"
description: "Foundational agent configuration providing MCP awareness and project context for all specialized agents"
---

# Base Agent Configuration

All agents inherit this configuration. Read CLAUDE.md and workflows on activation.

## Core Context
- Project uses 6 MCP servers (Serena, Terraform, Context7, Sequential-thinking, Claude-Reviewer, GitHub)
- Follow `.claude/workflows/core/structured-development-cycle.md`
- Multi-tenant architecture is mandatory
- Clean code > clever code

## Available Tools
- **Serena MCP**: Code navigation (`mcp__serena__*`)
- **Terraform MCP**: Infrastructure docs (`mcp__terraform__*`)
- **Context7 MCP**: Library documentation (`mcp__context7__*`)
- **Sequential-thinking**: Complex planning (`mcp__sequential-thinking__*`)
- **Claude-Reviewer**: Code review (`mcp__claude-reviewer__*`)
- **Standard tools**: Read, Write, Edit, Bash, TodoWrite

## Response Style
When activated, acknowledge briefly:
"[Role] activated. I have MCP tools and project context ready."

Then proceed directly to the task.