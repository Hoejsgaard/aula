# MinUddannelse Family Assistant

> **Finally, push notifications for school communications!** 🎓📱

An intelligent family automation system that integrates with the Danish school platform **MinUddannelse** to provide automated school communication and interactive AI assistance for busy parents.

## The Problem This Solves

**Every Danish parent knows the frustration**: You need to manually log into MinUddannelse (Aula) constantly just to check what's happening at your children's school this week. Is there homework due tomorrow? A field trip on Thursday? Parent meeting next week?

**This project eliminates that friction** by:
- ✅ Automatically fetching weekly school communications
- ✅ Pushing updates directly to your family's Slack and Telegram channels
- ✅ Providing an AI assistant that knows your children's schedules
- ✅ Setting up intelligent reminders for important events
- ✅ Syncing school events to your family calendar

**The result?** School information flows to you seamlessly, without the daily login ritual that makes managing multiple children's schedules a chore.

## What This Project Does

### Core Functionality
- **🔐 Authenticates with MinUddannelse** using secure UniLogin SAML flow (per child)
- **📄 Fetches weekly letters** from your children's school classes automatically
- **📢 Posts updates** to configured Slack and Telegram channels
- **🤖 Interactive AI chat** - Ask questions about school activities in natural language (Danish/English)
- **⏰ Smart reminders** - Create and manage AI-powered reminders based on school content
- **📅 Calendar integration** - Sync school events to Google Calendar automatically

### Evolution: From Manual Checking to Proactive Intelligence

**Before (Manual Hell):**
- Log into MinUddannelse multiple times per week per child
- Manually check for homework, events, and updates
- Risk missing important deadlines or events
- No central family view of school activities

**Current (Intelligent Assistant):**
- Automatic weekly letter fetching and distribution
- AI-powered natural language interaction via Slack/Telegram
- Context-aware conversations about each child's activities
- Proactive reminder creation with smart scheduling
- Cross-platform family coordination

**Future (Autonomous Agent):**
- Automatic analysis of weekly letters for action items
- Proactive reminder setup: *"Hey, remember to pack light tomorrow - Emma's class has a field trip to the zoo!"*
- Smart homework tracking and parent notifications
- Family calendar coordination with conflict detection

## Recent Quality Improvements (October 2025)

**✅ Sprint 2: Code Quality & Security Hardening**
- **Architectural consolidation**: Unified 3 duplicate SAML authentication implementations
- **Code reduction**: Eliminated 174 lines of duplicate code (-16%)
- **Complexity reduction**: 70% decrease in authentication flow complexity
- **Memory management**: Proper disposal patterns and resource cleanup
- **Test coverage**: All 1,533 tests passing with 68.66% line coverage
- **Security hardening**: Input validation, rate limiting, and audit trails

**🏗️ Architecture Enhancements:**
- Child-centric agent pattern for multi-child isolation
- Template Method pattern for extensible authentication
- Repository pattern with proper dependency injection
- Channel abstraction for easy integration expansion

## Security & Production Readiness

### ⚠️ **IMPORTANT: Secrets Management**
**BEFORE DEPLOYMENT**: Migrate secrets from `appsettings.json` to secure storage:

```bash
# For local development - use dotnet user-secrets
cd src/MinUddannelse
dotnet user-secrets init
dotnet user-secrets set "MinUddannelse:Children:0:UniLogin:Password" "your-password"
dotnet user-secrets set "OpenAi:ApiKey" "sk-proj-your-key"
```

### 🔒 **Security Features**
- **Multi-tenant isolation**: Each child's data is strictly isolated
- **Input sanitization**: 24 blocked patterns protect against prompt injection
- **Rate limiting**: Per-child, per-operation limits prevent abuse
- **Audit logging**: Full trail of authentication and data access
- **SAML authentication**: Secure integration with Danish UniLogin system

### 📊 **Production Quality Metrics**
- **68.66% test coverage** with 1,533 automated tests
- **Zero critical vulnerabilities** (after secret migration)
- **Proper error handling** with graceful degradation
- **Resource management** with automatic cleanup
- **Logging & monitoring** with structured audit trails

## Critical Hosting Requirement

**⚠️ MinUddannelse blocks major cloud providers** (AWS, Azure, GCP, etc.) by IP range.

**✅ Supported hosting:**
- **Home server/NAS** on residential internet
- **VPS providers** not on major cloud infrastructure
- **Dedicated servers** with business/residential IP ranges

**❌ Blocked hosting:**
- AWS EC2, Azure VMs, Google Cloud Compute
- Most major cloud platforms
- Corporate datacenter IP ranges

This is a **hard technical requirement** - the authentication will fail on blocked IPs.

## Key Features

### 🤖 Interactive AI Chat
Ask natural questions in Danish or English:
- *"Hvad skal Emma i morgen?"* (What does Emma have tomorrow?)
- *"Does Oliver have homework for Tuesday?"*
- *"What field trips are coming up this month?"*
- *"Remind me to pack gym clothes tomorrow at 7:00"*

