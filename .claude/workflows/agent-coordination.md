# Agent Coordination Workflow

## Purpose
Proper delegation to specialized agents ensures domain expertise is applied correctly and consistently throughout development. This workflow defines when and how to coordinate with the 5 specialized agents available.

## Agent Structure

### **Base Agent (Shared Foundation)**
- **`@base-agent`** - Common MCP awareness, project context, and tools
- All specialized agents inherit from this base
- Provides consistent response style and tool knowledge

### **Specialized Agents**

#### System Architecture
- **`@architect`** - System design, microservices, API patterns, security architecture, integration strategies
- **Use for:** Architecture decisions, service boundaries, choreography patterns, design reviews

#### Backend Development
- **`@backend`** - .NET development, APIs, data layers, Entity Framework, repository patterns, CQRS, financial logic
- **Use for:** API implementation, database work, business logic, financial calculations, EU VAT compliance

#### Infrastructure & DevOps
- **`@infrastructure`** - Terraform, Azure, Container Apps, DevOps pipelines, monitoring
- **Use for:** Cloud resources, IaC, deployment strategies, pipeline creation, cost optimization

#### Security & Compliance
- **`@security`** - Authentication, authorization, multi-tenant isolation, compliance, data protection
- **Use for:** Auth implementation, security reviews, GDPR compliance, tenant isolation strategies

#### Documentation
- **`@technical-writer`** - Documentation for humans and machines, API specs, architecture records
- **Use for:** README updates, API documentation, ADRs, configuration guides

## Agent Delegation Rules

### **Phase 1: Understanding & Analysis**
**Primary Agent:** `@architect` for system-level understanding
**Supporting Agents:** Specialized agents based on domain

```markdown
When analyzing a task:
1. Start with @architect for architectural constraints
2. Consult relevant specialists:
   - Infrastructure work → @infrastructure
   - Security concerns → @security
   - API/data questions → @backend
```

### **Phase 2: Planning & Design**
**Primary Agent:** Based on task type
- System design → `@architect`
- API design → `@backend`
- Infrastructure design → `@infrastructure`

```markdown
Design delegation:
- Microservice boundaries → @architect
- API contracts → @backend
- Azure resources → @infrastructure
- Security boundaries → @security
```

### **Phase 3: Implementation**
**Use the specialist for each domain:**

```markdown
Implementation mapping:
- Controllers/APIs → @backend
- Data repositories → @backend
- Terraform modules → @infrastructure
- Auth middleware → @security
- Documentation → @technical-writer
```

### **Phase 4: Review & Validation**
**All agents participate in their domains:**

```markdown
Review responsibilities:
- Architecture compliance → @architect
- Code quality → @backend
- Security validation → @security
- Infrastructure review → @infrastructure
- Documentation accuracy → @technical-writer
```

## Multi-Agent Coordination Patterns

### **Sequential Pattern**
For tasks that flow through multiple domains:
```
@architect (design) → @backend (implement) → @security (review) → @technical-writer (document)
```

### **Parallel Pattern**
For independent workstreams:
```
┌─ @backend (API development)
├─ @infrastructure (Azure setup)
└─ @technical-writer (documentation)
```

### **Consultative Pattern**
For specialized input during implementation:
```
@backend (primary) ← consults → @security (auth patterns)
                  ← consults → @architect (design validation)
```

## Agent Selection Quick Reference

| Task Type | Primary Agent | Supporting Agents |
|-----------|--------------|-------------------|
| New microservice | @architect | @backend, @infrastructure |
| API endpoint | @backend | @security |
| Terraform module | @infrastructure | @architect |
| Authentication | @security | @backend |
| Documentation | @technical-writer | Domain experts |
| Financial logic | @backend | @architect |
| Multi-tenant | @security | @backend, @architect |
| Deployment | @infrastructure | @security |

## Coordination Best Practices

1. **Start with the right agent** - Don't use @architect for simple API work
2. **Leverage specialization** - Each agent has deep expertise in their domain
3. **Cross-check critical decisions** - Security and architecture need validation
4. **Document agent decisions** - Include rationale in commit messages
5. **Use base-agent knowledge** - All agents know about MCP tools

## Common Anti-patterns to Avoid

❌ Using @architect for everything (over-engineering)
❌ Skipping @security for auth work (security debt)
❌ Not using @infrastructure for Terraform (reinventing patterns)
❌ Ignoring @technical-writer (documentation debt)
❌ Working without agents (missing expertise)