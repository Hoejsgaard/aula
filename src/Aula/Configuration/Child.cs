namespace Aula.Configuration;

public class Child
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Colour { get; set; } = string.Empty;
    public string GoogleCalendarId { get; set; } = string.Empty;
    public UniLogin? UniLogin { get; set; }
    public ChildChannels? Channels { get; set; }
}

public class ChildChannels
{
    public ChildSlackConfig? Slack { get; set; }
    public ChildTelegramConfig? Telegram { get; set; }
    public ChildGoogleCalendarConfig? GoogleCalendar { get; set; }
}

public class ChildSlackConfig
{
    public bool Enabled { get; set; }
    public string? WebhookUrl { get; set; }
    public string? ApiToken { get; set; }
    public string? ChannelId { get; set; }
    public bool EnableInteractiveBot { get; set; }
}

public class ChildTelegramConfig
{
    public bool Enabled { get; set; }
    public string? Token { get; set; }
    public string? ChannelId { get; set; }
    public bool EnableInteractiveBot { get; set; }
}

public class ChildGoogleCalendarConfig
{
    public bool Enabled { get; set; }
    public string? CalendarId { get; set; }
}