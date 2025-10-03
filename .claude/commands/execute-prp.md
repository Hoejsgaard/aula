# Execute PRP

Execute a Product Requirements Prompt with intelligent agent selection, MCP tools, and mandatory review.

## PRP File: $ARGUMENTS

## Initial Context Loading

**MANDATORY**: Start by reading context to ensure awareness:
- Read `CLAUDE.md` for MCP capabilities and agent roster
- Read `.claude/workflows/core/structured-development-cycle.md` for workflow phases
- Understand available tools: Serena, Terraform, Context7, Sequential-thinking, Claude-Reviewer, GitHub (if configured)

## Branch Strategy

**IMPORTANT**: Always use feature branches for PRP execution to provide safety and review opportunities.

- **Feature branches for PRPs** - Create `feature/[prp-name]` branch before starting
- **Incremental commits** - Commit progress after each major task completion
- **Master protection** - Keep master stable during complex infrastructure work

**PRP Branch Workflow**:
```bash
# Start PRP execution
git checkout -b feature/[prp-name]
# Execute tasks, commit incremental progress
git add . && git commit -m "feat(scope): complete task N"
# Push when ready for review
git push -u origin feature/[prp-name]
# Merge when PRP complete and tested
git checkout master && git merge feature/[prp-name]
```

## Execution Process

### 1. Branch & Context
```bash
git checkout -b feature/[prp-name]
```
Load PRP and determine domain/technology scope.

### 2. Intelligent Planning
- **Complex PRPs**: Use `mcp__sequential-thinking` for multi-step breakdown
- **Agent selection**: Match PRP domain to lead agent:
  - Infrastructure → `@infrastructure`
  - Backend/API → `@backend`  
  - Security → `@security`
  - Full-stack → `@architect` coordinates
- **Research**: Use `mcp__serena` and `mcp__context7` for patterns/docs
- **Output**: TodoWrite with clear implementation steps

### 3. Execute
Follow structured workflow phases with selected agents.
Mark todos in_progress → completed in real-time.

### 4. Validate
Run appropriate validation until all pass:
- Terraform: `terraform validate && terraform fmt`
- .NET: `dotnet build && dotnet test`
- Scripts/Pipelines: Syntax checks

### 5. Review Loop (MANDATORY)
```bash
git add -A
```
- Request: `mcp__claude-reviewer__request_review` with summary
- Auto-fix critical/major issues
- Iterate until approved (max 3 rounds)
- Include review ID in completion report

### 6. Documentation Update
- Use `mcp__context7` to review existing docs
- Update with `@technical-writer`:
  - API changes → OpenAPI specs
  - Architecture changes → ADRs
  - New features → README sections
  - Configuration → Setup guides

### 7. Complete
Verify all tasks complete, review passed, docs updated.

### 8. Archive
Move `PRPs/{prp-name}.md` → `PRPs/done/{prp-name}.md`
Update `PRPs/done/README.md` with summary + review ID.

## Quick Reference

**Agent Selection**:
- Microservice → `@backend` leads
- Infrastructure → `@infrastructure` leads
- Security/Auth → `@security` leads
- Architecture → `@architect` coordinates
- Docs → `@technical-writer` leads

**MCP Tools** (used automatically):
- `mcp__serena` - Code navigation/editing
- `mcp__context7` - Library docs & updates
- `mcp__sequential-thinking` - Complex planning
- `mcp__claude-reviewer` - Mandatory review
- `mcp__terraform` - Infrastructure docs
- `mcp__github` - PR/issue management (optional)

**Success Criteria**:
✅ All validation passes
✅ Review approved (or waived with reason)
✅ Documentation updated
✅ PRP archived with summary