# Documentation Update Requirements

**MANDATORY**: All agents and Claude Code must update corresponding documentation when making infrastructure or configuration changes.

## Auto-Discoverable Documentation Locations

### Infrastructure Documentation
- `docs/infrastructure/EXISTING-INFRASTRUCTURE.md` - Current state of all infrastructure components
- `docs/infrastructure/SERVICE-CONNECTIONS.md` - Registry of all service connections and their IDs
- `docs/infrastructure/ENVIRONMENT-CONFIGURATION.md` - Complete environment variable and tfvars reference

### Service Documentation  
- Service-specific README files
- API documentation in `docs/` folders
- Pipeline configuration in service directories

## Required Updates

### When Changing Infrastructure
**MUST UPDATE**:
1. `EXISTING-INFRASTRUCTURE.md` - Add/remove/modify infrastructure components
2. `SERVICE-CONNECTIONS.md` - Update service connection IDs and configurations
3. `ENVIRONMENT-CONFIGURATION.md` - Add new variables or modify existing ones

**Example Changes Requiring Updates**:
- Adding new Azure resources → Update infrastructure inventory
- Creating service connections → Update service connection registry  
- Adding environment variables → Update configuration reference
- Enabling/disabling Terraform modules → Update current state documentation

### When Changing Services
**MUST UPDATE**:
1. Service README with new features or configuration
2. API documentation if endpoints change
3. Pipeline documentation if build/deploy process changes
4. Integration guides if service interfaces change

### When Changing Pipelines
**MUST UPDATE**:
1. Pipeline documentation in `docs/deployment/pipeline-overview.md`
2. Troubleshooting guides with new common issues
3. Service connection documentation if new connections added
4. Environment configuration if new variables required

## Agent-Specific Requirements

### Infrastructure Expert Agent
- Always update `EXISTING-INFRASTRUCTURE.md` when analyzing or modifying infrastructure
- Update service connection registry when working with authentication
- Maintain environment configuration documentation

### Backend Expert Agents
- Update service README files when adding features
- Update API documentation when modifying endpoints
- Update integration guides when changing service interfaces

### Pipeline/DevOps Agents
- Update pipeline documentation when modifying build/deploy processes
- Update troubleshooting guides with new solutions
- Update environment configuration with new pipeline variables

## Documentation Discovery Command

Use `/discover-infrastructure` command to:
- Audit current documentation state
- Identify missing or outdated documentation
- Generate updated infrastructure inventory
- Validate documentation against actual state

## Quality Gates

Before considering any infrastructure or service change complete:
- [ ] Relevant documentation files updated
- [ ] Changes reflected in auto-discoverable locations
- [ ] Documentation validated against actual implementation
- [ ] Links and references still valid

## Documentation Patterns

### Infrastructure Changes
```markdown
# When adding new Azure resource:
1. Update EXISTING-INFRASTRUCTURE.md with new resource
2. Update ENVIRONMENT-CONFIGURATION.md if new variables required
3. Update troubleshooting guide if known issues exist
```

### Service Changes  
```markdown
# When adding new service endpoint:
1. Update service README with endpoint documentation
2. Update API documentation with request/response examples
3. Update integration guide if other services need to call it
```

### Pipeline Changes
```markdown
# When modifying build pipeline:
1. Update pipeline-overview.md with new steps
2. Update troubleshooting.md with new potential issues
3. Update service README if deployment process changed
```

## Automation

### Auto-Generated Documentation
- Infrastructure inventory via `/discover-infrastructure`
- Pipeline status and health checks
- Service connection validation

### Manual Documentation
- Troubleshooting guides and solutions
- Integration patterns and examples
- Configuration best practices

## Enforcement

This is a **MANDATORY** workflow requirement:
- Changes without documentation updates are incomplete
- Pull requests should include documentation updates
- Infrastructure changes must pass documentation review

---

*This ensures our auto-discoverable documentation remains current and useful for future development.*