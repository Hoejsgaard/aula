# Task 013: Fix LLM Reading Comprehension and System Prompt Leakage

**Priority**: High
**Type**: Bug Fix + Prompt Engineering
**Status**: Not Started
**Discovered**: 2025-10-01 17:42 UTC

---

## Problem Summary

The AI assistant failed to correctly parse and answer questions about scheduled events in week letters, leading to three critical issues:

1. ❌ **Missed scheduled events** (failed to mention "Foto: Torsdag 10.35-11.15")
2. ❌ **System prompt leakage** (returned "FALLBACK_TO_EXISTING_SYSTEM" to user)
3. ❌ **Hallucination** (denied photo session existed when directly asked)

---

## Actual User Conversation (Evidence)

```
User: Hvad skal Søren på torsdag?
AI: Søren skal til staveprøve i dansk på torsdag kl. 12.45.

User: Skal han andet?
AI: FALLBACK_TO_EXISTING_SYSTEM

User: Skal Søren andet end til staveprøve på torsdag?
AI: Nej, udover staveprøven på torsdag skal Søren ikke andet i denne uge ifølge ugebrevet.

User: Så Søren skal ikke fotograferes på torsdag?
AI: Nej, Søren skal ikke fotograferes på torsdag.
```

**Week letter content (excerpt)**:
```
Foto: Torsdag 10.35-11.15
Staveprøve dansk: Torsdag kl. 12.45 (husk opladet computer og høretelefoner)
```

**Correct answer should be**: "Ja, Søren har to ting på torsdag: Foto kl. 10.35-11.15 og staveprøve kl. 12.45"

---

## Root Cause Analysis

### Issue 1: Poor Event Parsing

**Problem**: LLM treated "Foto: Torsdag 10.35-11.15" as a category header instead of a scheduled event.

**Format ambiguity**:
```
Foto: Torsdag 10.35-11.15        ← Is "Foto:" a label or an event?
Staveprøve dansk: Torsdag kl. 12.45
```

**Current system prompt** (`src/Aula/Services/PromptBuilder.cs` lines 10-31):
- No explicit instruction to parse "Label: Day Time" format as scheduled events
- No instruction to list ALL events when asked "Skal han andet?" (Does he have anything else?)
- No instruction to carefully review all mentioned days/activities

**Evidence from code**: `PromptBuilder.cs:10-31`
```csharp
return ChatMessage.FromSystem($"You are a helpful assistant that answers questions about {childName}'s weekly school letter. " +
    // ... no event parsing instructions
```

---

### Issue 2: System Prompt Leakage

**Problem**: Internal control signal "FALLBACK_TO_EXISTING_SYSTEM" was returned to user instead of triggering fallback logic.

**Evidence from code**: `src/Aula/Services/OpenAiService.cs`
```csharp
private const string FallbackToExistingSystem = "FALLBACK_TO_EXISTING_SYSTEM";

// CRITICAL: Must return "FALLBACK_TO_EXISTING_SYSTEM" to trigger AgentService fallback logic
// DO NOT return generic help text here - it causes language mismatch (Danish->English)
// and prevents proper week letter processing
_logger.LogInformation("Delegating Aula query to existing system: {Query}", query);
return Task.FromResult(FallbackToExistingSystem);
```

**Root cause**: LLM returned this string verbatim instead of it being intercepted by application code.

**Check locations**:
- `src/Aula/Integration/AgentService.cs`: `if (response == "FALLBACK_TO_EXISTING_SYSTEM")`
- Ensure this check happens BEFORE response is sent to user

---

### Issue 3: Context Loss / Hallucination

**Problem**: LLM gave contradictory answers across conversation turns:
1. First: Only mentioned staveprøve
2. Second: System error (FALLBACK)
3. Third: Confirmed no other events
4. Fourth: Denied photo session exists

**Possible causes**:
- Limited conversation context window
- No reinforcement to "re-read the week letter for ALL Thursday events"
- No structured extraction before answering (relying on conversational memory)

---

## Proposed Fixes

### Fix 1: Improve Event Parsing Instructions ✅

**File**: `src/Aula/Services/PromptBuilder.cs`
**Method**: `CreateSystemInstructionsMessage()`

**Add to system prompt** (after line 20):
```csharp
"IMPORTANT: When parsing scheduled events, treat lines formatted as 'Activity: Day Time' as scheduled events. " +
"For example: 'Foto: Torsdag 10.35-11.15' means there IS a photo session on Thursday at 10:35-11:15. " +
"'Staveprøve dansk: Torsdag kl. 12.45' means there IS a spelling test on Thursday at 12:45. " +
"When a user asks about a specific day (like 'Hvad skal [child] på torsdag?'), carefully review the entire letter " +
"and list ALL events/activities mentioned for that day, not just the first one you see. " +
"When asked 'Skal han andet?' (Does he have anything else?), re-check the letter for additional events."
```

