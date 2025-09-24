# Architectural Principles

## Core Principles
1. **Code reads like a story** - Clear, linear, obvious intent
2. **Clean code over clever code** - Maintainability first
3. **Set-based operations only** - No single-item special cases
4. **Multi-tenancy baked in** - Every operation is tenant-scoped
5. **Good enough IS good enough** - MVP focus
6. **Dependency injection over tight coupling** - Interfaces for testability

## Set-Based Operations

**CRITICAL**: All core logic must be set-based. Single-item operations are convenience methods.

### Repository Pattern
```csharp
// Core implementation is set-based
public async Task<T[]> GetByIdsAsync(Guid tenantId, params Guid[] ids)

// Convenience method calls set-based
public async Task<T?> GetByIdAsync(Guid tenantId, Guid id)
    => (await GetByIdsAsync(tenantId, id)).FirstOrDefault();
```

### Service Pattern
```csharp
// Core handles arrays
public async Task<Result<T[]>> CreateAsync(Guid tenantId, params T[] items)

// Convenience for single item
public async Task<Result<T>> CreateOneAsync(Guid tenantId, T item)
    => /* calls CreateAsync */
```

**See**: `examples/template-app/Data/` for implementation patterns

## Multi-Tenant Architecture

### Requirements
- **Schema-per-tenant isolation**: Each tenant gets `tenant_{guid}` schema
- **Tenant context propagation**: Every request includes tenant context
- **Repository patterns**: All data access is tenant-scoped
- **Global query filters**: EF contexts filter by tenant automatically
- **API headers**: `X-Tenant-Id` header for context

### Implementation
```csharp
// Always explicit tenant context
await repository.GetInvoicesAsync(tenantId, filters);
```

**See**: `examples/template-app/Infrastructure/MultiTenantDbContext.cs`

## Template Usage

**MANDATORY**: Never start from scratch. Use templates:

- **Backend**: Copy `examples/template-app/`
- **Frontend**: Copy `examples/template-client/`
- **Configuration**: Copy `examples/configuration/`

## Authentication

Keep it simple for MVP:
- Simple email/password only
- JWT-based authentication
- Tenant context automatic
- Clean dependency injection

**See**: `services/auth-app/Application/Services/SimpleAuthService.cs`

## Microservice Structure

Clean Architecture layers:
- `Controllers/` - API endpoints
- `Application/` - Use cases, MediatR
- `Domain/` - Entities, value objects
- `Infrastructure/` - Data access, integrations

Maximum 500 lines per class - refactor if larger.