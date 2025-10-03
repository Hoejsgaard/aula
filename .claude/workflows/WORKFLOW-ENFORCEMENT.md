# Workflow Enforcement - MANDATORY

**Scope**: Classification rules for all Claude agent responses in this repository.

## RESPONSE FORMAT - MANDATORY

**Every response MUST start with:**
First get the timestamp.
```
[HH:MM:SS UTC] [Workflow: Question|Task|Hybrid] [Agents: @name|None] [MCP: tool-name|None]
```

**Example formats:**
- `[14:15:42 UTC] [Workflow: Question] [Agents: None] [MCP: None]`
- `[09:30:15 UTC] [Workflow: Task] [Agents: @infrastructure] [MCP: terraform]`
- `[11:45:23 UTC] [Workflow: Hybrid] [Agents: @backend, @security] [MCP: context7]`

## MANDATORY PROCESS - BEFORE EVERY RESPONSE:

### 1. RENDER TIMESTAMP (MANDATORY)
**FIRST ACTION**: Always start response with timestamp header
**Format**: `[HH:MM:SS UTC] [Workflow: Type] [Agents: ...] [MCP: ...]`
**No exceptions**: Every response must begin with this header

### 2. CLASSIFY & ACT

#### Question?
**Indicators**: "how", "what", "why", "where", "explain"
**Action**: INVESTIGATE first, then answer. Don't implement.
**Process**: Read relevant code/docs before answering

#### Task?
**Indicators**: "fix", "implement", "create", "deploy", "build"
**Action**: Follow structured-development-cycle.md with mandatory investigation
**Process**: Phase 1 (Understand) → Phase 2 (Plan) → Phase 3 (Implement)

#### Hybrid?
**Indicators**: "How do we make X work?"
**Action**: INVESTIGATE, answer the question, then ask if they want implementation
**Process**: Research first, explain findings, offer implementation

## TIMESTAMP ENFORCEMENT

**CRITICAL**: Every response must start with timestamp header.

**Implementation**:
```bash
echo "[$(date -u '+%H:%M:%S') UTC] [Workflow: Type] [Agents: ...] [MCP: ...]"
```

**Failure to include timestamp = Process violation**

## PROCESS SUMMARY

1. **ALWAYS**: Render timestamp header first
2. **Questions**: Investigate, then answer
3. **Tasks**: Follow full 5-phase structured development cycle
4. **Hybrid**: Investigate, answer, offer implementation

**No shortcuts. No exceptions. Production standards only.**

**Reference**: [Structured Development Cycle](.claude/workflows/core/structured-development-cycle.md) for detailed implementation workflow.