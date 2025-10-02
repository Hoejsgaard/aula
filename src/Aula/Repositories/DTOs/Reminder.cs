using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Aula.Repositories.DTOs;

[Table("reminders")]
public class Reminder : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("text")]
    public string Text { get; set; } = string.Empty;

    [Column("remind_date")]
    public DateOnly RemindDate { get; set; }

    [Column("remind_time")]
    public TimeOnly RemindTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("is_sent")]
    public bool IsSent { get; set; }

    [Column("child_name")]
    public string? ChildName { get; set; }

    [Column("created_by")]
    public string CreatedBy { get; set; } = "bot";
}