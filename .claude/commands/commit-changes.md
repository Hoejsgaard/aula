# Commit Changes Command

## Purpose
Create semantic commits that tell the project's story clearly and concisely.

**IMPORTANT**: Only commit when explicitly asked by the user. Never auto-commit after completing tasks.

## Commit Message Format
```
type(scope): brief summary

- What changed and why (if not obvious)
- Breaking changes (if any)
```

## Types
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation only
- `refactor:` - Code changes that neither fix bugs nor add features
- `test:` - Adding or updating tests
- `chore:` - Maintenance (dependencies, build, etc.)
- `perf:` - Performance improvements
- `style:` - Code style changes (formatting, etc.)

## When to Split Commits
Split unrelated changes into separate commits:
- Different features → separate `feat:` commits
- Bug fix + new feature → `fix:` then `feat:`
- Refactor + tests → `refactor:` then `test:`
- Infrastructure + code → `chore:` then `feat:`/`fix:`

## Examples

### Good - Concise and Clear
```
feat(auth): add JWT token validation

- Add token expiry check
- Validate tenant context in claims
```

```
fix(invoice): correct VAT calculation rounding

- Use banker's rounding for EU compliance
- Fixes #123
```

```
chore: add node_modules to gitignore
```

### Bad - Too Verbose or Mixed
```
feat: massive update with lots of changes and refactoring
```

```
fix(everything): fix auth, update docs, add tests, refactor db
```

## Commit Splitting Process
1. Stage related changes: `git add -p` for partial staging
2. Commit with focused message
3. Repeat for next logical change
4. Each commit should pass tests independently