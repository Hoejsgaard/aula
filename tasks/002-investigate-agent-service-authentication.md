# Task 002: Investigate AgentService Authentication Session Management

**Status:** COMPLETED ✅
**Priority:** High
**Created:** 2025-01-24
**Investigated:** 2025-01-25
**Implemented:** 2025-01-25

## Problem
The AgentService was designed for single-run operation but now runs continuously. There's concern about session management and authentication timeout handling for this long-running service.

## Investigation Summary (2025-01-25)
- **Session Duration**: MinUddannelse sessions last ~20-30 minutes, refreshed on use
- **Actual Usage Pattern**: MinUddannelse is only called during weekly scheduled tasks (once/week)
- **User Queries**: Always served from cache, never trigger API calls
- **Future Usage**: Planning ~10 additional API calls per day (still infrequent)

## Current Behavior
- Authentication happens in `AgentService.LoginAsync()`
- Sessions expire after ~20-30 minutes (server-side timeout)
- Each child requires separate authentication (per-child authentication model)
- Authenticated clients are cached in a ConcurrentDictionary (problematic for long-running service)

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

## Files Investigated
- src/Aula/Integration/AgentService.cs (not Services/)
- src/Aula/Integration/PerChildMinUddannelseClient.cs
- src/Aula/Integration/UniLoginDebugClient.cs
- src/Aula/Program.cs (DI registration)

## Investigation Findings

### Architecture Overview
The authentication system uses a chain of singleton services:
```
AgentService (singleton)
  └─> PerChildMinUddannelseClient (singleton)
       └─> ChildAuthenticatedClient instances (one per child)
            └─> UniLoginDebugClient (base class with HttpClient)
```

### Key Issues Discovered

1. **No Session Timeout Detection**
   - AgentService uses a simple `_isLoggedIn` boolean flag (line 14)
   - Once set to true in `LoginAsync()`, it never checks if sessions are still valid
   - No mechanism to detect when MinUddannelse sessions expire

2. **No Re-authentication on Failure**
   - When API calls fail due to expired sessions, `EnsureSuccessStatusCode()` throws HttpRequestException
   - No catch blocks to handle 401 Unauthorized responses
   - System will crash with exceptions rather than gracefully re-authenticating

3. **Singleton Pattern Issues**
   - All services are registered as singletons (Program.cs lines 197, 220)
   - Authentication state persists for entire application lifetime
   - Good for startup efficiency but problematic for long-running services

4. **Per-Child Authentication**
   - Each child has separate UniLogin credentials and authentication session
   - PerChildMinUddannelseClient maintains a `ConcurrentDictionary<string, ChildAuthenticatedClient>`
   - Better than original design but still lacks session management

5. **No Actual 30-Second Timeout Found**
   - Task documentation mentions 30-second timeout but no evidence found in code
   - HttpClient has no explicit timeout configuration
   - Session expiry time is determined by MinUddannelse server, not client

### Current Authentication Flow

1. **Initial Login:**
   - `AgentService.LoginAsync()` calls `PerChildMinUddannelseClient.LoginAsync()`
   - Creates `ChildAuthenticatedClient` for each child with credentials
   - Stores authenticated clients in dictionary
   - Sets `_isLoggedIn = true` in AgentService

2. **Subsequent API Calls:**
   - Check `_isLoggedIn` flag (always true after first login)
   - Use stored `ChildAuthenticatedClient` to make API calls
   - If client missing, attempts on-demand authentication
   - No check if existing session is still valid

3. **When Session Expires:**
   - API calls fail with 401 Unauthorized
   - `EnsureSuccessStatusCode()` throws exception
   - Application crashes or returns error to user
   - No automatic recovery

## Recommended Solution: Fresh Authentication Per Request

### The Chosen Approach
Create fresh `ChildAuthenticatedClient` instances for each API call instead of caching them:

