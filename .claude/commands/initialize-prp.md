# Initialize PRP

Create a new Problem Resolution Plan (PRP) through an adaptive questioning process. Start with essential questions, then ask follow-ups based on the complexity and type of feature.

## Adaptive Process

1. **Start Minimal**
   - Ask only 3-4 core questions first
   - Determine feature complexity from answers
   - Skip sections not relevant to the feature type

2. **Smart Follow-ups**
   - If infrastructure mentioned → ask about Azure resources
   - If integration mentioned → ask about external docs
   - If security critical → dive into security requirements
   - If simple feature → skip complex sections

3. **Auto-fill Smart Defaults**
   - Use existing patterns from codebase
   - Infer documentation references
   - Apply standard security practices
   - Default to template-app patterns

## Core Questions (Always Ask)

1. **What do you want to build?** (name and one-line description)
   - Example: "invoice-service - Generate and manage customer invoices"

2. **What's the main goal?** (2-3 sentences about the problem it solves)
   - Example: "Create PDF invoices from order data, email them to customers, track payment status"

3. **Is this a new service or a feature for an existing service?**
   - New service → will need full setup
   - Feature → can leverage existing patterns

4. **What's the complexity level?** (simple/medium/complex)
   - Simple: Basic CRUD, no external integrations
   - Medium: Some integrations, standard patterns
   - Complex: Multiple integrations, new infrastructure, complex business logic

## Adaptive Follow-ups

### If New Service:
- What type? (API service, background worker, UI application)
- Will it need a database?

### If External Integration:
- Which external services/APIs?
- Do you have documentation links?

### If Complex:
- What makes it complex?
- Any special infrastructure needs?
- Security concerns?

### If Medium/Complex:
- What are the success criteria?
- Any manual setup required?

## Smart Defaults Applied

Based on answers, the command will:
- **Auto-reference** standard docs (CLAUDE.md, template-app)
- **Auto-include** common patterns (Clean Architecture, MediatR, EF Core)
- **Auto-add** standard security (JWT auth, tenant isolation)
- **Auto-suggest** infrastructure based on service type
- **Skip irrelevant** sections (e.g., cost optimization for simple features)

## Example Flow

**User**: /initialize-prp

**Claude**: Let's create a PRP! First, the essentials:

1. What do you want to build?
> "customer-service - Manage customer profiles and preferences"

2. What's the main goal?
> "Store customer data, handle GDPR compliance, provide API for other services"

3. Is this a new service or a feature?
> "new service"

4. Complexity level?
> "medium"

**Claude**: Since it's a new medium-complexity service, a few more questions:

5. Will it need a database?
> "yes"

6. What are the success criteria?
> "CRUD APIs working, GDPR compliance features, integrated with auth service"

**Claude**: Perfect! I'll create your PRP with:
- Standard microservice setup based on template-app
- Entity Framework with multi-tenant patterns
- GDPR considerations for data handling
- Standard auth integration

PRP created: /PRPs/seeds/customer-service-prp.md

## Minimal PRP Example

For a simple feature, the PRP might only include:
- Feature description
- Primary goals
- What Claude will create
- Success criteria

Complex features automatically get:
- Infrastructure components
- Security considerations
- External documentation
- Manual prerequisites
- Cost optimization

## Output

Show what sections were included and why:
```
PRP created successfully: /PRPs/seeds/[service-name]-prp.md

Included sections:
- [YES] Core feature description
- [YES] Primary goals and deliverables
- [YES] Infrastructure (detected new service needs)
- [YES] Security (detected GDPR requirements)
- [SKIP] Cost optimization (standard patterns apply)

To execute: /execute-prp PRPs/seeds/[service-name]-prp.md
```