namespace MinUddannelse.Configuration;

public interface IConfigurationValidator
{
    Task<ValidationResult> ValidateConfigurationAsync(Config config);
}

public class ValidationResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static ValidationResult Success(IEnumerable<string>? warnings = null) =>
        new() { IsValid = true, Warnings = (warnings?.ToList() ?? new List<string>()).AsReadOnly() };

    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null) =>
        new() { IsValid = false, Errors = errors.ToList().AsReadOnly(), Warnings = (warnings?.ToList() ?? new List<string>()).AsReadOnly() };
}
