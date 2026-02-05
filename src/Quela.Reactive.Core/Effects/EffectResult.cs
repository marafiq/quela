namespace Quela.Reactive.Core.Effects;

/// <summary>
/// Represents the result of an effect execution.
/// </summary>
public record EffectResult
{
    /// <summary>
    /// Whether the effect completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error information if the effect failed.
    /// </summary>
    public EffectError? Error { get; init; }

    /// <summary>
    /// The boxed result value (null for void effects).
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Duration of the effect execution.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of retry attempts before success/failure.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Whether this result was served from the idempotency cache.
    /// </summary>
    public bool WasCached { get; init; }

    /// <summary>
    /// Additional metadata about the execution.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a successful result with no value.
    /// </summary>
    public static EffectResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static EffectResult Success(object? value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static EffectResult Failure(EffectError error) =>
        new() { IsSuccess = false, Error = error };

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static EffectResult Failure(Exception exception) =>
        Failure(EffectError.FromException(exception));

    /// <summary>
    /// Creates a cancelled result.
    /// </summary>
    public static EffectResult Cancelled() =>
        Failure(new EffectError(
            "OperationCancelled",
            "The operation was cancelled",
            isCancellation: true));
}

/// <summary>
/// Strongly-typed effect result.
/// </summary>
public sealed record EffectResult<T> : EffectResult
{
    /// <summary>
    /// The typed result value.
    /// </summary>
    public new T? TypedValue => (T?)Value;

    /// <summary>
    /// Creates a successful result with a typed value.
    /// </summary>
    public static EffectResult<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public new static EffectResult<T> Failure(EffectError error) =>
        new() { IsSuccess = false, Error = error };

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public new static EffectResult<T> Failure(Exception exception) =>
        Failure(EffectError.FromException(exception));

    /// <summary>
    /// Creates a cancelled result.
    /// </summary>
    public new static EffectResult<T> Cancelled() =>
        Failure(new EffectError(
            "OperationCancelled",
            "The operation was cancelled",
            isCancellation: true));

    /// <summary>
    /// Maps the result value if successful.
    /// </summary>
    public EffectResult<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (!IsSuccess)
            return EffectResult<TNew>.Failure(Error!);

        return EffectResult<TNew>.Success(mapper(TypedValue!));
    }
}

/// <summary>
/// Represents an error from effect execution.
/// </summary>
public sealed record EffectError(
    string Code,
    string Message,
    Exception? Exception = null,
    bool IsRetryable = false,
    bool IsCancellation = false,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Creates an error from an exception.
    /// </summary>
    public static EffectError FromException(Exception ex) =>
        new(
            ex.GetType().Name,
            ex.Message,
            ex,
            IsRetryable: ex is TimeoutException or System.Net.Http.HttpRequestException,
            IsCancellation: ex is OperationCanceledException);
}

/// <summary>
/// Retry policy configuration for effects.
/// </summary>
public sealed record RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Initial delay before first retry.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Exception types that are retryable.
    /// </summary>
    public IReadOnlySet<Type> RetryableExceptions { get; init; } = new HashSet<Type>
    {
        typeof(TimeoutException),
        typeof(System.Net.Http.HttpRequestException),
        typeof(System.Net.Sockets.SocketException)
    };

    /// <summary>
    /// Custom predicate for determining if an error is retryable.
    /// </summary>
    public Func<Exception, bool>? RetryPredicate { get; init; }

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker.
    /// </summary>
    public int CircuitBreakerThreshold { get; init; } = 5;

    /// <summary>
    /// Duration to keep circuit breaker open.
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to add jitter to retry delays.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Default retry policy.
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// No retry policy.
    /// </summary>
    public static RetryPolicy None => new() { MaxRetries = 0 };

    /// <summary>
    /// Aggressive retry policy for critical operations.
    /// </summary>
    public static RetryPolicy Aggressive => new()
    {
        MaxRetries = 5,
        InitialDelay = TimeSpan.FromMilliseconds(50),
        MaxDelay = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2.5
    };

    /// <summary>
    /// Determines if an exception should trigger a retry.
    /// </summary>
    public bool ShouldRetry(Exception ex, int attemptNumber)
    {
        if (attemptNumber >= MaxRetries)
            return false;

        if (RetryPredicate != null)
            return RetryPredicate(ex);

        return RetryableExceptions.Any(type => type.IsInstanceOfType(ex));
    }

    /// <summary>
    /// Calculates the delay before the next retry attempt.
    /// </summary>
    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        var delay = TimeSpan.FromMilliseconds(
            InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attemptNumber));

        if (delay > MaxDelay)
            delay = MaxDelay;

        if (UseJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3; // 0-30% jitter
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (1 + jitter));
        }

        return delay;
    }
}
