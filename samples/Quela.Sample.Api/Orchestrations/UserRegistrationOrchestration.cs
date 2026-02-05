using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Effects;
using Quela.Reactive.Core.Validation;
using Quela.Reactive.DSL;
using Quela.Reactive.DSL.Extensions;

namespace Quela.Sample.Api.Orchestrations;

/// <summary>
/// User registration form orchestration.
/// Demonstrates validation, computed fields, and effects.
/// </summary>
public static class UserRegistrationOrchestration
{
    public static ReactiveGraph Build()
    {
        return OrchestrationBuilder.Create("user-registration", "User Registration Form", "1.0.0")
            // Email field with debounced async validation
            .Field<string>("email")
                .Default("")
                .Required("Email is required")
                .Email("Please enter a valid email address")
                .BindTo("#email")
                .Debounce(300)
                .Add()

            // Password field with strength requirements
            .Field<string>("password")
                .Default("")
                .Required("Password is required")
                .MinLength(8, "Password must be at least 8 characters")
                .Validate("PasswordStrength", "Password must contain uppercase, lowercase, and number",
                    value => value != null &&
                             value.Any(char.IsUpper) &&
                             value.Any(char.IsLower) &&
                             value.Any(char.IsDigit))
                .BindTo("#password")
                .Add()

            // Password confirmation
            .Field<string>("confirmPassword")
                .Default("")
                .Required("Please confirm your password")
                .BindTo("#confirm-password")
                .Add()

            // First name
            .Field<string>("firstName")
                .Default("")
                .Required("First name is required")
                .MinLength(2, "First name must be at least 2 characters")
                .BindTo("#first-name")
                .Add()

            // Last name
            .Field<string>("lastName")
                .Default("")
                .Required("Last name is required")
                .MinLength(2, "Last name must be at least 2 characters")
                .BindTo("#last-name")
                .Add()

            // Computed: full name display
            .Computed<string>("fullName")
                .DependsOn("firstName", "lastName")
                .Compute(ctx =>
                {
                    var first = ctx.GetOrDefault<string>(new("firstName"), "");
                    var last = ctx.GetOrDefault<string>(new("lastName"), "");
                    return $"{first} {last}".Trim();
                })
                .BindTo("#full-name-display")
                .Add()

            // Computed: password match validation
            .Computed<bool>("passwordsMatch")
                .DependsOn("password", "confirmPassword")
                .Compute(ctx =>
                {
                    var password = ctx.GetOrDefault<string>(new("password"), "");
                    var confirm = ctx.GetOrDefault<string>(new("confirmPassword"), "");
                    return !string.IsNullOrEmpty(password) && password == confirm;
                })
                .Add()

            // Async validation: check if email exists
            .Validation<string>("emailUniqueness")
                .For("email")
                .ValidateAsync(async (ctx, email, ct) =>
                {
                    if (string.IsNullOrEmpty(email))
                        return ValidationResult.Valid();

                    // Simulate API call to check email uniqueness
                    await Task.Delay(500, ct);

                    // For demo: emails containing "taken" are considered taken
                    if (email.Contains("taken", StringComparison.OrdinalIgnoreCase))
                        return ValidationResult.Invalid("This email is already registered");

                    return ValidationResult.Valid();
                })
                .Debounce(TimeSpan.FromMilliseconds(500))
                .Add()

            // Conditional: show terms checkbox only when form is valid
            .When("showTerms")
                .DependsOn("email", "password", "confirmPassword", "firstName", "lastName", "passwordsMatch")
                .Condition(ctx =>
                {
                    // Show terms when basic fields are filled and passwords match
                    var email = ctx.GetOrDefault<string>(new("email"), "");
                    var password = ctx.GetOrDefault<string>(new("password"), "");
                    var firstName = ctx.GetOrDefault<string>(new("firstName"), "");
                    var lastName = ctx.GetOrDefault<string>(new("lastName"), "");
                    var passwordsMatch = ctx.GetOrDefault<bool>(new("passwordsMatch"), false);

                    return !string.IsNullOrEmpty(email) &&
                           !string.IsNullOrEmpty(password) &&
                           !string.IsNullOrEmpty(firstName) &&
                           !string.IsNullOrEmpty(lastName) &&
                           passwordsMatch;
                })
                .ThenActivate("termsAccepted")
                .Add()

            // Terms acceptance checkbox
            .Field<bool>("termsAccepted")
                .Default(false)
                .BindTo("#terms-accepted")
                .Add()

            // Submit trigger
            .Trigger("submit")
                .OnSubmit()
                .RequiresValid("email", "password", "firstName", "lastName")
                .BindTo("#submit-btn")
                .Add()

            // Registration effect
            .Effect<RegistrationResult>("registerUser")
                .DependsOn("submit", "email", "password", "firstName", "lastName")
                .WithRetry(3)
                .WithTimeout(30)
                .WithIdempotencyKey(ctx =>
                {
                    var email = ctx.GetOrDefault<string>(new("email"), "");
                    return $"register:{email}";
                })
                .ExecuteAsync(async ctx =>
                {
                    var email = ctx.Get<string>(new("email"));
                    var password = ctx.Get<string>(new("password"));
                    var firstName = ctx.Get<string>(new("firstName"));
                    var lastName = ctx.Get<string>(new("lastName"));

                    // Simulate API call
                    await Task.Delay(1000, ctx.CancellationToken);

                    ctx.Logger.Info("User registered: {Email}", email);

                    return new RegistrationResult
                    {
                        UserId = Guid.NewGuid().ToString(),
                        Email = email,
                        FullName = $"{firstName} {lastName}"
                    };
                })
                .Add()

            .Build();
    }

    public record RegistrationResult
    {
        public string UserId { get; init; } = "";
        public string Email { get; init; } = "";
        public string FullName { get; init; } = "";
    }
}
