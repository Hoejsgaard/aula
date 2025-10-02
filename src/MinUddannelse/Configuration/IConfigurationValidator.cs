namespace MinUddannelse.Configuration;

/// <summary>
/// Provides configuration validation services to ensure application settings are valid and complete.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Asynchronously validates the provided configuration and returns validation results.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>A task that represents the asynchronous validation operation. The task result contains the validation outcome.</returns>
    Task<ValidationResult> ValidateConfigurationAsync(Config config);
}

/// <summary>
/// Represents the result of configuration validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the collection of validation errors, if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the collection of validation warnings, if any.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success(IEnumerable<string>? warnings = null) =>
        new() { IsValid = true, Warnings = (warnings?.ToList() ?? new List<string>()).AsReadOnly() };

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null) =>
        new() { IsValid = false, Errors = errors.ToList().AsReadOnly(), Warnings = (warnings?.ToList() ?? new List<string>()).AsReadOnly() };
}
