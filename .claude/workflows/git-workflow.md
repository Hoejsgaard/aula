# Git Workflow

## Core Rules

### NEVER MERGE TO MASTER LOCALLY
```bash
# ❌ ABSOLUTELY FORBIDDEN
git checkout master
git merge feature-branch    # NEVER DO THIS
git commit -m "anything"    # NEVER DO THIS on master
```

**WHY**: Master branch is protected in GitHub. ALL changes go through pull requests.

### Correct Workflow
1. Work on feature branches: `git checkout -b feature/[name]`
2. **CLAUDE: NEVER PUSH! ONLY COMMIT LOCALLY!** User will push when ready
3. Create PR in GitHub (USER DOES THIS)
4. Let GitHub handle the merge

### ❌ CLAUDE MUST NEVER DO THIS
```bash
# NEVER NEVER NEVER
git push                     # FORBIDDEN FOR CLAUDE
git push origin              # FORBIDDEN FOR CLAUDE  
git push -u origin           # FORBIDDEN FOR CLAUDE
git remote set-url           # FORBIDDEN FOR CLAUDE
```

## Commit Standards

### Message Format
```
type(scope): brief summary

- What changed and why
- Breaking changes (if any)
```

### Types
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation only
- `refactor:` - Code restructuring
- `test:` - Tests
- `chore:` - Maintenance

### Pre-Commit Review
**MANDATORY**: Always request AI code review before committing:
1. Stage changes: `git add -A`
2. Request review: `mcp__claude-reviewer__request_review`
3. Fix critical/major issues
4. Include review ID in commit message