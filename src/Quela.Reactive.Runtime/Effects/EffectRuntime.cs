using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Effects;
using Quela.Reactive.Core.Execution;
using Quela.Reactive.Core.Graph.Nodes;
using Quela.Reactive.Runtime.State;

namespace Quela.Reactive.Runtime.Effects;

/// <summary>
/// Runtime for executing effects with retry, timeout, and idempotency support.
/// </summary>
public sealed class EffectRuntime
{
    private readonly ILogger<EffectRuntime> _logger;
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<IdempotencyKey, CachedEffectResult> _idempotencyCache = new();
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly EffectRuntimeOptions _options;

    public EffectRuntime(
        ILogger<EffectRuntime> logger,
        IServiceProvider services,
        IHttpClientFactory? httpClientFactory = null,
        EffectRuntimeOptions? options = null)
    {
        _logger = logger;
        _services = services;
        _httpClientFactory = httpClientFactory ?? new DefaultHttpClientFactory();
        _options = options ?? new EffectRuntimeOptions();
        _concurrencySemaphore = new SemaphoreSlim(
            _options.MaxConcurrentEffects,
            _options.MaxConcurrentEffects);
    }

    /// <summary>
    /// Executes an effect with full lifecycle management.
    /// </summary>
    public async Task<EffectResult> ExecuteAsync(
        EffectNode effectNode,
        EffectExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // Check idempotency cache
        if (effectNode.IdempotencyKeyGenerator != null)
        {
            var key = new IdempotencyKey(effectNode.IdempotencyKeyGenerator(context));
            if (_idempotencyCache.TryGetValue(key, out var cached) && !cached.IsExpired)
            {
                _logger.LogDebug(
                    "Effect {EffectId} served from idempotency cache",
                    effectNode.Id);
                return cached.Result with { WasCached = true };
            }
        }

        // Check circuit breaker
        var circuitBreaker = GetCircuitBreaker(effectNode.Id.Value);
        if (circuitBreaker.IsOpen)
        {
            _logger.LogWarning(
                "Circuit breaker open for effect {EffectId}",
                effectNode.Id);
            return EffectResult.Failure(new EffectError(
                "CircuitBreakerOpen",
                "Too many recent failures",
                IsRetryable: true));
        }

        // Acquire concurrency semaphore
        await _concurrencySemaphore.WaitAsync(cancellationToken);

        try
        {
            return await ExecuteWithRetryAsync(effectNode, context, circuitBreaker, cancellationToken);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task<EffectResult> ExecuteWithRetryAsync(
        EffectNode effectNode,
        EffectExecutionContext context,
        CircuitBreakerState circuitBreaker,
        CancellationToken cancellationToken)
    {
        var retryPolicy = effectNode.RetryPolicy;
        var attemptNumber = 0;
        Exception? lastException = null;

        while (attemptNumber <= retryPolicy.MaxRetries)
        {
            try
            {
                var result = await ExecuteSingleAttemptAsync(
                    effectNode,
                    context,
                    attemptNumber,
                    cancellationToken);

                if (result.IsSuccess)
                {
                    circuitBreaker.RecordSuccess();

                    // Cache successful result
                    if (effectNode.IdempotencyKeyGenerator != null)
                    {
                        var key = new IdempotencyKey(effectNode.IdempotencyKeyGenerator(context));
                        var cached = new CachedEffectResult(
                            key,
                            result,
                            DateTimeOffset.UtcNow,
                            _options.IdempotencyCacheTtl);
                        _idempotencyCache[key] = cached;
                    }

                    return result;
                }

                // Non-retryable error
                if (result.Error != null && !result.Error.IsRetryable)
                {
                    circuitBreaker.RecordFailure();
                    return result;
                }

                lastException = result.Error?.Exception;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return EffectResult.Cancelled();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!retryPolicy.ShouldRetry(ex, attemptNumber))
                {
                    circuitBreaker.RecordFailure();
                    return EffectResult.Failure(ex) with { RetryCount = attemptNumber };
                }
            }

            attemptNumber++;

            if (attemptNumber <= retryPolicy.MaxRetries)
            {
                var delay = retryPolicy.GetRetryDelay(attemptNumber - 1);
                _logger.LogDebug(
                    "Retrying effect {EffectId} in {Delay}ms (attempt {Attempt})",
                    effectNode.Id, delay.TotalMilliseconds, attemptNumber);
                await Task.Delay(delay, cancellationToken);
            }
        }

        circuitBreaker.RecordFailure();
        return EffectResult.Failure(lastException ?? new Exception("Unknown error"))
            with { RetryCount = attemptNumber };
    }

    private async Task<EffectResult> ExecuteSingleAttemptAsync(
        EffectNode effectNode,
        EffectExecutionContext context,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(effectNode.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);

        var effectContext = new EffectContextImpl(
            context,
            linkedCts.Token,
            _httpClientFactory,
            _services,
            new EffectLoggerImpl(_logger, effectNode.Id),
            attemptNumber,
            effectNode.Timeout);

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await effectNode.EffectFunction(effectContext);
            sw.Stop();

            return result with
            {
                Duration = sw.Elapsed,
                RetryCount = attemptNumber
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            sw.Stop();
            return EffectResult.Failure(new EffectError(
                "Timeout",
                $"Effect timed out after {effectNode.Timeout.TotalSeconds}s",
                IsRetryable: true)) with { Duration = sw.Elapsed };
        }
    }

    private CircuitBreakerState GetCircuitBreaker(string key)
    {
        return _circuitBreakers.GetOrAdd(key, _ => new CircuitBreakerState(
            _options.CircuitBreakerThreshold,
            _options.CircuitBreakerDuration));
    }

    /// <summary>
    /// Clears the idempotency cache.
    /// </summary>
    public void ClearIdempotencyCache()
    {
        _idempotencyCache.Clear();
    }

    /// <summary>
    /// Resets all circuit breakers.
    /// </summary>
    public void ResetCircuitBreakers()
    {
        _circuitBreakers.Clear();
    }
}

/// <summary>
/// Context for effect execution.
/// </summary>
public sealed class EffectExecutionContext : IDependencyReader
{
    private readonly SessionState _sessionState;

