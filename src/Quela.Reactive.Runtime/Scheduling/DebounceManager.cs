using System.Collections.Concurrent;
using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Runtime.Scheduling;

/// <summary>
/// Manages debouncing for node updates.
/// Prevents excessive recomputation during rapid changes.
/// </summary>
public sealed class DebounceManager : IDisposable
{
    private readonly ConcurrentDictionary<NodeId, DebounceState> _debounceStates = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public DebounceManager()
    {
        _cleanupTimer = new Timer(
            CleanupExpired,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Debounces an action for a node.
    /// </summary>
    public async Task<bool> DebounceAsync(
        NodeId nodeId,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        var state = _debounceStates.GetOrAdd(nodeId, _ => new DebounceState());

        lock (state)
        {
            // Cancel any pending debounce
            state.CancellationTokenSource?.Cancel();
            state.CancellationTokenSource?.Dispose();
            state.CancellationTokenSource = new CancellationTokenSource();
            state.LastTriggerTime = DateTimeOffset.UtcNow;
        }

        var localCts = state.CancellationTokenSource;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                localCts.Token, cancellationToken);

            await Task.Delay(delay, linkedCts.Token);

            // Check if we're still the latest trigger
            lock (state)
            {
                if (state.CancellationTokenSource == localCts)
                {
                    state.CancellationTokenSource = null;
                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Throttles an action for a node (rate limiting).
    /// </summary>
    public bool TryThrottle(NodeId nodeId, TimeSpan interval)
    {
        var state = _debounceStates.GetOrAdd(nodeId, _ => new DebounceState());

        lock (state)
        {
            var now = DateTimeOffset.UtcNow;
            if (state.LastExecutionTime == null ||
                now - state.LastExecutionTime.Value >= interval)
            {
                state.LastExecutionTime = now;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Cancels any pending debounce for a node.
    /// </summary>
    public void Cancel(NodeId nodeId)
    {
        if (_debounceStates.TryGetValue(nodeId, out var state))
        {
            lock (state)
            {
                state.CancellationTokenSource?.Cancel();
                state.CancellationTokenSource?.Dispose();
                state.CancellationTokenSource = null;
            }
        }
    }

    /// <summary>
    /// Cancels all pending debounces.
    /// </summary>
    public void CancelAll()
    {
        foreach (var nodeId in _debounceStates.Keys.ToList())
        {
            Cancel(nodeId);
        }
    }

    private void CleanupExpired(object? state)
    {
        var expiredThreshold = DateTimeOffset.UtcNow.AddMinutes(-5);

        var expiredKeys = _debounceStates
            .Where(kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.CancellationTokenSource == null &&
                           kvp.Value.LastTriggerTime < expiredThreshold;
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _debounceStates.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        CancelAll();
    }

    private sealed class DebounceState
    {
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public DateTimeOffset LastTriggerTime { get; set; }
        public DateTimeOffset? LastExecutionTime { get; set; }
    }
}
