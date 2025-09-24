# Code Quality Gates

## Purpose
Mandatory quality checkpoints that must pass before code can be considered complete. These gates prevent broken code from being committed and ensure high quality standards.

## Quality Gate Hierarchy

### **Level 1: Build Quality Gates** (Mandatory)
Must pass after every code change:

1. **✅ Clean Build**
   ```bash
   dotnet build
   # Must complete with 0 errors, 0 warnings
   ```

2. **✅ Application Startup**
   ```bash
   dotnet run
   # Must start without crashes
   # Must respond to basic requests
   ```

**Failure Response:** Fix immediately before proceeding to any other work.

---

### **Level 2: Testing Quality Gates** (Mandatory)
Must pass before marking any task complete:

1. **✅ All Unit Tests Pass**
   ```bash
   dotnet test
   # 100% green results required
   # No skipped tests without justification
   ```

2. **✅ Integration Tests Pass** (when applicable)
   ```bash
   dotnet test --filter Category=Integration
   # All integration tests must pass
   # TestContainers tests must complete successfully
   ```

3. **✅ Property-Based Tests Pass** (for financial logic)
   ```bash
   dotnet test --filter Category=Property
   # All FsCheck property tests must pass
   # Financial calculations must satisfy mathematical properties
   ```

**Failure Response:** Do not proceed. Fix tests or code until all pass.

---

### **Level 3: Code Quality Gates** (Mandatory)
Must validate before committing:

1. **✅ Code Follows Established Patterns**
   - Repository pattern matches `examples/template-app/Data/ExampleRepository.cs`
   - Service pattern follows clean architecture layers
   - Set-based operations only (no single-item special cases)
   - Multi-tenant context in all operations

2. **✅ Clean Code Standards**
   - Code reads like a story (clear, linear, obvious intent)
   - Clean code over clever code
   - Dependency injection with interfaces
   - XML documentation on all public methods
   - No comments unless explaining complex business logic

3. **✅ Security Standards**
   - No secrets or keys in code
   - All financial operations have audit trails
   - Tenant isolation maintained
   - Input validation using FluentValidation

**Failure Response:** Refactor code to meet standards before committing.

---

### **Level 4: Architecture Quality Gates** (Critical)
Must validate for significant changes:

1. **✅ Multi-Tenant Compliance**
   - Every operation includes tenant context
   - Global query filters applied
   - No cross-tenant data access possible
   - Repository methods accept tenant ID as first parameter

2. **✅ Financial Calculation Compliance**
   - Decimal types used for all monetary values
   - Banker's rounding applied consistently
   - EU VAT regulations followed
   - Money value objects used properly

3. **✅ Performance Standards**
   - Set-based database operations
   - No N+1 query problems
   - Appropriate caching where needed
   - Async/await patterns used correctly

**Failure Response:** Architectural review required. May need `@architect` consultation.

---

## Quality Gate Execution Order

**Always execute in this order:**

1. **Build** → Fix immediately if fails
2. **Test** → Fix all failing tests
3. **Code Review** → Refactor to meet standards
4. **Architecture Review** → Validate against principles
5. **Commit** → Only after all gates pass

## Quality Gate Violations

**❌ Never commit if any gate fails:**
- Broken builds
- Failing tests
- Code that doesn't follow patterns
- Multi-tenant violations
- Security vulnerabilities
- Performance regressions

## Agent Consultation for Quality Issues

**Build/Test Failures:**
- `@backend` - API, controller, repository, and database issues
- `@infrastructure` - Terraform and deployment issues
- `@security` - Authentication and authorization issues

**Code Quality Issues:**
- `@architect` - Architecture and pattern violations
- `@backend` - Financial calculations and business logic problems
- `@security` - Security and compliance issues

**Complex Quality Problems:**
- Use `@architect` for architectural reviews and service boundaries
- Use `@backend` for domain logic and data patterns

## Quality Metrics Targets

**Code Coverage:** >90% for business logic  
**Build Time:** <2 minutes for full solution  
**Test Execution:** <30 seconds for unit tests  
**Security Scan:** 0 high/critical vulnerabilities  
**Performance:** <200ms API response times  

## Emergency Quality Bypass

**NEVER bypass quality gates.** If urgent fixes are needed:
1. Create hotfix branch
2. Apply minimal fix
3. Still run all quality gates
4. Create follow-up task for proper solution

Quality is non-negotiable in financial systems.