**Estimated effort**: 15 minutes
**Test**: Create unit test with multi-event Thursday scenario

---

### Fix 2: Prevent System Prompt Leakage ✅

**File**: `src/Aula/Integration/AgentService.cs`

**Find where response is checked** and ensure it never reaches user:
```csharp
var response = await _openAiService.SomeMethod(...);

// Add this check BEFORE returning to user
if (response == "FALLBACK_TO_EXISTING_SYSTEM")
{
    _logger.LogInformation("Triggering fallback to week letter system");
    return await FallbackToWeekLetterSystemAsync(...);
}

// Only return response to user if it's NOT the fallback signal
return response;
```

**Files to check**:
- `src/Aula/Integration/AgentService.cs` (main location)
- Any other places that call OpenAI service and return to user

**Estimated effort**: 30 minutes
**Test**: Mock scenario where FALLBACK is returned, verify user never sees it

---

### Fix 3: Add Structured Event Extraction (Optional but Recommended) ⚠️

**Current flow**: User asks question → LLM reads entire letter conversationally → Answers

**Proposed flow**:
1. On week letter load, extract all structured events:
   ```json
   {
     "monday": [...],
     "tuesday": [...],
     "thursday": [
       {"activity": "Foto", "time": "10.35-11.15"},
       {"activity": "Staveprøve dansk", "time": "12.45"}
     ]
   }
   ```
2. When user asks about Thursday, answer from structured data
3. LLM has explicit list to work with, reducing comprehension errors

**Implementation**:
- Use existing `CreateKeyInformationExtractionMessages()` in `PromptBuilder.cs:51-62`
- Store extracted structure in conversation context
- Reference structured data when answering day-specific questions

**Estimated effort**: 2-3 hours
**Benefit**: More reliable, testable, less prone to hallucination

---

### Fix 4: Add Explicit Multi-Event Reminder 🔄

**File**: `src/Aula/Services/PromptBuilder.cs`

**Add after line 30**:
```csharp
"CRITICAL: If multiple activities are scheduled for the same day, ALWAYS list ALL of them when asked about that day. " +
"For example, if both 'Foto' and 'Staveprøve' are on Thursday, mention both. " +
"If asked 'Skal han andet?' after mentioning one activity, re-read the letter carefully to check for additional activities on that day."
```

**Estimated effort**: 10 minutes

---

## Testing Strategy

### Test Case 1: Multi-Event Day Parsing
```
Week letter content:
  Foto: Torsdag 10.35-11.15
  Staveprøve dansk: Torsdag kl. 12.45

Questions to test:
  1. "Hvad skal [child] på torsdag?"
     Expected: Mention BOTH Foto and Staveprøve

  2. "Skal [child] andet på torsdag?" (after first answer)
     Expected: Mention the other event, not "nej"

  3. "Skal [child] fotograferes på torsdag?"
     Expected: "Ja, kl. 10.35-11.15"
```

### Test Case 2: System Prompt Leakage
```
Scenario: Trigger fallback condition
Expected: User receives fallback response, NOT "FALLBACK_TO_EXISTING_SYSTEM"
```

### Test Case 3: Context Consistency
```
Scenario: Multi-turn conversation about same day
Expected: Answers remain consistent, no contradictions
```

---

## Implementation Priority

1. **Fix 2** (System prompt leakage) - CRITICAL ⚠️ (30 min)
2. **Fix 1** (Event parsing instructions) - HIGH 🔴 (15 min)
3. **Fix 4** (Multi-event reminder) - HIGH 🔴 (10 min)
4. **Fix 3** (Structured extraction) - MEDIUM 🟡 (2-3 hours, optional)

**Total estimated time** (without optional Fix 3): ~1 hour

---

## Success Criteria

✅ LLM mentions ALL scheduled events for a given day when asked
✅ "FALLBACK_TO_EXISTING_SYSTEM" never appears in user-facing responses
✅ Answers remain consistent across conversation turns
✅ Direct questions about specific events (like "Skal han fotograferes?") are answered correctly

---

## Related Files

- `src/Aula/Services/PromptBuilder.cs` (prompt engineering)
- `src/Aula/Services/OpenAiService.cs` (FALLBACK constant)
- `src/Aula/Integration/AgentService.cs` (fallback handling)
- `src/Aula.Tests/Services/OpenAiServiceTests.cs` (add new tests)

---

## Notes

- This issue affects Danish language responses but likely exists in English too
- Week letter format varies (sometimes "Activity: Day Time", sometimes different)
- Consider adding telemetry to track when multi-event days are mentioned incompletely
- May want to add a "re-check" step before sending final answer for day-specific queries

---

**Created by**: Agent analysis of production dialogue
**Date**: 2025-10-01
**Sprint**: Post-Sprint 1 (bug fix)
