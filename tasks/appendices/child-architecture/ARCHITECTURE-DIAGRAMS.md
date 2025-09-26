# Architecture Transformation Diagrams

## Current Architecture: Service-Centric
```
┌─────────────────────────────────────────────────────────────────┐
│                         GLOBAL SPACE                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   [Program.cs]                                                   │
│        │                                                         │
│        ├──> [AgentService] ──> [MinUddannelseClient]            │
│        │         │                     │                        │
│        │         ├─────────────────────┤                        │
│        │         ▼                     ▼                        │
│        │    [DataService]     [Authentication]                  │
│        │         │                     │                        │
│        │         ├──────[Cache]────────┤                        │
│        │         │                                              │
│        │         ▼                                              │
│        └──> [ChannelManager] ──┬──> [SlackChannel]  ────┐      │
│                                 │                         │      │
│                                 └──> [TelegramChannel] ──┤      │
│                                                          │      │
│                                                          ▼      │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │ BROADCAST TO ALL:                                       │  │
│   │ • TestChild1's letters → All channels                  │  │
│   │ • TestChild2's letters → All channels                        │  │
│   │ • No isolation, no routing, no preferences              │  │
│   └──────────────────────────────────────────────────────────┘  │
│                                                                  │
│   PROBLEMS:                                                      │
│   ❌ Children passed as parameters everywhere                    │
│   ❌ No child-specific configuration                             │
│   ❌ Global broadcast to all channels                            │
│   ❌ Cache key collisions (FirstName based)                      │
└─────────────────────────────────────────────────────────────────┘
```

## Proposed Architecture: Child-Centric
```
┌─────────────────────────────────────────────────────────────────┐
│                    CHILD ORCHESTRATOR                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────┐    ┌─────────────────────────┐    │
│  │   HANS MARTIN WORLD      │    │   SØREN JOHANNES WORLD  │    │
│  ├─────────────────────────┤    ├─────────────────────────┤    │
│  │                          │    │                          │    │
│  │  [ChildId: testchild1-2025]   │    │  [ChildId: soren-2025]   │    │
│  │                          │    │                          │    │
│  │  ┌──────────────────┐    │    │  ┌──────────────────┐    │    │
│  │  │ Service Container│    │    │  │ Service Container│    │    │
│  │  ├──────────────────┤    │    │  ├──────────────────┤    │    │
│  │  │ • Authentication │    │    │  │ • Authentication │    │    │
│  │  │ • Data Service   │    │    │  │ • Data Service   │    │    │
│  │  │ • Cache (isolated)│   │    │  │ • Cache (isolated)│   │    │
│  │  │ • Week Letters   │    │    │  │ • Week Letters   │    │    │
│  │  └────────┬─────────┘    │    │  └────────┬─────────┘    │    │
│  │           │              │    │           │              │    │
│  │  ┌────────▼─────────┐    │    │  ┌────────▼─────────┐    │    │
│  │  │ Channel Manager  │    │    │  │ Channel Manager  │    │    │
│  │  ├──────────────────┤    │    │  ├──────────────────┤    │    │
│  │  │ Slack: C123456   │────┼────┼─▶│ Slack: C789012   │    │    │
│  │  │ (TestChild1's class)   │    │    │  │ (TestChild2's class)  │    │    │
│  │  │                  │    │    │  │                  │    │    │
│  │  │ Parents:         │    │    │  │ Parents:         │    │    │
│  │  │ • Parent1 ✓      │    │    │  │ • Parent1 ✓      │    │    │
│  │  │ • Parent2 ✓      │    │    │  │ • Parent3 ✗      │    │    │
│  │  │ • Parent3 ✓      │    │    │  │ • Parent4 ✓      │    │    │
│  │  └──────────────────┘    │    │  └──────────────────┘    │    │
│  │                          │    │                          │    │
│  └─────────────────────────┘    └─────────────────────────┘    │
│                                                                  │
│   BENEFITS:                                                      │
│   ✅ Complete data isolation between children                    │
│   ✅ Per-child channel configuration                             │
│   ✅ Parent access control                                       │
│   ✅ Stable identity (ChildId)                                   │
└─────────────────────────────────────────────────────────────────┘
```

## Data Flow Comparison

### Current Flow: Mixed Data
```
  [Week Letter Fetch]
         │
         ▼
   [AgentService]
         │
    ┌────┴────┐
    │ TestChild1    │ TestChild2
    │ Martin  │ Johannes
    └────┬────┘
         │
         ▼
   [Single Cache]
   "WeekLetter:TestChild1:39:2025"  ← String keys, collision risk
   "WeekLetter:TestChild2:39:2025"
         │
         ▼
   [ChannelManager]
         │
         ├──> Slack Channel #general
         │    ├── TestChild1's letter    ← Mixed together
         │    └── TestChild2's letter
         │
         └──> Telegram Channel
              ├── TestChild1's letter    ← Mixed together
              └── TestChild2's letter
```

