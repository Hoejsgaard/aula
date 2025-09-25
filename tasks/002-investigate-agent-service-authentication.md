# Task 002: Investigate AgentService Authentication Session Management

**Status:** PENDING
**Priority:** High
**Created:** 2025-01-24

## Problem
The AgentService authentication session expires after only 30 seconds, requiring frequent re-authentication. This causes performance issues and unnecessary API calls.

## Current Behavior
- Authentication happens in `AgentService.LoginAsync()`
- Session appears to expire after 30 seconds
- Each child requires separate authentication (per-child authentication model)
- Frequent re-authentication impacts performance

## Investigation Areas
1. **Session Timeout Configuration**
   - Check if 30-second timeout is configurable
   - Look for session refresh mechanisms
   - Review MinUddannelse API documentation

2. **Token Management**
   - Investigate if tokens can be refreshed without full re-authentication
   - Check if tokens are being cached properly
   - Review PerChildMinUddannelseClient implementation

3. **Connection Pooling**
   - Verify if HttpClient instances are being reused
   - Check for connection lifecycle issues
   - Review cookie container management

## Potential Solutions
1. Implement token refresh mechanism
2. Add configurable session timeout
3. Implement connection pooling per child
4. Add session keep-alive mechanism
5. Cache authentication tokens with longer TTL

## Files to Investigate
- src/Aula/Services/AgentService.cs
- src/Aula/Integration/PerChildMinUddannelseClient.cs
- src/Aula/Integration/UniLoginClient.cs

## Success Criteria
- Authentication sessions last for reasonable duration (e.g., 30 minutes)
- Reduced number of authentication API calls
- Improved application performance
- No impact on security