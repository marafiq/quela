using Quela.Reactive.Core.Validation;

namespace Quela.Reactive.DSL.Extensions;

/// <summary>
/// Extension methods for string field validation.
/// </summary>
public static class StringFieldExtensions
{
    /// <summary>
    /// Adds a minimum length validation.
    /// </summary>
    public static ValueFieldBuilder<string> MinLength(
        this ValueFieldBuilder<string> builder,
        int length,
        string? message = null)
    {
        return builder.Validate(ValidationRules.MinLength(length, message));
    }

    /// <summary>
    /// Adds a maximum length validation.
    /// </summary>
    public static ValueFieldBuilder<string> MaxLength(
        this ValueFieldBuilder<string> builder,
        int length,
        string? message = null)
    {
        return builder.Validate(ValidationRules.MaxLength(length, message));
    }

    /// <summary>
    /// Adds an email format validation.
    /// </summary>
    public static ValueFieldBuilder<string> Email(
        this ValueFieldBuilder<string> builder,
        string? message = null)
    {
        return builder.Validate(ValidationRules.Email(message));
    }

    /// <summary>
    /// Adds a regex pattern validation.
    /// </summary>
    public static ValueFieldBuilder<string> Pattern(
        this ValueFieldBuilder<string> builder,
        string pattern,
        string? message = null)
    {
        return builder.Validate(ValidationRules.Pattern(pattern, message));
    }

    /// <summary>
    /// Adds a URL format validation.
    /// </summary>
    public static ValueFieldBuilder<string> Url(
        this ValueFieldBuilder<string> builder,
        string? message = null)
    {
        return builder.Validate(ValidationRules.Pattern(
            @"^https?://[^\s/$.?#].[^\s]*$",
            message ?? "Must be a valid URL"));
    }

    /// <summary>
    /// Adds a phone number validation.
    /// </summary>
    public static ValueFieldBuilder<string> Phone(
        this ValueFieldBuilder<string> builder,
        string? message = null)
    {
        return builder.Validate(ValidationRules.Pattern(
            @"^[\d\s\-\+\(\)]+$",
            message ?? "Must be a valid phone number"));
    }

    /// <summary>
    /// Adds alphanumeric validation.
    /// </summary>
    public static ValueFieldBuilder<string> Alphanumeric(
        this ValueFieldBuilder<string> builder,
        string? message = null)
    {
        return builder.Validate(ValidationRules.Pattern(
            @"^[a-zA-Z0-9]+$",
            message ?? "Must contain only letters and numbers"));
    }

    /// <summary>
    /// Adds a credit card number validation (Luhn algorithm).
    /// </summary>
    public static ValueFieldBuilder<string> CreditCard(
        this ValueFieldBuilder<string> builder,
        string? message = null)
    {
        return builder.Validate(new PredicateRule<string>(
            "CreditCard",
            message ?? "Must be a valid credit card number",
            value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                    return true; // Let Required handle empty

                var digits = value.Where(char.IsDigit).ToArray();
                if (digits.Length < 13 || digits.Length > 19)
                    return false;

                // Luhn algorithm
                var sum = 0;
                var alternate = false;
                for (var i = digits.Length - 1; i >= 0; i--)
                {
                    var digit = digits[i] - '0';
                    if (alternate)
                    {
                        digit *= 2;
                        if (digit > 9) digit -= 9;
                    }
                    sum += digit;
                    alternate = !alternate;
                }
                return sum % 10 == 0;
            }));
    }
}

/// <summary>
/// Extension methods for numeric field validation.
/// </summary>
public static class NumericFieldExtensions
{
    /// <summary>
    /// Adds a minimum value validation.
    /// </summary>
    public static ValueFieldBuilder<T> Min<T>(
        this ValueFieldBuilder<T> builder,
        T minValue,
        string? message = null) where T : IComparable<T>
    {
        return builder.Validate(
            "Min",
            message ?? $"Must be at least {minValue}",
            value => value.CompareTo(minValue) >= 0);
    }

    /// <summary>
    /// Adds a maximum value validation.
    /// </summary>
    public static ValueFieldBuilder<T> Max<T>(
        this ValueFieldBuilder<T> builder,
        T maxValue,
        string? message = null) where T : IComparable<T>
    {
        return builder.Validate(
            "Max",
            message ?? $"Must be at most {maxValue}",
            value => value.CompareTo(maxValue) <= 0);
    }

    /// <summary>
    /// Adds a range validation.
    /// </summary>
    public static ValueFieldBuilder<T> Range<T>(
        this ValueFieldBuilder<T> builder,
        T minValue,
        T maxValue,
        string? message = null) where T : IComparable<T>
    {
        return builder.Validate(ValidationRules.Range(minValue, maxValue, message));
    }

    /// <summary>
    /// Adds a positive value validation.
    /// </summary>
    public static ValueFieldBuilder<int> Positive(
        this ValueFieldBuilder<int> builder,
        string? message = null)
    {
        return builder.Min(1, message ?? "Must be a positive number");
    }

    /// <summary>
    /// Adds a positive value validation.
    /// </summary>
    public static ValueFieldBuilder<decimal> Positive(
        this ValueFieldBuilder<decimal> builder,
        string? message = null)
    {
        return builder.Validate(
            "Positive",
            message ?? "Must be a positive number",
            value => value > 0);
    }
}

/// <summary>
/// Extension methods for collection field validation.
/// </summary>
public static class CollectionFieldExtensions
{
    /// <summary>
    /// Adds a non-empty validation.
    /// </summary>
    public static ValueFieldBuilder<IReadOnlyList<T>> NotEmpty<T>(
        this ValueFieldBuilder<IReadOnlyList<T>> builder,
        string? message = null)
    {
        return builder.Validate(
            "NotEmpty",
            message ?? "Must have at least one item",
            value => value?.Count > 0);
    }

    /// <summary>
    /// Adds a minimum count validation.
    /// </summary>
    public static ValueFieldBuilder<IReadOnlyList<T>> MinCount<T>(
        this ValueFieldBuilder<IReadOnlyList<T>> builder,
        int count,
        string? message = null)
    {
        return builder.Validate(
            "MinCount",
            message ?? $"Must have at least {count} items",
            value => value?.Count >= count);
    }

    /// <summary>
    /// Adds a maximum count validation.
    /// </summary>
    public static ValueFieldBuilder<IReadOnlyList<T>> MaxCount<T>(
        this ValueFieldBuilder<IReadOnlyList<T>> builder,
        int count,
        string? message = null)
    {
        return builder.Validate(
            "MaxCount",
            message ?? $"Must have at most {count} items",
            value => value == null || value.Count <= count);
    }
}
