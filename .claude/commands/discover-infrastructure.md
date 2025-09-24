# Discover Existing Infrastructure

**Purpose**: Automatically discover and document existing infrastructure components, configurations, and missing pieces.

**Usage**: "Discover infrastructure" or "What infrastructure exists?" or "Check current setup"

## Command Implementation

When triggered, this command will:

### 1. Infrastructure Audit
- Scan Terraform modules and variables
- Check existing service connections and IDs
- Review pipeline configurations and templates
- Identify configured vs missing components
- Document current state vs target state

### 2. Service Connection Discovery
```bash
# Check Azure service connections
az devops service-endpoint list --organization $AZDO_ORG_SERVICE_URL --project $PROJECT_NAME

# Scan Terraform for service connection references
grep -r "service_connection" terraform/ --include="*.tf"

# Check pipeline service connection usage
grep -r "serviceConnection" .azure-pipelines/ --include="*.yml"
```

### 3. Environment Variable Analysis
```bash
# Check required vs configured Terraform variables
terraform validate -json terraform/

# Scan for undefined variables in tfvars
find terraform/environments/ -name "*.tfvars" -exec echo "File: {}" \; -exec cat {} \;

# Check for missing environment variables
echo "Required: AZDO_ORG_SERVICE_URL, AZDO_PERSONAL_ACCESS_TOKEN"
```

### 4. Azure Resource State Check
```bash
# Check existing Azure resources
az resource list --resource-group rho-rg-dev --output table

# Verify Container Apps status
az containerapp list --resource-group rho-rg-dev --query "[].{name:name, state:properties.runningStatus}"

# Check Key Vault secrets
az keyvault secret list --vault-name rho-kv-dev --query "[].{name:name, enabled:attributes.enabled}"
```

### 5. Documentation Sync
- Update `docs/infrastructure/EXISTING-INFRASTRUCTURE.md`
- Refresh service connection registry
- Update environment configuration examples
- Generate missing tfvars templates

### 6. Gap Analysis Report
Generate comprehensive report including:

**Existing Infrastructure:**
- ✅ Terraform modules and their status
- ✅ Pipeline templates and configurations
- ✅ Azure resources and their state
- ✅ Service connections and authentication

**Missing Configuration:**
- ❌ Undefined Terraform variables
- ❌ Missing service connection IDs
- ❌ Unconfigured environment variables
- ❌ Missing Azure DevOps setup

**Next Steps:**
- Prioritized list of configuration tasks
- Links to relevant documentation
- Commands to resolve each gap

## Auto-Generated Outputs

### 1. Infrastructure Inventory
```markdown
# Current Infrastructure State
Generated: $(date)

## Azure Resources
- Resource Group: rho-rg-dev (exists)
- Key Vault: rho-kv-dev (exists, 3 secrets)
- Container Registry: rhocrdev (exists, Basic SKU)
- Container App Environment: rho-cae-dev (exists)

## Pipeline Infrastructure
- Build Templates: Available
- Deploy Templates: Available  
- Service Connections: 1 Azure, 0 GitHub

## Missing Configuration
- github_service_connection_id in dev.tfvars
- AZDO_PERSONAL_ACCESS_TOKEN environment variable
- Branch protection rules in GitHub
```

### 2. Setup Commands
```bash
# Commands to resolve gaps
echo "1. Get GitHub service connection ID:"
echo "   Navigate to Azure DevOps > Project Settings > Service connections"

echo "2. Update tfvars:"
echo "   Add github_service_connection_id = \"guid\" to terraform/environments/dev.tfvars"

echo "3. Set environment variables:"
echo "   export AZDO_ORG_SERVICE_URL=\"https://dev.azure.com/runehojsgaard\""
```

### 3. Validation Checklist
- [ ] All Terraform variables defined
- [ ] Service connections accessible
- [ ] Pipeline templates functional
- [ ] Azure resources healthy
- [ ] Documentation up-to-date

## Integration with Other Commands

This command is called automatically by:
- `/setup-environment` - Before environment setup
- `/troubleshoot-deployment` - During deployment issues
- `/generate-prp` - During PRP research phase (optional)

## Manual Triggers

- "What's already configured?"
- "Check existing infrastructure"
- "Audit current setup"
- "What do I need to configure?"
- "Discovery infrastructure gaps"

## Output Locations

- Update: `docs/infrastructure/EXISTING-INFRASTRUCTURE.md`
- Generate: `docs/infrastructure/CURRENT-STATE-{timestamp}.md`
- Log: `.claude/logs/infrastructure-discovery-{date}.log`

---

*This command ensures infrastructure knowledge is always current and discoverable when needed.*