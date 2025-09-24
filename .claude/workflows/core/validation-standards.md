# Validation Standards

## Mandatory Validation

**NO GUESSING - VALIDATE EVERYTHING**

### Immediate Validation After Changes
- **Terraform**: `terraform validate` → must pass
- **.NET**: `dotnet build` → no warnings/errors
- **Pipelines**: YAML syntax validation
- **Tests**: `dotnet test` → 100% green

### For Interconnected Systems
Trace the ENTIRE data flow:
1. What creates what?
2. What consumes what?
3. Where does each artifact live?
4. Hand-run every step mentally
5. Verify all file paths with concrete examples

### Validation Checklist
✅ Code builds successfully  
✅ Application runs without crashes  
✅ All tests pass  
✅ Terraform validation passes (`./scripts/validate-terraform.sh quick`)  
✅ Data flow traced end-to-end  

## Confidence Requirements

**95%+ confidence required for production**

Evidence must include:
- Test results with links
- Code traces with file:line references
- Each assumption validated with code/docs
- Edge cases tested
- Maintainable by mid-level developer

## Testing Standards

### Test Requirements
- xUnit tests for all features
- FluentAssertions for readability
- Minimum coverage: happy path, edge case, failure case
- Integration tests use TestContainers

### Test Structure
```
Tests/
├── Unit/
│   ├── Controllers/
│   ├── Services/
│   └── Domain/
└── Integration/
    └── Api/
```

## Documentation Requirements

### Code Documentation
- XML documentation on all public methods
- Inline comments for "why" not "what"
- Examples in complex areas

### Updated Documentation
When features change, update:
- README.md for setup/usage
- API specifications
- Architecture decision records
- Configuration guides