    public TransactionId TransactionId { get; }
    public SessionId SessionId => _sessionState.SessionId;

    public EffectExecutionContext(
        SessionState sessionState,
        TransactionId transactionId)
    {
        _sessionState = sessionState;
        TransactionId = transactionId;
    }

    public T Get<T>(NodeId nodeId)
    {
        var value = _sessionState.GetNodeState(nodeId).Value;
        if (value is T typedValue)
            return typedValue;
        throw new InvalidOperationException(
            $"Node {nodeId} has value of type {value?.GetType().Name ?? "null"}, expected {typeof(T).Name}");
    }

    public bool TryGet<T>(NodeId nodeId, out T? value)
    {
        var raw = _sessionState.GetNodeState(nodeId).Value;
        if (raw is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public T GetOrDefault<T>(NodeId nodeId, T defaultValue = default!)
    {
        return TryGet<T>(nodeId, out var value) ? value! : defaultValue;
    }

    public object? GetRaw(NodeId nodeId)
    {
        return _sessionState.GetNodeState(nodeId).Value;
    }

    public bool HasValue(NodeId nodeId)
    {
        return _sessionState.GetNodeState(nodeId).Value != null;
    }

    public bool NodeExists(NodeId nodeId)
    {
        return _sessionState.Graph.ContainsNode(nodeId);
    }
}

/// <summary>
/// Implementation of IEffectContext.
/// </summary>
internal sealed class EffectContextImpl : IEffectContext
{
    private readonly EffectExecutionContext _context;
    private readonly List<Func<Task>> _cleanupActions = new();

    public CancellationToken CancellationToken { get; }
    public IHttpClientFactory HttpClientFactory { get; }
    public IServiceProvider Services { get; }
    public IEffectLogger Logger { get; }
    public int AttemptNumber { get; }
    public TimeSpan RemainingTimeout { get; }
    public TransactionId TransactionId => _context.TransactionId;
    public SessionId SessionId => _context.SessionId;

    public EffectContextImpl(
        EffectExecutionContext context,
        CancellationToken cancellationToken,
        IHttpClientFactory httpClientFactory,
        IServiceProvider services,
        IEffectLogger logger,
        int attemptNumber,
        TimeSpan timeout)
    {
        _context = context;
        CancellationToken = cancellationToken;
        HttpClientFactory = httpClientFactory;
        Services = services;
        Logger = logger;
        AttemptNumber = attemptNumber;
        RemainingTimeout = timeout;
    }

    public T Get<T>(NodeId nodeId) => _context.Get<T>(nodeId);
    public bool TryGet<T>(NodeId nodeId, out T? value) => _context.TryGet(nodeId, out value);
    public T GetOrDefault<T>(NodeId nodeId, T defaultValue = default!) =>
        _context.GetOrDefault(nodeId, defaultValue);
    public object? GetRaw(NodeId nodeId) => _context.GetRaw(nodeId);
    public bool HasValue(NodeId nodeId) => _context.HasValue(nodeId);
    public bool NodeExists(NodeId nodeId) => _context.NodeExists(nodeId);

    public void RegisterCleanup(Func<Task> cleanup)
    {
        _cleanupActions.Add(cleanup);
    }

    public void ReportProgress(double progress, string? message = null)
    {
        Logger.Debug("Progress: {Progress:P0} - {Message}", progress, message ?? "");
    }

    public async Task YieldAsync()
    {
        await Task.Yield();
    }
}

/// <summary>
/// Logger for effects.
/// </summary>
internal sealed class EffectLoggerImpl : IEffectLogger
{
    private readonly ILogger _logger;
    private readonly NodeId _effectId;

    public EffectLoggerImpl(ILogger logger, NodeId effectId)
    {
        _logger = logger;
        _effectId = effectId;
    }

    public void Debug(string message, params object[] args) =>
        _logger.LogDebug($"[Effect {_effectId}] {message}", args);

    public void Info(string message, params object[] args) =>
        _logger.LogInformation($"[Effect {_effectId}] {message}", args);

    public void Warning(string message, params object[] args) =>
        _logger.LogWarning($"[Effect {_effectId}] {message}", args);

    public void Error(string message, Exception? ex = null, params object[] args) =>
        _logger.LogError(ex, $"[Effect {_effectId}] {message}", args);
}

/// <summary>
/// Circuit breaker state.
/// </summary>
internal sealed class CircuitBreakerState
{
    private readonly int _threshold;
    private readonly TimeSpan _duration;
    private int _failureCount;
    private DateTimeOffset? _openedAt;
    private readonly object _lock = new();

    public CircuitBreakerState(int threshold, TimeSpan duration)
    {
        _threshold = threshold;
        _duration = duration;
    }

    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                if (_openedAt == null)
                    return false;

                if (DateTimeOffset.UtcNow - _openedAt.Value > _duration)
                {
                    // Half-open - allow one request through
                    _openedAt = null;
                    _failureCount = _threshold - 1;
                    return false;
                }

                return true;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _openedAt = null;
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            if (_failureCount >= _threshold)
            {
                _openedAt = DateTimeOffset.UtcNow;
            }
        }
    }
}

/// <summary>
/// Options for the effect runtime.
/// </summary>
public sealed record EffectRuntimeOptions
{
    public int MaxConcurrentEffects { get; init; } = 10;
    public TimeSpan IdempotencyCacheTtl { get; init; } = TimeSpan.FromMinutes(5);
    public int CircuitBreakerThreshold { get; init; } = 5;
    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);
}