### Proposed Flow: Isolated Data
```
  [Week Letter Fetch]
         │
    ┌────┴────┐
    │         │
    ▼         ▼
[TestChild1 Container] [TestChild2 Container]
    │              │
    ▼              ▼
[TestChild1 Cache]    [TestChild2 Cache]
"testchild1-2025:39"  "soren-2025:39"  ← Stable IDs, no collision
    │              │
    ▼              ▼
[TestChild1 Channels] [TestChild2 Channels]
    │              │
    ▼              ▼
Slack #C123456  Slack #C789012
(TestChild1's class)  (TestChild2's class)
    │              │
Only TestChild1's     Only TestChild2's
  parents         parents
```

## Channel Access Control Model

### Current: No Access Control
```
┌──────────────────────────────┐
│      SLACK WORKSPACE         │
├──────────────────────────────┤
│                              │
│  #general-channel            │
│  ├── ALL parents see:        │
│  │   • TestChild1's letters        │
│  │   • TestChild2's letters       │
│  │   • Other children        │
│  └── No privacy              │
│                              │
└──────────────────────────────┘
```

### Proposed: Fine-Grained Access
```
┌──────────────────────────────┐     ┌──────────────────────────────┐
│    HANS'S CHANNEL SPACE      │     │   SØREN'S CHANNEL SPACE      │
├──────────────────────────────┤     ├──────────────────────────────┤
│                              │     │                              │
│  #testchild1-class-2c              │     │  #soren-class-4a             │
│  ├── Authorized Parents:     │     │  ├── Authorized Parents:     │
│  │   ✅ You                  │     │  │   ✅ You                  │
│  │   ✅ TestChild1's Mom           │     │  │   ❌ TestChild1's Mom           │
│  │   ✅ Classmate Parent 1   │     │  │   ✅ Different Parent     │
│  │   ✅ Classmate Parent 2   │     │  │   ✅ Teacher              │
│  │                          │     │  │                          │
│  ├── Content:               │     │  ├── Content:               │
│  │   • TestChild1's week letters  │     │  │   • TestChild2's week letters  │
│  │   • TestChild1's schedule      │     │  │   • TestChild2's schedule      │
│  │   • TestChild1's reminders     │     │  │   • TestChild2's reminders     │
│  └── Complete isolation     │     │  └── Complete isolation     │
│                              │     │                              │
└──────────────────────────────┘     └──────────────────────────────┘
```

## Service Lifecycle Management

### Current: Global Lifecycle
```
[Application Start]
        │
        ▼
[Initialize Services]
        │
        ├──> AgentService (singleton)
        ├──> DataService (singleton)
        ├──> ChannelManager (singleton)
        │
        ▼
[All Children Share Services]
        │
        ▼
[Single Point of Failure]
```

### Proposed: Per-Child Lifecycle
```
[Application Start]
        │
        ▼
[Child Orchestrator]
        │
        ├──> [Initialize TestChild1 Container]
        │     ├──> TestChild1 Authentication
        │     ├──> TestChild1 Data Service
        │     ├──> TestChild1 Cache
        │     └──> TestChild1 Channels
        │
        ├──> [Initialize TestChild2 Container]
        │     ├──> TestChild2 Authentication
        │     ├──> TestChild2 Data Service
        │     ├──> TestChild2 Cache
        │     └──> TestChild2 Channels
        │
        ▼
[Independent Health Monitoring]
        │
        ├──> TestChild1 Health Check ──> Auto-recovery
        └──> TestChild2 Health Check ──> Auto-recovery
```

## Migration Path Visualization

```
WEEK 1-3: Foundation
────────────────────
Current State          Transitional              Goal State
    Child     ─────>   Child + ChildId  ─────>   ChildId only
 (FirstName)         (Backward compat)         (Stable identity)

WEEK 4-6: Service Isolation
──────────────────────────
Global Services ─────> Hybrid Model      ─────> Child Containers
                     (Feature flagged)         (Full isolation)

WEEK 7-9: Channel Revolution
───────────────────────────
Broadcast All   ─────> Selective Send    ─────> Child Channels
                     (Gradual rollout)         (Parent access control)

WEEK 10: Completion
──────────────────
Old Code       ─────> Cleanup           ─────> Pure Architecture
(Disabled)           (Remove flags)            (Child-centric)
```

## Success Metrics Dashboard

```
┌─────────────────────────────────────────────────────────────────┐
│                    MIGRATION SUCCESS METRICS                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Data Isolation:          [████████████████████] 100%           │
│  - TestChild1 data in TestChild1 channels only ✓                            │
│  - TestChild2 data in TestChild2 channels only ✓                          │
│                                                                  │
│  Performance:              [████████████░░░░░░░] 75%            │
│  - Cache hit rate improved 30% ✓                                │
│  - Response time decreased 25% ✓                                │
│  - Memory usage optimization pending                            │
│                                                                  │
│  Parent Satisfaction:      [████████████████████] 100%          │
│  - Can share TestChild1's channel with class ✓                        │
│  - TestChild2's data stays private ✓                                 │
│  - Easy parent invitation system ✓                              │
│                                                                  │
│  Code Quality:             [██████████████░░░░░] 85%            │
│  - Clear domain boundaries ✓                                    │
│  - Reduced coupling ✓                                           │
│  - Improved testability ✓                                       │
│  - Documentation updates in progress                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```