namespace Quela.Reactive.Core.Validation;

/// <summary>
/// Interface for validation rules that can be applied to values.
/// </summary>
public interface IValidationRule<T>
{
    /// <summary>
    /// Unique name for this rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Error message template when validation fails.
    /// </summary>
    string ErrorMessage { get; }

    /// <summary>
    /// Validates the value synchronously.
    /// </summary>
    ValidationResult Validate(T value);
}

/// <summary>
/// Interface for async validation rules (e.g., server-side checks).
/// </summary>
public interface IAsyncValidationRule<T> : IValidationRule<T>
{
    /// <summary>
    /// Validates the value asynchronously.
    /// </summary>
    Task<ValidationResult> ValidateAsync(T value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for simple validation rules.
/// </summary>
public abstract class ValidationRule<T> : IValidationRule<T>
{
    public abstract string Name { get; }
    public abstract string ErrorMessage { get; }

    public abstract ValidationResult Validate(T value);

    protected ValidationResult Success() => ValidationResult.Valid();

    protected ValidationResult Failure(string? message = null, string? code = null) =>
        ValidationResult.Invalid(message ?? ErrorMessage, code ?? Name);
}

/// <summary>
/// Predicate-based validation rule.
/// </summary>
public sealed class PredicateRule<T> : ValidationRule<T>
{
    private readonly Func<T, bool> _predicate;

    public override string Name { get; }
    public override string ErrorMessage { get; }

    public PredicateRule(string name, string errorMessage, Func<T, bool> predicate)
    {
        Name = name;
        ErrorMessage = errorMessage;
        _predicate = predicate;
    }

    public override ValidationResult Validate(T value) =>
        _predicate(value) ? Success() : Failure();
}

/// <summary>
/// Collection of common validation rules.
/// </summary>
public static class ValidationRules
{
    /// <summary>
    /// Creates a required (non-null/non-empty) rule.
    /// </summary>
    public static IValidationRule<string> Required(string? message = null) =>
        new PredicateRule<string>(
            "Required",
            message ?? "This field is required",
            value => !string.IsNullOrWhiteSpace(value));

    /// <summary>
    /// Creates a minimum length rule.
    /// </summary>
    public static IValidationRule<string> MinLength(int length, string? message = null) =>
        new PredicateRule<string>(
            "MinLength",
            message ?? $"Must be at least {length} characters",
            value => value?.Length >= length);

    /// <summary>
    /// Creates a maximum length rule.
    /// </summary>
    public static IValidationRule<string> MaxLength(int length, string? message = null) =>
        new PredicateRule<string>(
            "MaxLength",
            message ?? $"Must be at most {length} characters",
            value => value == null || value.Length <= length);

    /// <summary>
    /// Creates an email format rule.
    /// </summary>
    public static IValidationRule<string> Email(string? message = null) =>
        new PredicateRule<string>(
            "Email",
            message ?? "Must be a valid email address",
            value => string.IsNullOrEmpty(value) ||
                     System.Text.RegularExpressions.Regex.IsMatch(
                         value,
                         @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));

    /// <summary>
    /// Creates a regex pattern rule.
    /// </summary>
    public static IValidationRule<string> Pattern(string pattern, string? message = null) =>
        new PredicateRule<string>(
            "Pattern",
            message ?? $"Must match pattern: {pattern}",
            value => string.IsNullOrEmpty(value) ||
                     System.Text.RegularExpressions.Regex.IsMatch(value, pattern));

    /// <summary>
    /// Creates a numeric range rule.
    /// </summary>
    public static IValidationRule<T> Range<T>(T min, T max, string? message = null)
        where T : IComparable<T> =>
        new PredicateRule<T>(
            "Range",
            message ?? $"Must be between {min} and {max}",
            value => value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0);

    /// <summary>
    /// Creates a custom predicate rule.
    /// </summary>
    public static IValidationRule<T> Custom<T>(string name, string errorMessage, Func<T, bool> predicate) =>
        new PredicateRule<T>(name, errorMessage, predicate);

    /// <summary>
    /// Combines multiple rules into one.
    /// </summary>
    public static IValidationRule<T> Combine<T>(params IValidationRule<T>[] rules) =>
        new CompositeRule<T>(rules);
}

/// <summary>
/// Composite rule that combines multiple rules.
/// </summary>
internal sealed class CompositeRule<T> : IValidationRule<T>
{
    private readonly IValidationRule<T>[] _rules;

    public string Name => "Composite";
    public string ErrorMessage => "Validation failed";

    public CompositeRule(IValidationRule<T>[] rules)
    {
        _rules = rules;
    }

    public ValidationResult Validate(T value)
    {
        var results = _rules.Select(r => r.Validate(value)).ToArray();
        return ValidationResult.Combine(results);
    }
}
