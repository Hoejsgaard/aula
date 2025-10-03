# Structured Development Cycle

## Purpose
This workflow enforces disciplined development that prevents rushing to implementation and ensures thorough understanding, planning, and quality validation at each step.

## Core Principle
**QUESTIONS GET ANSWERED FIRST. UNDERSTANDING BEFORE ACTION.**

Never start implementing until you fully understand the problem, have researched the solution space, and created a clear plan with proper agent delegation.

## Workflow Phases

### Phase 1: UNDERSTAND & ANALYZE
**When user asks a question or requests work:**

1. **STOP and ANALYZE** the request first
2. **Answer questions** if that's what was asked - DO NOT implement
3. **Ask clarifying questions** if the request is unclear
4. **Research the codebase** to understand current state
5. **Identify the problem scope** and dependencies

**MANDATORY CHECKPOINT - Before proceeding:**
❓ **Did I check existing configuration?** (tfvars, settings, module variables)
❓ **Did I read the actual code that created the failing resource?**
❓ **Did I trace the dependency chain?** (what creates what, what configures what)
❓ **Did I check documentation for the failing component?**
If ANY answer is NO → STOP and do it first

**Required Agent Consultation:**
- `@architect` - Core principles and architectural constraints
- `@backend` - Domain logic and business rules as needed

**Quality Gate:** ✅ User confirms understanding is correct before proceeding

---

### Phase 2: DEEP THINK & PLAN  
**Only after understanding is confirmed:**

1. **MANDATORY MCP/AGENT CONSULTATION** (see [MCP-SERVERS.md](../../docs/development/MCP-SERVERS.md)):
   - **Terraform/Infrastructure** → MUST use `@infrastructure` agent + Terraform MCP docs
   - **.NET/Backend** → MUST use `@backend` agent + Context7 MCP for library docs
   - **Security** → MUST use `@security` agent
   - **NO EXCEPTIONS** - Even if you think you know the answer

2. **TRACE EXISTING CODE** - Read and understand current implementations, don't guess
3. **VALIDATE ALL ASSUMPTIONS** - Question every assumption, prove it with code/docs
4. **ASSUMPTION VERIFICATION** - For each assumption: "How would I prove this is true?"
5. **OFFICIAL DOCS CHECK** - Verify tool/API behavior against vendor documentation  
6. **Design the solution** with clear technical approach
7. **HAND-RUN THE ENTIRE FLOW** - Mentally execute every step of the solution
8. **EXPECTED OUTPUT PREDICTION** - What exactly should logs/output show if this works?
9. **Break down into implementable steps** 
10. **Identify required agents** for each step
11. **Plan testing and validation approach**
12. **Consider edge cases and failure modes**
13. **ASK CRITICAL QUESTIONS** - What could go wrong? What am I missing?
14. **TRIVIAL FAILURE PREVENTION** - What simple assumptions could cause hours of debugging?

**Quality Gate:** ✅ Present plan to user with CONFIDENCE LEVEL (95%+ required) and evidence for all assumptions

---

### Phase 3: IMPLEMENT WITH DISCIPLINE
**Only after plan approval:**

1. **Use TodoWrite** to track implementation steps
2. **Work through plan step-by-step** - no shortcuts
3. **Use correct specialized agents** for each step:
   - `@backend` - Controllers, APIs, repository patterns, and data layers
   - `@infrastructure` - Azure services, Terraform, and deployment
   - `@security` - Authentication, authorization, and compliance
   - `@architect` - System design and architectural decisions
4. **Mark todos as in_progress** before starting each step
5. **Mark todos as completed** immediately after finishing each step

**AUTOMATIC REVIEW TRIGGERS:**
- After running `/fix-coderabbit-feedback` command and implementing all fixes
- After completing multi-step implementation tasks
- When fixing CodeRabbit or external review feedback
- Before any commits to the repository
- After significant refactoring or architecture changes

**MANDATORY Validation After Each Step:**
- ✅ **Terraform changes** → Run `terraform plan` WITH REAL VALUES IMMEDIATELY
  - ❌ NOT just `terraform validate` - that's only syntax checking!
  - ✅ MUST use real tokens from temp/credentials
  - ✅ MUST pass actual namespace and environment values  
  - ✅ MUST see plan complete without errors before claiming it works
- ✅ **C# code changes** → Run `dotnet build` IMMEDIATELY
- ✅ **Pipeline YAML changes** → Verify syntax IMMEDIATELY
- ✅ **Tests must pass** → Run `dotnet test` if tests exist
- ✅ **MANDATORY FLOW TRACING** → For interconnected systems (pipelines, artifacts, deployments):
  - **TRACE THE ENTIRE DATA FLOW** → What creates what? What consumes what? Where does each file/artifact live?
  - **HAND-RUN EVERY STEP** → Mentally execute: "Build puts X at location Y, Deploy expects Z at location W"
  - **VERIFY FILE PATHS** → Check actual vs expected paths with concrete examples
  - **NO PATH ASSUMPTIONS** → Every file path must be traced from source to destination