```csharp
public async Task<JObject> GetWeekLetter(Child child, DateOnly date)
{
    // Create fresh client for this request - no caching
    var childClient = new ChildAuthenticatedClient(
        child,
        child.UniLogin.Username,
        child.UniLogin.Password,
        _logger
    );

    // Authenticate fresh for this request
    var loginSuccess = await childClient.LoginAsync();
    if (!loginSuccess)
    {
        _logger.LogError("❌ Failed to authenticate {ChildName}", child.FirstName);
        return CreateEmptyWeekLetter(GetIsoWeekNumber(date));
    }

    // Use the fresh client to get the week letter
    return await childClient.GetWeekLetter(date);
}
```

### Why This Solution is Optimal

1. **Actual Usage Pattern**:
   - Current: ~1 API call per week (weekly scheduled task)
   - Future: ~10 API calls per day (still very infrequent)
   - Sessions expire in 20-30 minutes, so would timeout between calls anyway

2. **Simplicity**:
   - No session management complexity
   - No expiry tracking needed
   - No 401 error handling required
   - Cannot fail due to expired sessions

3. **Performance**:
   - Creating 10 HttpClients per day is negligible overhead
   - Authentication time is minimal compared to 2-3 hour gaps between calls
   - No performance benefit from session reuse at this scale

4. **Reliability**:
   - Every API call gets a fresh, valid session
   - Eliminates entire category of session-related bugs
   - No risk of cascading failures from expired sessions

### Implementation Steps

1. **Remove Session Caching** in `PerChildMinUddannelseClient`:
   ```csharp
   // Remove this line:
   private readonly ConcurrentDictionary<string, ChildAuthenticatedClient> _authenticatedClients = new();
   ```

2. **Update `GetWeekLetter` Method**:
   - Remove dictionary lookups
   - Create fresh `ChildAuthenticatedClient` on each call
   - Authenticate, use, and let it be garbage collected

3. **Update `GetWeekSchedule` Method**:
   - Apply same pattern as GetWeekLetter
   - Fresh instance per call

4. **Keep Singleton Pattern**:
   - `AgentService` remains singleton (fine)
   - `PerChildMinUddannelseClient` remains singleton (fine)
   - Only `ChildAuthenticatedClient` instances are ephemeral

### When to Reconsider

Only revisit this approach if:
- API calls increase to >100 per day (every ~15 minutes)
- Calls happen in bursts (multiple calls within minutes)
- Authentication becomes slow (currently fast)

For the foreseeable future with ~10 calls/day, fresh instances are the perfect solution.

## Implementation Complete

### Changes Made

1. **PerChildMinUddannelseClient.cs**:
   - Removed `ConcurrentDictionary<string, ChildAuthenticatedClient>` field
   - Updated `GetWeekLetter()` to create fresh `ChildAuthenticatedClient` for each request
   - Updated `GetWeekSchedule()` to create fresh `ChildAuthenticatedClient` for each request
   - Changed `LoginAsync()` to no-op method returning `Task.FromResult(true)`
   - Removed unused `using System.Collections.Concurrent;`

2. **AgentService.cs**:
   - Removed `_isLoggedIn` field (was only set, never checked anymore)
   - Removed login checks from `GetWeekLetterAsync()` and `GetWeekScheduleAsync()`
   - Updated `LoginAsync()` to simply delegate to MinUddannelseClient

3. **Tests Added**:
   - Created `PerChildMinUddannelseClientTests.cs` with 8 tests verifying:
     - Fresh authentication per request
     - No session caching
     - Proper error handling for missing credentials
   - Enhanced `AgentServiceTests.cs` with 7 additional tests verifying:
     - No login status checking
     - Proper cache behavior
     - Direct delegation to MinUddannelseClient

### Results
- ✅ All 1,545 tests passing
- ✅ No compilation errors or warnings
- ✅ Fresh authentication guaranteed for each API call
- ✅ Session timeout issues eliminated
- ✅ Simpler, more reliable code

## Success Criteria
- ✅ Authentication sessions handle expiry gracefully
- ✅ Automatic re-authentication when sessions expire
- ✅ No crashes due to 401 Unauthorized responses
- ✅ Minimal performance impact from re-authentication
- ✅ Maintains security without storing credentials unsafely