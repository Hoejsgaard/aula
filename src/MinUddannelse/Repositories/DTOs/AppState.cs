using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MinUddannelse.Repositories.DTOs;

[Table("app_state")]
public class AppState : BaseModel
{
    [PrimaryKey("key")]
    public string Key { get; set; } = string.Empty;

    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
