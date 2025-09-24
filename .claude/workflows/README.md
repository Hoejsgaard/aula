# Claude Workflows System

This directory contains structured development workflows that Claude maintains in active memory to ensure consistent, disciplined development practices.

## Directory Structure

### **core/** - Core Development Workflows
Essential workflows that apply to all development tasks:
- `structured-development-cycle.md` - Master workflow enforcing proper analyspictogram2 → planning → implementation → review
- `code-quality-gates.md` - Build, test, and quality validation checkpoints
- `agent-coordination.md` - How to properly delegate to specialized agents

### **domain/** - Domain-Specific Workflows
Business domain workflows for specialized implementation:
- `financial-calculation-workflow.md` - EU VAT compliance and money calculations
- `multi-tenant-implementation.md` - Tenant-scoped operations and data isolation
- `invoice-processing-flow.md` - Invoice lifecycle and business rules

### **quality/** - Quality Assurance Workflows  
Quality gates and review processes:
- `code-review-process.md` - Comprehensive code review checklist
- `security-review-checklist.md` - Security validation for financial systems
- `compliance-validation.md` - EU regulations and audit trail requirements

### **templates/** - Workflow Templates
Reusable templates for common development patterns:
- `service-creation-template.md` - New microservice implementation workflow
- `api-endpoint-template.md` - REST API development with validation
- `database-migration-template.md` - Safe database schema changes

## Usage

Claude automatically references these workflows during development tasks and maintains them in active memory for consistent application of established patterns.

## Integration with Agents

Workflows coordinate with specialized agents using `@agent-name` references:
- `@architect` - System design, service patterns, and architectural decisions
- `@backend` - API implementation, financial logic, and EU VAT compliance
- `@infrastructure` - Terraform, Azure resources, and deployment
- `@security` - Authentication, multi-tenant isolation, and compliance
- `@technical-writer` - Documentation and API specifications

## Workflow Activation

Workflows are automatically activated based on task context:
- **Feature Development** → `core/structured-development-cycle.md` 
- **Financial Logic** → `domain/financial-calculation-workflow.md`
- **Multi-Tenant Features** → `domain/multi-tenant-implementation.md`
- **Code Reviews** → `quality/code-review-process.md`