**Features:**
- Context-aware follow-up conversations
- Automatic language detection and matching
- Family-specific information (knows your children's names and schedules)
- Cross-platform availability (same AI on Slack and Telegram)

### ⏰ Smart Reminders
Create intelligent reminders via natural language:
- *"remind me tomorrow at 8:00 that Oliver has show-and-tell"*
- *"set a reminder for Thursday morning about Emma's field trip"*

**Features:**
- AI-powered date/time extraction
- Cross-platform delivery (Slack + Telegram)
- 10-second responsiveness for time-critical reminders
- Automatic retry on missed deliveries
- Persistent storage with startup recovery

### 📅 Automated Scheduling
- **Weekly letter posting**: Configurable automatic distribution
- **Real-time reminder checking**: 10-second precision timing
- **Startup recovery**: Catches any missed reminders on restart
- **Cron-based scheduling**: Flexible timing via database configuration

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Access to Danish MinUddannelse/UniLogin (for your children)
- Slack workspace or Telegram bot (for notifications)
- OpenAI API key (for AI features)
- Supabase account (for data storage)

### Quick Setup

1. **Clone and configure:**
   ```bash
   git clone <repository-url>
   cd MinUddannelse
   cp src/MinUddannelse/appsettings.example.json src/MinUddannelse/appsettings.json
   ```

2. **Secure secret management:**
   ```bash
   cd src/MinUddannelse
   dotnet user-secrets init
   # Add your secrets via user-secrets (see Security section above)
   ```

3. **Configure integrations** in `appsettings.json`:
   - **Children**: UniLogin credentials per child
   - **Slack/Telegram**: Bot tokens and channel IDs
   - **OpenAI**: API key for AI features
   - **Supabase**: Database connection
   - **Google Calendar**: Service account (optional)

4. **Set up database** (see [Supabase Setup Guide](doc/SUPABASE_SETUP.md))

5. **Run the application:**
   ```bash
   dotnet run
   ```

### Detailed Configuration Guides

#### 📱 Telegram Setup (Recommended for families)
1. **Create bot with [@BotFather](https://t.me/botfather):**
   ```
   /newbot
   → Follow prompts to name your bot
   → Save the provided token
   ```

2. **Configure channels per child:**
   ```json
   {
     "Children": [
       {
         "FirstName": "Emma",
         "LastName": "Johnson",
         "Channels": {
           "Telegram": {
             "Enabled": true,
             "Token": "123456789:ABCDEF-your-bot-token",
             "ChannelId": "-100123456789",
             "EnableInteractiveBot": true
           }
         }
       }
     ]
   }
   ```

3. **Get channel ID:**
   - Create private channel, add bot as admin
   - Use [@userinfobot](https://t.me/userinfobot) to get numeric channel ID
   - Private channels start with `-100`

#### 🏢 Slack Setup (Good for tech-savvy families)
1. **Create Slack app** at [api.slack.com/apps](https://api.slack.com/apps)
2. **Enable features:**
   - Webhooks for posting messages
   - Socket Mode for interactive chat
   - Bot Token Scopes: `app_mentions:read`, `channels:history`, `chat:write`
3. **Configure per child** with workspace tokens and channel IDs

#### 🤖 OpenAI Setup (Required for AI features)
1. **Get API key** from [platform.openai.com](https://platform.openai.com)
2. **Configure via user-secrets:**
   ```bash
   dotnet user-secrets set "OpenAi:ApiKey" "sk-proj-your-key-here"
   dotnet user-secrets set "OpenAi:Model" "gpt-3.5-turbo"
   ```

#### 📊 Supabase Setup (Required for reminders/scheduling)
1. **Create project** at [supabase.com](https://supabase.com)
2. **Follow detailed setup**: [Supabase Setup Guide](doc/SUPABASE_SETUP.md)
3. **Configure connection** via user-secrets

## Usage Examples

Once running, the assistant automatically:

### 📄 Weekly Letter Distribution
- Fetches new weekly letters every Sunday at 16:00 (configurable)
- Posts formatted summaries to all configured channels
- Maintains history to avoid duplicate posting
- Handles authentication renewal automatically

### 💬 Interactive Conversations
**Telegram/Slack chat examples:**
```
Parent: "What does Emma have tomorrow?"
Bot: "Tomorrow Emma has:
• Math class at 10:00-11:00
• Field trip to the Science Museum (remember to pack lunch!)
• No homework due"

Parent: "Remind me tonight at 8pm to prepare Emma's lunch"
Bot: "✅ I'll remind you tonight at 20:00 to prepare Emma's lunch for tomorrow's field trip"
```

### 📅 Automatic Calendar Sync
- Creates Google Calendar events for each child's activities
- Updates existing events when school information changes
- Syncs field trips, parent meetings, and special events
- Maintains separate calendars per child for organization

## Architecture Overview

### 🏗️ Child-Centric Design
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Child Agent   │    │   Child Agent   │    │   Child Agent   │
│     (Emma)      │    │    (Oliver)     │    │     (Lily)      │
├─────────────────┤    ├─────────────────┤    ├─────────────────┤
│ UniLogin Auth   │    │ UniLogin Auth   │    │ UniLogin Auth   │
│ Week Letters    │    │ Week Letters    │    │ Week Letters    │
│ Slack Channel   │    │ Telegram Bot    │    │ Both Channels   │
│ Calendar Sync   │    │ AI Assistant    │    │ Full Features   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                    ┌─────────────────────┐
                    │   Shared Services   │
                    ├─────────────────────┤
                    │ OpenAI Integration  │
                    │ Reminder Engine     │
                    │ Scheduling Service  │
                    │ Audit & Security    │
                    └─────────────────────┘
```

### 🔧 Modern .NET Patterns
- **Dependency Injection**: Full IoC container with proper lifetimes
- **Repository Pattern**: Data access abstraction with Supabase
- **Factory Pattern**: Child agent creation and management
- **Template Method**: Extensible authentication flows
- **Channel Abstraction**: Easy integration with new platforms

### 🔒 Security Architecture
- **Multi-tenant isolation**: Database-level separation per child
- **Input validation**: Comprehensive sanitization against injection attacks
- **Rate limiting**: Sliding window algorithm per child/operation
- **Audit logging**: Full activity trail with severity levels
- **Authentication flows**: Secure SAML integration with Danish UniLogin

## Roadmap & Future Features

### 🔮 Phase 1: Enhanced Intelligence (Q1 2026)
- [ ] **Automatic reminder extraction** from weekly letters
  - *"Field trip Thursday → Auto-remind Wednesday evening to pack light"*
- [ ] **Homework tracking** with deadline reminders
- [ ] **Parent meeting coordination** with calendar conflict detection
- [ ] **Weather-aware notifications** for outdoor activities

### 🚀 Phase 2: Family Ecosystem (Q2 2026)
- [ ] **Multi-family support** in shared channels
- [ ] **@mention activation** (respond only when explicitly mentioned)
- [ ] **Sibling coordination** (detect schedule conflicts)
- [ ] **Parent task assignment** with completion tracking

### 🏗️ Phase 3: Platform Expansion (Q3 2026)
- [ ] **WhatsApp integration** for broader family reach
- [ ] **Microsoft Teams** support for corporate families
- [ ] **iOS/Android apps** with push notifications
- [ ] **Web dashboard** for family schedule overview

## Contributing

This is a family-focused project, but contributions are welcome! Areas where help is needed:

### 🛠️ Development
- **New channel integrations** (WhatsApp, Discord, etc.)
- **Enhanced AI prompts** for better Danish/English understanding
- **Calendar integration improvements** (Outlook, Apple Calendar)
- **Mobile app development** (React Native, Flutter)

### 🧪 Testing
- **Multi-child testing** with different school systems
- **Edge case handling** (holidays, system downtime)
- **Performance testing** with large families
- **Security testing** (penetration testing welcome)

### 📚 Documentation
- **Translation** of documentation to Danish
- **Video tutorials** for setup and usage
- **Best practices guides** for family digital organization

## Technical Documentation

- **[Architecture Guide](doc/ARCHITECTURE.md)** - Detailed system design
- **[Security Assessment](doc/SECURITY.md)** - Comprehensive security review
- **[Supabase Setup](doc/SUPABASE_SETUP.md)** - Database configuration
- **[Troubleshooting](doc/TROUBLESHOOTING.md)** - Common issues and solutions
- **[API Documentation](doc/API.md)** - Integration reference

## Support & Community

### 🤝 Getting Help
- **GitHub Issues**: Bug reports and feature requests
- **Discussions**: Setup help and usage questions
- **Security Issues**: Email directly for security-related concerns

### 📞 Professional Support
This project is maintained by a small team of Danish parents who understand the struggle of managing multiple children's school schedules. We offer:

- **Setup assistance** for technical families
- **Custom integrations** for specific school systems
- **Family workflow consulting** for digital organization

## License

MIT License - Use this freely to make your family's life easier!

See [LICENSE](LICENSE) for full details.

---

## Acknowledgments

**Built by parents, for parents.** 🏠

Special thanks to:
- **The Danish school system** for providing APIs (even if they block cloud hosting 😅)
- **OpenAI** for making AI accessible to small family projects
- **Supabase** for excellent open-source backend infrastructure
- **The .NET community** for world-class development tools

**Most importantly**: Thanks to all the Danish families beta-testing this system and providing feedback on making school communication less painful!

---

*"Because checking MinUddannelse 17 times a week shouldn't be a parent's job."*