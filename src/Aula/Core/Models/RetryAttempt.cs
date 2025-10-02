using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Aula.Core.Models;

[Table("retry_attempts")]
public class RetryAttempt : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("child_name")]
    public string ChildName { get; set; } = string.Empty;

    [Column("week_number")]
    public int WeekNumber { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [Column("last_attempt")]
    public DateTime LastAttempt { get; set; }

    [Column("next_attempt")]
    public DateTime? NextAttempt { get; set; }

    [Column("max_attempts")]
    public int MaxAttempts { get; set; }

    [Column("is_successful")]
    public bool IsSuccessful { get; set; }
}
