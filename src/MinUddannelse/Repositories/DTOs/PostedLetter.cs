using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MinUddannelse.Repositories.DTOs;

[Table("posted_letters")]
public class PostedLetter : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("child_name")]
    public string ChildName { get; set; } = string.Empty;

    [Column("week_number")]
    public int WeekNumber { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [Column("posted_at")]
    public DateTime PostedAt { get; set; }

    [Column("posted_to_slack")]
    public bool PostedToSlack { get; set; }

    [Column("posted_to_telegram")]
    public bool PostedToTelegram { get; set; }

    [Column("raw_content")]
    public string? RawContent { get; set; }
}