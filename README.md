# Aula Bot

A bot that fetches information from Aula (Danish school communication platform) and posts it to Slack and/or Telegram.

## Features

- Login to Aula via UniLogin
- Fetch weekly letters for your children
- Post weekly letters to Slack and/or Telegram
- Fetch schedule/calendar for your children
- Add schedule to Google Calendar
- Interactive bot for Slack and Telegram that can answer questions about your children's school activities

## Setup

1. Clone this repository
2. Copy `appsettings.json.example` to `appsettings.json`
3. Fill in your UniLogin credentials, Slack webhook URL, and other configuration
4. Build and run the application

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
   - "What is TestChild2 doing tomorrow?"
   - "Does TestChild1 have homework for Tuesday?"
   - "What activities are planned for this week?"

The bot supports both Danish and English questions and will respond in the same language as your query.

### Slack Integration

Refer to the original documentation for setting up Slack integration.
