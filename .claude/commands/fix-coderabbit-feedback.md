# Fix CodeRabbit Feedback Command

All work must be done with careful analysis. Be critical of anything that looks non-trivial or architectural. 

## Purpose
Systematically address **ALL** CodeRabbit feedback with **QUALITY OVER SPEED** focus.

## ⚠️ CRITICAL REQUIREMENTS
1. **CACHE CLEARING**: Script clears `.artifacts/` by default (Set `CR_CLEAN_ARTIFACTS=0` to preserve for debugging only)
2. **FIX EVERYTHING**: 100% of items - actionable, nitpicks, suggestions - ALL OF THEM
3. **QUALITY FOCUS**: Thorough understanding and complete fixes, NOT quick patches
4. **ITERATIVE COMMITS**: Commit after each category with MCP review
5. **WORKFLOW RELOAD**: Re-read @CLAUDE.md & workflows between categories

## EXECUTION MODES
```bash
./scripts/fix-coderabbit-feedback.sh        # DEFAULT: Latest review only
./scripts/fix-coderabbit-feedback.sh all    # ALL: Every review in PR
```
**BOTH modes are critical - preserve default/all behavior**

## SCOPE: FIX ABSOLUTELY EVERYTHING
**ZERO TOLERANCE POLICY:**
- ✅ Actionable items → FIX THEM ALL
- ✅ Nitpicks → FIX THEM ALL  
- ✅ Style suggestions → FIX THEM ALL
- ✅ "Consider doing X" → DO IT
- ✅ "Maybe try Y" → TRY IT
- ✅ Optional improvements → IMPLEMENT THEM
**If CodeRabbit mentioned it, you fix it. No exceptions. No skipping. EVERYTHING.**

## MANDATORY OUTPUT
ALWAYS Display this summary after running script, before solving any items:
```
CodeRabbit Feedback from commit: <hash>
Claimed by CodeRabbit:  Actionable: X  Duplicate: Y  Nitpicks: Z  Total: N
Counted by script:      Actionable: A  Duplicate: B  Nitpicks: C  Total: M  Positive: P
```

## ITERATIVE EXECUTION PATTERN

### INITIALIZATION (EVERY RUN)
1. **AUTOMATIC CACHE CLEAR**: 
   ```bash
   ./scripts/fix-coderabbit-feedback.sh [mode]
   # Cache is cleared automatically by default
   # Only set CR_CLEAN_ARTIFACTS=0 for debugging to preserve
   ```
2. **FETCH FRESH**: Get latest feedback from GitHub (never stale)
3. **CATEGORIZE**: Group by priority (Critical → Important → Nitpicks)

### ITERATION CYCLE (PER CATEGORY)
```
For each category (Critical, Important, Nitpicks):
  1. RELOAD:
     - Re-read CLAUDE.md (head -200)
     - Re-read structured-development-cycle.md
     - Fresh context, no drift
  
  2. IMPLEMENT:
     - TodoWrite: Track items in this category
     - Fix EVERY item completely (quality > speed)
     - Validate after each fix
     - Mark todos as completed
  
  3. REVIEW & COMMIT:
     - mcp__claude-reviewer__request_review
     - Fix any review feedback
     - Commit: "fix(scope): address CodeRabbit [category] - review-id"
  
  4. CONTINUE:
     - Move to next category
     - RELOAD workflows again
```

### QUALITY ENFORCEMENT
- **NO RUSHING**: Take time to understand root causes
- **COMPLETE FIXES**: Fix all instances, not just one
- **VALIDATE EVERYTHING**: Test after every change
- **MULTIPLE COMMITS**: One per category, all reviewed

## STRUCTURED WORKFLOW PER ITERATION

### Phase 1: UNDERSTAND & ANALYZE
**MANDATORY RELOAD before each category:**
```bash
cat CLAUDE.md | head -200
cat .claude/workflows/core/structured-development-cycle.md
```
- Analyze items in current category
- Understand root causes, not symptoms
- Check existing code patterns

### Phase 2: DEEP THINK & PLAN
- Design fixes for category items
- Validate assumptions with code/docs
- Plan validation approach
- Consider impact across codebase

### Phase 3: IMPLEMENT WITH DISCIPLINE
- TodoWrite to track category items
- Fix each item COMPLETELY
- Validate immediately:
  - Terraform: `terraform validate`
  - .NET: `dotnet build && dotnet test`
  - Scripts: `bash -n`
- Mark todos completed as you go

### Phase 4: REVIEW BEFORE COMMIT
**MANDATORY for each category:**
```
mcp__claude-reviewer__request_review
  summary: "Fixed X CodeRabbit [category] items from PR #Y"
  focus_areas: "[Category] feedback implementation"
```

### Phase 5: COMMIT WITH REFERENCE
```
fix(scope): address CodeRabbit [category] feedback - <review-id>
- Fixed X items in [category]
- All validations passing
- Review: <review-id>
```


## ✅ Success Criteria
- ALL items in scope fixed (no exceptions)
- All tests passing
- AI review completed via MCP
- Changes committed with review ID
- ZERO remaining feedback items

## Script Details
- **Output**: `.artifacts/latest-coderabbit.json`
- **Deduplication**: By structural keys (path, line, end_line, type, source, body prefix)
- **Error Handling**: Graceful with clear messages
- **Dependencies**: `gh` CLI, `jq`, `git`

## Quick Troubleshooting
```bash
gh auth status                              # Check auth
gh pr view --json number                    # Verify PR
```

---
**Remember**: This command enforces the FULL structured workflow. 
Re-read @CLAUDE.md and @.claude/workflows/core/structured-development-cycle.md before EVERY run.