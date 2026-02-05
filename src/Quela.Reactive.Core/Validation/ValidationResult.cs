namespace Quela.Reactive.Core.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors (if any).
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    /// <summary>
    /// Validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = Array.Empty<ValidationWarning>();

    /// <summary>
    /// Additional metadata about the validation.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Valid() => new() { IsValid = true };

    /// <summary>
    /// Creates a successful result with warnings.
    /// </summary>
    public static ValidationResult ValidWithWarnings(params ValidationWarning[] warnings) =>
        new() { IsValid = true, Warnings = warnings };

    /// <summary>
    /// Creates an invalid result with errors.
    /// </summary>
    public static ValidationResult Invalid(params ValidationError[] errors) =>
        new() { IsValid = false, Errors = errors };

    /// <summary>
    /// Creates an invalid result with a single error message.
    /// </summary>
    public static ValidationResult Invalid(string errorMessage, string? errorCode = null) =>
        Invalid(new ValidationError(errorMessage, errorCode));

    /// <summary>
    /// Result indicating validation is pending/in-progress.
    /// </summary>
    public static ValidationResult Pending => new()
    {
        IsValid = false,
        Metadata = new Dictionary<string, object> { ["IsPending"] = true }
    };

    /// <summary>
    /// Whether this result represents a pending validation.
    /// </summary>
    public bool IsPending =>
        Metadata?.TryGetValue("IsPending", out var isPending) == true && isPending is true;

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var errors = results.SelectMany(r => r.Errors).ToList();
        var warnings = results.SelectMany(r => r.Warnings).ToList();

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
}

/// <summary>
/// Represents a validation error.
/// </summary>
public sealed record ValidationError(
    string Message,
    string? Code = null,
    string? Field = null,
    IReadOnlyDictionary<string, object>? Metadata = null);

/// <summary>
/// Represents a validation warning (non-blocking).
/// </summary>
public sealed record ValidationWarning(
    string Message,
    string? Code = null,
    string? Field = null);

/// <summary>
/// Represents the current validation state of a value.
/// </summary>
public enum ValidationState
{
    /// <summary>
    /// Validation has not yet been performed.
    /// </summary>
    Pending,

    /// <summary>
    /// Validation is currently in progress.
    /// </summary>
    Validating,

    /// <summary>
    /// Value is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// Value is invalid.
    /// </summary>
    Invalid,

    /// <summary>
    /// Validation was skipped.
    /// </summary>
    Skipped
}
