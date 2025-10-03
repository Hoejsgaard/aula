# Request Code Review Command

## Purpose
Request an AI-powered code review of your current changes using the Claude Reviewer MCP server.

## Usage
```
/request-review [focus-areas]
```

**Note**: This command uses the `mcp__claude-reviewer__request_review` MCP tool internally.

## Examples
```
# Basic review
/request-review

# Security-focused review
/request-review security

# Multi-focus review
/request-review security performance architecture
```

## What It Does

1. **Checks for changes** - Ensures you have uncommitted changes to review
2. **Checks MCP availability** - Verifies Claude Reviewer MCP is running
3. **Stages files (with confirmation)** - Optionally runs `git add -A` to stage changes
4. **Requests AI review** - Uses Claude Reviewer MCP to analyze changes
5. **Provides feedback** - Shows architectural, security, and code quality issues
6. **Tracks history** - Maintains review history in `.reviews/` for follow-ups

## When to Use

**REQUIRED for:**
- Completing multi-step coding tasks
- Before committing core functionality changes
- After fixing bugs or implementing features
- When refactoring existing code

**OPTIONAL for:**
- Documentation-only changes
- Simple typo fixes
- Debug logging additions

## Workflow Integration

This command is integrated into Phase 4 of the structured development cycle:

1. Complete implementation and pass all tests
2. Run `/request-review` to get AI feedback
3. Address critical issues identified
4. Request follow-up review if needed
5. Proceed to commit when approved

## Review Focus Areas

Default focus areas (applied automatically):
- **Architecture** - Design patterns and structure
- **Security** - Vulnerability detection
- **Performance** - Optimization opportunities
- **Best practices** - Code quality and maintainability
- **Testing** - Test coverage and quality

Custom focus areas can be specified:
- `security` - Deep security analysis
- `performance` - Performance bottlenecks
- `architecture` - Design compliance
- `testing` - Test completeness
- `documentation` - Doc quality

## Review States

- **approved** ✅ - Ready to commit
- **needs_changes** ⚠️ - Address feedback first
- **needs_major_changes** ❌ - Significant issues found

## Implementation

```python
# Command implementation
def request_review(focus_areas=None):
    # Default focus areas (single source of truth)
    DEFAULT_FOCUS_AREAS = [
        "architecture",
        "security", 
        "performance",
        "best practices",
        "testing"
    ]
    
    # Check for uncommitted changes
    if not git.has_changes():
        return "No changes to review. Make changes first."
    
    # Check MCP availability
    if not mcp.claude_reviewer.is_available():
        return "Claude Reviewer MCP is not available. Check your .claude/mcp.json configuration."
    
    # Optional staging (with confirmation)
    # WARNING: auto_stage bypasses safety checks - ensure no secrets/credentials in working directory
    if cli.flags.get("auto_stage", False) or cli.confirm("Stage all changes (git add -A)?"):
        git.add_all()
    elif not git.has_staged_changes():
        return "No staged changes. Stage files with 'git add' or confirm auto-staging."
    
    # Generate summary
    summary = git.get_change_summary()
    
    # Detect test command
    test_cmd = detect_test_command()
    
    # Request review via MCP
    review = mcp.claude_reviewer.request_review(
        summary=summary,
        focus_areas=focus_areas or DEFAULT_FOCUS_AREAS,
        test_command=test_cmd
    )
    
    # Display results
    display_review_results(review)
    
    # Save to history in .reviews/ directory
    save_review_history(review, location=".reviews/history.jsonl")
    
    return review
```

## Review History

View past reviews:
```
/review-history [limit]
```

Mark review complete:
```
/review-complete [review-id] [status]
```

**Allowed status values:**
- `approved` - Changes approved and ready to merge
- `abandoned` - Review abandoned (changes discarded)
- `merged` - Changes have been merged

**History storage:**
- Location: `.reviews/history.jsonl` (JSON Lines format)
- Sessions: `.reviews/sessions/` (individual review sessions)
- Rotation: Automatic cleanup after 30 days (configurable)

## Tips

1. **Stage selectively** - Use `git add` for specific files before review (safer than auto-staging)
2. **Be specific** - Provide focus areas for targeted feedback
3. **Iterate** - Request follow-up reviews after addressing feedback
4. **Track progress** - Reference previous review IDs
5. **Document decisions** - Explain why certain feedback wasn't addressed
6. **Avoid secrets** - Review staging to ensure no secrets/credentials are included

## Automation

The review process is automated through:
- Optional file staging (with confirmation for safety)
- MCP availability verification
- Change detection and summarization
- Test command detection
- Review history tracking in `.reviews/`
- Integration with git workflow

## Error Handling

Common issues:
- **No changes**: Make changes before requesting review
- **Tests failing**: Fix tests first for accurate review
- **Large changesets**: Consider breaking into smaller reviews
- **Parse errors**: Ensure MCP server is running correctly

## Related Commands

- `/fix-coderabbit-feedback` - Process external code reviews
- `/test` - Run project tests
- `/validate` - Validate Terraform/code syntax
- `/commit` - Create semantic commits after review