- ✅ **NO GUESSWORK** → Every assumption must be validated with code/documentation
- ✅ **ASSUMPTION DOUBLE-CHECK** → For every assumption, ask "How would I verify this is actually true?"
- ✅ **EXPECTED vs ACTUAL** → What should happen vs what would logs/output show if it worked?
- ✅ **MICROSOFT DOCS CHECK** → Verify API/tool behavior against official documentation
- ✅ Code follows established patterns
- ✅ Update tests if logic changed

**VALIDATION IS NOT OPTIONAL. If you cannot validate, explicitly state why.
ENTERPRISE QUALITY STANDARD: No "I think", "probably", "should work" statements allowed.
DEBUGGING PREVENTION: Double-check assumptions BEFORE implementation, not after failure.**

---

### Phase 4: REVIEW & VALIDATE
**After implementation is complete:**

1. **VALIDATE EVERYTHING (MANDATORY):**
   - **Terraform** → `terraform validate` must pass
   - **C# code** → `dotnet build` with no warnings/errors
   - **All tests** → `dotnet test` must be green
   - **Pipeline YAML** → Syntax must be valid
   - Code follows established patterns from examples/templates
   - Multi-tenant requirements satisfied
   - Security considerations addressed

2. **Self-review for quality:**
   - Code reads like a story (clear, linear, obvious intent)
   - Clean code over clever code
   - Set-based operations only
   - Dependency injection patterns followed
   - XML documentation on public methods

3. **AI CODE REVIEW (AUTOMATIC - USING CLAUDE REVIEWER MCP):**
   - **Stage changes safely**: Automatic for review purposes, request confirmation only before permanent commits
   - **Request review automatically**: Using `mcp__claude-reviewer__request_review` tool
     - Generate summary of all changes made
     - Focus on security, performance, architecture
     - Run detected test commands:
       - Terraform: `terraform fmt -check`, `terraform validate`, `terraform plan` (never apply)
       - .NET: `dotnet build --nologo`, `dotnet test --nologo`
       - Scripts: `bash -n`, `shellcheck` validation
   - **Fix issues systematically**:
     - Critical issues: Fixed immediately  
     - Major issues: Fixed unless conflicting with requirements
     - Minor issues: Fixed if time permits
   - **Request follow-up reviews**: Continue until approved (max 3 rounds)
   - **Report status**: Display final review results with review ID
   - **Mark complete**: When review passes or user provides override
   - **Manual override**: Skip only with explicit phrase `skip review: <reason>`

4. **Enterprise confidence validation:**
   - **CONFIDENCE LEVEL**: State exact percentage (95%+ required for production)
   - **EVIDENCE**: Document specific evidence supporting confidence:
     - Test results: Link to test run artifacts
     - Code traces: Reference specific files and line numbers
     - Assumption validation: List each assumption with verification method
   - **TRACE RECORDS**: Store validation evidence in `.artifacts/validation/<date>/`
   - **EVIDENCE**: What specific evidence supports your confidence?
   - **TRACE VALIDATION**: Have you traced the entire execution path?
   - **ASSUMPTION VALIDATION**: Every assumption proven with code/docs?
   - Have you tested edge cases?
   - Does it follow all architectural principles?
   - Is the code maintainable by a mid-level developer?

**Quality Gate:** ✅ Only proceed with 95%+ confidence backed by traceable evidence AND code review complete
   - After 3 failed review attempts, escalate to user for manual intervention
   - Override requires:
     - Explicit phrase: `skip review: <reason>` with justification
     - Record waiver in `.reviews/sessions/<review-id>/waiver.md`
     - Not allowed when Critical severity issues remain open
   - All review results and waivers stored in `.reviews/sessions/` for audit trail

---

### Phase 5: COMPLETION & HANDOFF
**Before marking work complete:**

1. **Update documentation** if needed (README, task status)
2. **Create clear commit messages** following semantic versioning
3. **Provide concise summary** of what was accomplished
4. **No promotional content** - focus on technical facts
5. **Ask if user wants anything adjusted** before considering complete

## Workflow Violations - NEVER DO THESE:

❌ **Never start coding immediately** when asked a question  
❌ **Never skip the planning phase** and jump to implementation  
❌ **Never ignore quality gates** (build, test, review)  
❌ **Never use wrong agents** for specialized work  
❌ **Never mark tasks complete** with failing tests or builds  
❌ **Never commit without testing** the changes first  
❌ **Never assume requirements** - ask clarifying questions  

## Agent Delegation Rules

**Always use the right agent for the job:**

- **System design & architecture** → `@architect`
- **API & backend development** → `@backend`
- **Infrastructure & DevOps** → `@infrastructure`
- **Security & compliance** → `@security`
- **Documentation** → `@technical-writer`

**Note:** All agents inherit from `base-agent` for consistent MCP awareness and project context.

## Emergency Brake

If you find yourself jumping to implementation without following this workflow:
1. **STOP immediately**
2. **Go back to Phase 1** (Understand & Analyze)
3. **Follow the workflow properly**

This workflow is mandatory for all development tasks. No exceptions.