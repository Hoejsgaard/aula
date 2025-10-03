# ❌ STOP - MANDATORY PRE-RESPONSE PROTOCOL ❌

**Audience**: Claude agents working on this invoicing-poc repository
**Purpose**: Enforce consistent workflow execution for all development tasks

## YOU MUST DO THIS BEFORE EVERY RESPONSE:

### 1. READ WORKFLOW-ENFORCEMENT.md
[@.claude/workflows/WORKFLOW-ENFORCEMENT.md](.claude/workflows/WORKFLOW-ENFORCEMENT.md)


### 2. CLASSIFY THE USER'S MESSAGE
- **Question** → Answer only
- **Task** → Follow ALL 5 phases
- **Hybrid** → Answer, then ask

### 3. FOR TASKS - USE AGENTS & MCP (MANDATORY)

**Reference**: See [MCP-SERVERS.md](docs/development/MCP-SERVERS.md) for complete tool definitions

**Infrastructure/Terraform Tasks:**
```
@infrastructure - Consult for ALL Terraform work  
mcp__terraform__* - Provider documentation tools
```

**Backend/.NET Tasks:**
```
@backend - Consult for ALL .NET work
mcp__context7__* - Library documentation tools  
```

**Security Tasks:**
```
@security - Consult for auth/security work
```

### 4. VALIDATION PROTOCOL
- Run `terraform validate` && `terraform fmt -check` for Terraform changes
- Run `dotnet build --nologo` && `dotnet test --nologo` for C# changes  
- Validate YAML syntax for pipeline changes
- Test locally BEFORE committing
- NO "I think" or "probably" statements
- 95%+ confidence required for production changes

## FAILURE CONSEQUENCES

If you skip this protocol:
- ❌ You WILL make trivial mistakes
- ❌ User WILL lose faith in you
- ❌ You WILL waste everyone's time

## EVIDENCE OF COMPLIANCE (INVOICING-POC REPOSITORY)

Start your response with:
```
[HH:MM:SS] [Workflow: Question/Task/Hybrid] [Agents: @xyz|None] [MCP: tool-name|None]
```

**Example:**
```bash
echo "[$(date '+%H:%M:%S')] [Workflow: Task] [Agents: @infrastructure] [MCP: terraform]"
```

This shows you followed the protocol AND provides timestamp for tracking when actions occurred.