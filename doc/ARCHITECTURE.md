# Aula Architecture Documentation

This document provides architectural overview and diagrams for the Aula Family Assistant application.

---

## Table of Contents
- [System Overview](#system-overview)
- [Authentication Architecture](#authentication-architecture)
- [Agent Architecture](#agent-architecture)
- [Data Flow](#data-flow)
- [Key Design Patterns](#key-design-patterns)

---

## System Overview

Aula is a .NET 9.0 console application that integrates with the Danish school platform via MinUddannelse to provide automated school communication and interactive assistance for parents.

```mermaid
graph TB
    subgraph "External Systems"
        AULA[Aula/MinUddannelse<br/>Danish School Platform]
        SLACK[Slack Workspace]
        TELEGRAM[Telegram]
        OPENAI[OpenAI GPT-3.5]
        SUPABASE[(Supabase PostgreSQL)]
    end

    subgraph "Aula Application"
        PROGRAM[Program.cs<br/>DI Container & Startup]
        AGENTS[Child Agents<br/>Per-Child Intelligence]
        AUTH[Authentication<br/>UniLogin SAML]
        SCHED[SchedulingService<br/>Cron-based Tasks]
        SERVICES[Business Services<br/>Week Letters, AI, etc.]
        BOTS[Interactive Bots<br/>Slack & Telegram]
        REPOS[Repositories<br/>Data Access Layer]
    end

    PROGRAM --> AGENTS
    PROGRAM --> SCHED
    PROGRAM --> SERVICES

    AGENTS --> BOTS
    AGENTS --> SERVICES

    AUTH --> AULA
    SERVICES --> AUTH
    SERVICES --> REPOS
    SERVICES --> OPENAI

    BOTS --> SLACK
    BOTS --> TELEGRAM

    REPOS --> SUPABASE

    SCHED --> SERVICES

    style AULA fill:#e1f5fe
    style SLACK fill:#e8f5e9
    style TELEGRAM fill:#e8f5e9
    style OPENAI fill:#fff3e0
    style SUPABASE fill:#f3e5f5
    style AGENTS fill:#ffebee
    style AUTH fill:#fce4ec
```

---

## Authentication Architecture

The authentication system uses SAML-based UniLogin to access MinUddannelse. After Sprint 2 refactoring, all authentication flows are unified under `UniLoginAuthenticatorBase`.

### Class Hierarchy

```mermaid
classDiagram
    class UniLoginAuthenticatorBase {
        <<abstract>>
        -HttpClient _httpClient
        -string _username
        -string _password
        +LoginAsync() Task~bool~
        #ProcessLoginResponseAsync() Task~bool~
        #VerifyAuthentication() Task~bool~
    }

    class IDisposable {
        <<interface>>
        +Dispose()
    }

    class MinUddannelseClient {
        -JObject _userProfile
        +GetWeekLetter() Task~JObject~
        +GetWeekSchedule() Task~JObject~
        -ExtractUserProfile() Task~JObject~
    }

    class PerChildMinUddannelseClient {
        +GetWeekLetter() Task~JObject~
        +GetWeekSchedule() Task~JObject~
        -ChildAuthenticatedClient
    }

    class PictogramAuthenticatedClient {
        -string[] _pictogramSequence
        #HandlePictogramAuthentication() Task~bool~
    }

    UniLoginAuthenticatorBase ..|> IDisposable
    MinUddannelseClient --|> UniLoginAuthenticatorBase
    PerChildMinUddannelseClient ..> ChildAuthenticatedClient
    ChildAuthenticatedClient --|> UniLoginAuthenticatorBase
    PictogramAuthenticatedClient --|> UniLoginAuthenticatorBase
```

### SAML Authentication Flow

```mermaid
sequenceDiagram
    participant App as Application
    participant Auth as UniLoginAuthenticatorBase
    participant UniLogin as UniLogin SAML
    participant MinUdd as MinUddannelse

    App->>Auth: LoginAsync()
    Auth->>UniLogin: GET /login (initiate SAML)
    UniLogin-->>Auth: Login form
    Auth->>UniLogin: POST credentials
    UniLogin-->>Auth: SAML response
    Auth->>MinUdd: SAML assertion
    MinUdd-->>Auth: Session cookie
    Auth->>MinUdd: VerifyAuthentication()
    MinUdd-->>Auth: User profile JSON
    Auth-->>App: Success (authenticated)
```

### Authentication Method Decomposition (Sprint 2)

After Sprint 2 refactoring, the complex 274-line `ProcessLoginResponseAsync` method was decomposed:

```mermaid
graph LR
    A[ProcessLoginResponseAsync<br/>80 lines<br/>Orchestration] --> B[TryVerifyAuthentication<br/>AfterCredentials]
    A --> C[CheckForAuthentication<br/>Success]
    A --> D[TrySubmitForm]
    A --> E[TryAlternativeNavigation]
    A --> F[LogFormStructure]
    A --> G[LogJavaScriptRedirects]
    A --> H[LogLoginLinks]
    A --> I[LogErrorMessages]

    style A fill:#4CAF50,color:#fff
    style B fill:#2196F3,color:#fff
    style C fill:#2196F3,color:#fff
    style D fill:#2196F3,color:#fff
    style E fill:#2196F3,color:#fff
    style F fill:#9E9E9E,color:#fff
    style G fill:#9E9E9E,color:#fff
    style H fill:#9E9E9E,color:#fff
    style I fill:#9E9E9E,color:#fff
```

---

## Agent Architecture

The application uses a per-child agent architecture where each child has their own intelligent agent coordinating communication channels.

### Agent Structure

```mermaid
graph TB
    subgraph "Child Agent (Søren)"
        AGT_S[ChildAgent<br/>Søren Johannes]
        SLACK_S[SlackInteractiveBot]
        TG_S[TelegramBot]
        HANDLER_S[ChildWeekLetterHandler]
    end

    subgraph "Child Agent (Hans)"
        AGT_H[ChildAgent<br/>Hans Martin]
        SLACK_H[SlackInteractiveBot]
        TG_H[TelegramBot]
        HANDLER_H[ChildWeekLetterHandler]
    end

    subgraph "Shared Services"
        WL_SVC[WeekLetterService]
        AI_SVC[OpenAiService]
        SCHED[SchedulingService]
    end

    AGT_S --> SLACK_S
    AGT_S --> TG_S
    AGT_S --> HANDLER_S
    AGT_S --> WL_SVC

    AGT_H --> SLACK_H
    AGT_H --> TG_H
    AGT_H --> HANDLER_H
    AGT_H --> WL_SVC

    SLACK_S --> AI_SVC
    SLACK_H --> AI_SVC

    SCHED --> WL_SVC
    SCHED --> HANDLER_S
    SCHED --> HANDLER_H

    style AGT_S fill:#ffcdd2
    style AGT_H fill:#c5cae9
    style WL_SVC fill:#fff9c4
    style AI_SVC fill:#f8bbd0
```

### Event-Driven Communication

```mermaid
sequenceDiagram
    participant Sched as SchedulingService
    participant WLSvc as WeekLetterService
    participant Agent as ChildAgent
    participant Handler as ChildWeekLetterHandler
    participant Slack as SlackBot

    Sched->>WLSvc: Check for new week letters
    WLSvc->>WLSvc: Fetch from MinUddannelse
    WLSvc->>Agent: Emit ChildWeekLetterReady event
    Agent->>Handler: Event propagates
    Handler->>Slack: PostWeekLetter()
    Slack-->>Handler: Posted successfully
    Handler-->>WLSvc: Mark as posted
```

---

## Data Flow

### Week Letter Retrieval Flow

```mermaid
flowchart TD
    START([User Request or<br/>Scheduled Task])

    START --> CHECK_DB{Check Database<br/>for Cached Letter}

    CHECK_DB -->|Found| RETURN_CACHED[Return Cached<br/>Week Letter]
    CHECK_DB -->|Not Found| CHECK_LIVE{Live Fetch<br/>Allowed?}

    CHECK_LIVE -->|No| RETURN_EMPTY[Return Empty<br/>Week Letter]
    CHECK_LIVE -->|Yes| AUTH[Authenticate with<br/>UniLogin SAML]

    AUTH --> FETCH[Fetch from<br/>MinUddannelse API]

    FETCH --> VALIDATE{Has<br/>Content?}

    VALIDATE -->|No Content| RETURN_EMPTY
    VALIDATE -->|Has Content| STORE[Store in Database<br/>with Content Hash]

    STORE --> RETURN_LIVE[Return Live<br/>Week Letter]

    RETURN_CACHED --> END([Week Letter<br/>Available])
    RETURN_EMPTY --> END
    RETURN_LIVE --> END

    style START fill:#4CAF50,color:#fff
    style END fill:#4CAF50,color:#fff
    style CHECK_DB fill:#2196F3,color:#fff
    style AUTH fill:#FF9800,color:#fff
    style STORE fill:#9C27B0,color:#fff
```

### AI Query Processing Flow

```mermaid
sequenceDiagram
    participant User as User (Slack/Telegram)
    participant Bot as InteractiveBot
    participant AI as OpenAiService
    participant WL as WeekLetterService
    participant OpenAI as OpenAI API

    User->>Bot: Ask question (Danish)
    Bot->>AI: GetAiResponse(childName, question)
    AI->>AI: Detect query type

    alt Week Letter Query
        AI->>WL: GetWeekLetter(child, date)
        WL-->>AI: Week letter JSON
        AI->>OpenAI: Process with week letter context
    else General Query
        AI->>OpenAI: Process without context
    end

    OpenAI-->>AI: AI response
    AI-->>Bot: Formatted response
    Bot-->>User: Reply in Slack/Telegram
```

---

## Key Design Patterns

### 1. Template Method Pattern
**Location**: `UniLoginAuthenticatorBase`
- Base class defines authentication flow skeleton
- Subclasses customize specific steps (e.g., pictogram handling)
- Reduces duplication while allowing customization

### 2. Repository Pattern
**Location**: `/Repositories`
- Abstracts data access behind interfaces
- Implementations: `WeekLetterRepository`, `ReminderRepository`, `ScheduledTaskRepository`
- Enables testability and separation of concerns

### 3. Dependency Injection
**Location**: `Program.cs`
- All services registered in DI container
- Constructor injection throughout
- Enables loose coupling and testability

### 4. Event-Driven Architecture
**Location**: Agent communication
- `ChildWeekLetterReady` event connects scheduling to posting
- Decouples week letter fetching from distribution
- Allows multiple handlers per event

### 5. Strategy Pattern
**Location**: Authentication methods
- `IChildAuthenticatedClient` interface
- Implementations: Standard password, Pictogram sequence
- Runtime selection based on child configuration

---

## Code Organization Principles

### Separation of Concerns

```
Integration/    ←  External API communication (MinUddannelse, UniLogin)
Services/       ←  Business logic (week letters, AI, reminders)
Repositories/   ←  Data persistence (Supabase)
Bots/           ←  Channel-specific bot implementations
Agents/         ←  Per-child orchestration and intelligence
Channels/       ←  Abstraction over Slack/Telegram
Scheduling/     ←  Time-based task execution
Utilities/      ←  Shared helper methods (Sprint 2)
```

### Testability

- ✅ 938 unit tests covering business logic
- ✅ Mocked dependencies via interfaces
- ✅ Public API testing (no reflection)
- ✅ Clear test naming conventions

---

## Recent Architectural Improvements (Sprint 2)

### Code Reduction
- **Before**: 3 duplicate authentication implementations (716 lines total)
- **After**: 1 unified `UniLoginAuthenticatorBase` (534 lines)
- **Savings**: 207 lines eliminated (-29%)

### Complexity Reduction
- **Before**: `ProcessLoginResponseAsync` - 274 lines, ~20 decision points
- **After**: Main method 80 lines + 8 helpers, ~6 decision points
- **Improvement**: 70% complexity reduction

### Utility Consolidation
- **Before**: 11 duplicate utility methods across 5 files
- **After**: `WeekLetterUtilities` static class
- **Methods**: `GetIsoWeekNumber`, `ComputeContentHash`, `CreateEmptyWeekLetter`

---

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Runtime | .NET 9.0 | Application platform |
| Language | C# 13 | Modern features (generated regex, using declarations) |
| Database | Supabase PostgreSQL | Reminders, scheduling, week letters |
| AI | OpenAI GPT-3.5-turbo | Natural language processing |
| Messaging | Slack API, Telegram Bot API | Parent communication |
| Authentication | UniLogin SAML | Danish school system access |
| Testing | xUnit, Moq | Unit testing framework |
| Dependency Injection | Microsoft.Extensions.DependencyInjection | IoC container |

---

## Performance Considerations

### Authentication
- Fresh client instances per request (memory managed via `using` statements)
- Session cookies cached within HttpClient
- SAML flow typically 3-5 round trips

### Week Letters
- Database-first approach (check cache before live fetch)
- Content hash comparison prevents duplicate posts
- Retry tracking for delayed letters

### AI Queries
- Automatic context detection (week letter vs general query)
- GPT-3.5-turbo for cost efficiency
- Danish/English language detection

### Scheduling
- 10-second reminder check interval
- 1-minute scheduled task check interval
- Event-driven posting (no polling)

---

## Security Considerations

### Credentials
- Per-child UniLogin credentials in `appsettings.json`
- Pictogram sequences stored as integer arrays
- Passwords never logged (masked in debug output)

### Multi-Tenancy
- Separate agents per child
- Isolated Slack/Telegram channels per child
- No cross-child data leakage

### Disposal
- `IDisposable` implementation on authenticated clients
- `using` statements for automatic cleanup
- Prevents HttpClient leaks (Sprint 2 fix)

---

## Future Architecture Considerations

### Scalability
- Current: Single-process console application
- Future: Could be adapted to web API with webhooks
- Consideration: MinUddannelse IP blocking limits cloud deployment

### Extensibility
- Channel abstraction allows adding new platforms (Discord, WhatsApp)
- Agent architecture supports different interaction patterns
- Repository pattern enables different data stores

### Observability
- Structured logging via Microsoft.Extensions.Logging
- Metrics potential: Authentication success rates, AI query latency
- Distributed tracing potential for debugging

---

*Last Updated: October 2025 (Sprint 2 Refactoring)*
