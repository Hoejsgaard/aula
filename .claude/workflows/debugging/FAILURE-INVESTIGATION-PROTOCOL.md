# Failure Investigation Protocol

## MANDATORY for ALL Failures (401, 404, Connection Errors, etc.)

### STOP - Before ANY Solution Attempts:

1. **CHECK THE CONFIGURATION THAT CREATED IT**
   ```bash
   # Example for Key Vault issues:
   grep -n "enable_rbac" terraform/**/*.tf terraform/**/*.tfvars
   ```

2. **READ THE ACTUAL TERRAFORM/CODE**
   - What module created this resource?
   - What variables were passed?
   - What mode/configuration was used?

3. **TRACE THE ACCESS CHAIN**
   - What needs access? (Function App)
   - To what? (Key Vault)  
   - How? (Access Policy vs RBAC)
   - What was configured? (Check tfvars)

4. **CHECK LOGS/DIAGNOSTICS FIRST**
   - Azure Portal diagnostic information
   - Application logs
   - Error messages in UI

5. **VERIFY ASSUMPTIONS**
   - "I think it uses access policies" → CHECK THE CODE
   - "It should have access" → VERIFY THE CONFIGURATION
   - "The secret exists" → CONFIRM WITH LOGS

### ONLY AFTER ALL ABOVE:
- Propose solution based on findings
- Fix in Terraform/Code, not scripts
- Document the root cause

## Example: Key Vault 401 Investigation

❌ **WRONG:**
"Let me try adding access policy"
"Let me recreate the secret"
"Let me restart the function"

✅ **RIGHT:**
1. Check: `grep enable_rbac terraform/**/*.tfvars`
2. Found: `enable_rbac_authorization = true`
3. Check webhook module: Uses access policies
4. Root cause: Mismatch - RBAC enabled but using policies
5. Fix: Update terraform to use RBAC role assignment

## Red Flags That Mean STOP:
- "Let me try..."
- "It might be..."
- "Usually this means..."
- "The common fix is..."

Replace with:
- "Let me check the configuration..."
- "Let me trace what created this..."
- "Let me verify the actual setup..."