# Aula Integration Bot

An intelligent bot that integrates with the Danish school platform **Aula** (via MinUddannelse) to provide automated school communication and interactive assistance for parents.

## What This Project Does

### Core Functionality
- **Authenticates with MinUddannelse** using UniLogin credentials to access Aula data
- **Fetches weekly letters** from your children's school classes
- **Posts weekly updates** automatically to Slack and Telegram channels
- **Interactive chat support** - Ask questions about your children's school activities in natural language
- **Smart reminders** - Create and manage reminders, including AI-generated ones based on school content
- **Calendar integration** - Sync school events to Google Calendar

### Evolution: From Dumb Bot to Intelligent Agent

**Original (Dumb Bot):**
- Simple scheduled job running Sundays at 16:00
- Fetched week letters and dumped them to channels
- No interaction, just automated posting

**Current (Intelligent Agent):**
- Full LLM integration with OpenAI for natural language interaction
- Interactive chat in both Slack and Telegram
- AI-powered reminder creation and management
- Function calling for calendar integration and reminder tools
- Context-aware conversations about school activities

## Key Features

### Interactive Chat
- Ask questions in natural language (Danish/English): *"Hvad skal Søren i dag?"*
- Get intelligent answers about children's school activities
- Context-aware follow-up conversations
- Automatic language detection and response matching

### Smart Reminders
- Create reminders via natural language: *"remind me tomorrow at 8:00 that Hans has field trip"*
- AI extracts dates, times, and context automatically
- Cross-platform delivery (both Slack and Telegram)
- Database-driven scheduling with 10-second responsiveness

### Automated Scheduling
- Weekly letter posting (Sundays at 16:00) **Currently needs rewiring**
- Real-time reminder checking every 10 seconds
- Missed reminder recovery on startup
- Cron-based task scheduling via Supabase

## Critical Hosting Requirement

**MinUddannelse blocks major cloud providers** (AWS, Azure, GCP, etc.) by IP range. You must host this:
- **Locally** on your home network
- **VPS providers** not on major cloud infrastructure  
- **Dedicated servers** with residential/business IP ranges

This is a **hard requirement** - the bot will not work on major cloud platforms.

## Current Limitations & TODOs

### Known Issues
1. **Weekly letter timer missing** - Automatic Sunday posting not wired up to new SchedulingService
2. **No user profiles** - Single family configuration, no multi-user support
3. **No mention detection** - Bot responds to all messages, no @mention filtering

### Future Roadmap
- [ ] **Fix weekly letter scheduling** - Wire up Sunday 16:00 posting to SchedulingService timer
- [ ] **User profile system** - Support multiple families/users in same channels
- [ ] **Mention-based activation** - Only respond when explicitly mentioned (@AulaBot)
- [ ] **Enhanced AI features** - Automatic reminder generation from week letter content
- [ ] **Improved calendar sync** - Better integration with family calendar systems

## Setup

1. Clone this repository
2. Copy `appsettings.json.example` to `appsettings.json`
3. Fill in your UniLogin credentials, OpenAI API key, and platform configurations
4. **Ensure hosting environment** can reach MinUddannelse (not on blocked cloud providers)
5. **Set up database**: Follow [Supabase Setup Guide](SUPABASE_SETUP.md) for reminders and scheduling
6. Build and run the application

**Additional Documentation:**
- [Supabase Setup Guide](SUPABASE_SETUP.md) - Database configuration for reminders and scheduling
- [Troubleshooting Guide](TROUBLESHOOTING.md) - Common issues and solutions

### Slack Configuration

- Create a Slack app at https://api.slack.com/apps
- Add a webhook URL to post messages to a channel
- For interactive bot functionality:
  - Enable Socket Mode in your Slack app
  - Add the Bot Token Scopes: `app_mentions:read`, `channels:history`, `chat:write`, `im:history`, `im:write`
  - Install the app to your workspace
  - Copy the Bot User OAuth Token to the `ApiToken` field in `appsettings.json`
  - Set `EnableInteractiveBot` to `true` in `appsettings.json`
  - Set `ChannelId` to the ID of the channel where you want the bot to listen
  - Set `PostWeekLettersOnStartup` to control whether week letters are automatically posted on startup

### Telegram Configuration

- Create a Telegram bot using BotFather (https://t.me/botfather)
- Copy the token to the `Token` field in `appsettings.json`
- Create a channel and add your bot as an administrator
- Copy the channel ID to the `ChannelId` field in `appsettings.json`
- Set `Enabled` to `true` in `appsettings.json`
- Set `PostWeekLettersOnStartup` to control whether week letters are automatically posted on startup

### Google Calendar Configuration

- Create a Google Cloud project
- Enable the Google Calendar API
- Create a service account
- Download the service account key as JSON
- Copy the contents of the JSON file to the `GoogleServiceAccount` section in `appsettings.json`
- Create a calendar for each child and share it with the service account email
- Copy the calendar ID to the `GoogleCalendarId` field for each child in `appsettings.json`

## Usage

Run the application to fetch and post weekly letters:

```bash
cd src/Aula
dotnet run
```

The application will:
1. Login to Aula via UniLogin
2. Fetch weekly letters for your children
3. Post weekly letters to Slack and/or Telegram (if configured to do so on startup)
4. Start interactive bots (if enabled)
5. Keep running to handle interactive requests

## Docker

A Dockerfile is provided to run the application in a container:

```bash
docker build -t aula-bot -f src/Aula.Api/Dockerfile .
docker run -d --name aula-bot aula-bot
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Integration Options

### Telegram Integration

The application now supports an interactive Telegram bot that can answer questions about your children's school activities based on their weekly letters.

#### Setting up a Telegram Bot

1. Start a chat with [@BotFather](https://t.me/botfather) on Telegram
2. Send the command `/newbot` and follow the instructions to create a new bot
3. Once created, BotFather will provide you with a token (looks like `123456789:ABCDEF...`)
4. Copy this token to the `Token` field in the `Telegram` section of your `appsettings.json` file

#### Configuring the Telegram Integration

In your `appsettings.json` file:

```json
"Telegram": {
    "Enabled": true,
    "BotName": "YourAulaBot",
    "Token": "123456789:ABCDEF-your-bot-token-here",
    "ChannelId": "@yourchannel or -100123456789"
}
```

- `Enabled`: Set to `true` to enable the Telegram integration
- `BotName`: The name of your bot (for reference only)
- `Token`: The token provided by BotFather
- `ChannelId`: Where weekly letters will be posted:
  - For public channels: use the username with @ prefix (e.g., `@mychannel`)
  - For private channels: use the numeric ID (e.g., `-100123456789`)
  - For private chats: use the numeric chat ID

#### Using the Telegram Bot

Once configured and running, you can:

1. Add your bot to a channel where it will post weekly letters
2. Start a private chat with the bot to ask questions about your children's activities
3. Ask questions like:
   - "What is Søren doing tomorrow?"
   - "Does Hans have homework for Tuesday?"
   - "What activities are planned for this week?"

The bot supports both Danish and English questions and will respond in the same language as your query.

### Slack Integration

Refer to the original documentation for setting up Slack integration.
