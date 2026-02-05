using System.Collections.Concurrent;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Validation;

namespace Quela.Reactive.Runtime.State;

/// <summary>
/// Mutable state for a single session (user interaction context).
/// Each session maintains its own state snapshot.
/// </summary>
public sealed class SessionState
{
    private readonly ConcurrentDictionary<NodeId, NodeState> _nodeStates = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationScopes = new();
    private readonly object _lock = new();

    /// <summary>
    /// Session identifier.
    /// </summary>
    public SessionId SessionId { get; }

    /// <summary>
    /// The reactive graph this session is executing.
    /// </summary>
    public ReactiveGraph Graph { get; }

    /// <summary>
    /// When this session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>
    /// Current sequence number for ordering.
    /// </summary>
    public SequenceGenerator SequenceGenerator { get; } = new(1);

    /// <summary>
    /// IDs of currently dirty nodes.
    /// </summary>
    public ConcurrentDictionary<NodeId, byte> DirtyNodes { get; } = new();

    /// <summary>
    /// Custom session data.
    /// </summary>
    public ConcurrentDictionary<string, object> CustomData { get; } = new();

    public SessionState(SessionId sessionId, ReactiveGraph graph)
    {
        SessionId = sessionId;
        Graph = graph;
        CreatedAt = DateTimeOffset.UtcNow;
        LastActivityAt = CreatedAt;

        // Initialize node states
        foreach (var (nodeId, node) in graph.Nodes)
        {
            _nodeStates[nodeId] = new NodeState(nodeId);
        }
    }

    /// <summary>
    /// Gets the state for a specific node.
    /// </summary>
    public NodeState GetNodeState(NodeId nodeId)
    {
        if (!_nodeStates.TryGetValue(nodeId, out var state))
            throw new InvalidOperationException($"Node '{nodeId}' not found in session");
        return state;
    }

    /// <summary>
    /// Gets or creates a cancellation scope.
    /// </summary>
    public CancellationToken GetCancellationToken(string scope)
    {
        var cts = _cancellationScopes.GetOrAdd(scope, _ => new CancellationTokenSource());
        return cts.Token;
    }

    /// <summary>
    /// Cancels a specific scope.
    /// </summary>
    public void CancelScope(string scope)
    {
        if (_cancellationScopes.TryRemove(scope, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Cancels all active scopes.
    /// </summary>
    public void CancelAllScopes()
    {
        foreach (var scope in _cancellationScopes.Keys.ToList())
        {
            CancelScope(scope);
        }
    }

    /// <summary>
    /// Updates the last activity timestamp.
    /// </summary>
    public void Touch()
    {
        LastActivityAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the value of a node.
    /// </summary>
    public T? GetValue<T>(NodeId nodeId)
    {
        var state = GetNodeState(nodeId);
        return state.Value is T typedValue ? typedValue : default;
    }

    /// <summary>
    /// Sets the value of a node.
    /// </summary>
    public void SetValue(NodeId nodeId, object? value)
    {
        var state = GetNodeState(nodeId);
        state.SetValue(value, SequenceGenerator.Next());
        MarkDirty(nodeId);
    }

    /// <summary>
    /// Marks a node as dirty.
    /// </summary>
    public void MarkDirty(NodeId nodeId)
    {
        DirtyNodes.TryAdd(nodeId, 0);

        // Propagate to dependents
        foreach (var dependentId in Graph.GetDependents(nodeId))
        {
            MarkDirty(dependentId);
        }
    }

    /// <summary>
    /// Marks a node as clean.
    /// </summary>
    public void MarkClean(NodeId nodeId)
    {
        DirtyNodes.TryRemove(nodeId, out _);
    }

    /// <summary>
    /// Gets all dirty nodes.
    /// </summary>
    public IReadOnlySet<NodeId> GetDirtyNodes() =>
        DirtyNodes.Keys.ToHashSet();

    /// <summary>
    /// Creates a snapshot of current state for a transaction.
    /// </summary>
    public StateSnapshot CreateSnapshot()
    {
        var values = new Dictionary<NodeId, object?>();
        foreach (var (nodeId, state) in _nodeStates)
        {
            values[nodeId] = state.Value;
        }
        return new StateSnapshot(values, SequenceGenerator.Current);
    }

    /// <summary>
    /// Restores state from a snapshot (for rollback).
    /// </summary>
    public void RestoreSnapshot(StateSnapshot snapshot)
    {
        foreach (var (nodeId, value) in snapshot.Values)
        {
            if (_nodeStates.TryGetValue(nodeId, out var state))
            {
                state.SetValue(value, SequenceGenerator.Next());
            }
        }
    }
}

/// <summary>
/// State for a single node.
/// </summary>
public sealed class NodeState
{
    private readonly object _lock = new();
    private object? _value;
    private ValidationResult _validationResult = ValidationResult.Pending;

    public NodeId NodeId { get; }
    public bool IsDirty { get; private set; } = true;
    public bool IsComputing { get; private set; }
    public SequenceNumber LastUpdatedSequence { get; private set; }
    public DateTimeOffset LastUpdatedAt { get; private set; }

    public object? Value
    {
        get { lock (_lock) return _value; }
    }

    public ValidationResult ValidationResult
    {
        get { lock (_lock) return _validationResult; }
    }

    public NodeState(NodeId nodeId)
    {
        NodeId = nodeId;
    }

    public void SetValue(object? value, SequenceNumber sequence)
    {
        lock (_lock)
        {
            _value = value;
            LastUpdatedSequence = sequence;
            LastUpdatedAt = DateTimeOffset.UtcNow;
            IsDirty = false;
        }
    }

    public void SetValidationResult(ValidationResult result)
    {
        lock (_lock)
        {
            _validationResult = result;
        }
    }

    public void MarkDirty()
    {
        lock (_lock)
        {
            IsDirty = true;
        }
    }

    public void BeginCompute()
    {
        lock (_lock)
        {
            IsComputing = true;
        }
    }

    public void EndCompute()
    {
        lock (_lock)
        {
            IsComputing = false;
        }
    }
}

/// <summary>
/// Snapshot of state for transactional operations.
/// </summary>
public sealed record StateSnapshot(
    IReadOnlyDictionary<NodeId, object?> Values,
    SequenceNumber Sequence);
