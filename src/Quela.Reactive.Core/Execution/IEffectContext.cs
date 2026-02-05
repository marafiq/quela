using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Effects;

namespace Quela.Reactive.Core.Execution;

/// <summary>
/// Context provided to effect nodes during execution.
/// Provides access to dependencies, cancellation, and effect infrastructure.
/// </summary>
public interface IEffectContext : IDependencyReader
{
    /// <summary>
    /// Cancellation token for this effect execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// HTTP client factory for making HTTP requests.
    /// </summary>
    IHttpClientFactory HttpClientFactory { get; }

    /// <summary>
    /// Service provider for resolving dependencies.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Logger for effect diagnostics.
    /// </summary>
    IEffectLogger Logger { get; }

    /// <summary>
    /// Current retry attempt number (0 for first attempt).
    /// </summary>
    int AttemptNumber { get; }

    /// <summary>
    /// Time remaining before timeout.
    /// </summary>
    TimeSpan RemainingTimeout { get; }

    /// <summary>
    /// Registers a cleanup action to run on completion/cancellation.
    /// </summary>
    void RegisterCleanup(Func<Task> cleanup);

    /// <summary>
    /// Reports progress for long-running effects.
    /// </summary>
    void ReportProgress(double progress, string? message = null);

    /// <summary>
    /// Yields to allow other effects to run (cooperative multitasking).
    /// </summary>
    Task YieldAsync();
}

/// <summary>
/// Logger interface for effects.
/// </summary>
public interface IEffectLogger
{
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Error(string message, Exception? ex = null, params object[] args);
}

/// <summary>
/// HTTP client factory interface.
/// </summary>
public interface IHttpClientFactory
{
    HttpClient CreateClient(string? name = null);
}

/// <summary>
/// Default implementation of HTTP client factory.
/// </summary>
public sealed class DefaultHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients = new();
    private readonly object _lock = new();

    public HttpClient CreateClient(string? name = null)
    {
        var key = name ?? "";

        lock (_lock)
        {
            if (!_clients.TryGetValue(key, out var client))
            {
                client = new HttpClient();
                _clients[key] = client;
            }
            return client;
        }
    }
}

/// <summary>
/// Progress report for long-running effects.
/// </summary>
public sealed record EffectProgress(
    double Progress,
    string? Message = null,
    DateTimeOffset Timestamp = default)
{
    public EffectProgress(double progress, string? message = null)
        : this(progress, message, DateTimeOffset.UtcNow) { }
}

/// <summary>
/// Idempotency key for effect deduplication.
/// </summary>
public readonly record struct IdempotencyKey
{
    private readonly string _value;

    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public string Value => _value;

    public static IdempotencyKey Create(string value) => new(value);

    public static IdempotencyKey Generate() => new(Guid.NewGuid().ToString("N"));

    public static IdempotencyKey FromParts(params object[] parts) =>
        new(string.Join(":", parts.Select(p => p?.ToString() ?? "null")));

    public override string ToString() => _value;
}

/// <summary>
/// Cached effect result for idempotency.
/// </summary>
public sealed record CachedEffectResult(
    IdempotencyKey Key,
    EffectResult Result,
    DateTimeOffset CachedAt,
    TimeSpan? Ttl = null)
{
    public bool IsExpired => Ttl.HasValue && DateTimeOffset.UtcNow > CachedAt + Ttl.Value;
}
