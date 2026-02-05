using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Graph;

/// <summary>
/// Base interface for all nodes in the reactive graph.
/// Nodes are the fundamental units of computation and state.
/// </summary>
public interface INode
{
    /// <summary>
    /// Unique identifier for this node within the graph.
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// Human-readable name for debugging and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of this node (Value, Computed, Effect, etc.).
    /// </summary>
    NodeType Type { get; }

    /// <summary>
    /// IDs of nodes this node depends on (upstream).
    /// </summary>
    IReadOnlySet<NodeId> Dependencies { get; }

    /// <summary>
    /// IDs of nodes that depend on this node (downstream).
    /// </summary>
    IReadOnlySet<NodeId> Dependents { get; }

    /// <summary>
    /// Metadata associated with this node.
    /// </summary>
    NodeMetadata Metadata { get; }
}

/// <summary>
/// Enumeration of node types in the reactive graph.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Holds a mutable value (e.g., form field).
    /// </summary>
    Value,

    /// <summary>
    /// Derives value from dependencies through pure computation.
    /// </summary>
    Computed,

    /// <summary>
    /// Represents a side effect (network request, I/O).
    /// </summary>
    Effect,

    /// <summary>
    /// Routes execution based on conditions.
    /// </summary>
    Conditional,

    /// <summary>
    /// Aggregates multiple sources (fan-in).
    /// </summary>
    Aggregator,

    /// <summary>
    /// Represents a nested partial with its own sub-graph.
    /// </summary>
    Partial,

    /// <summary>
    /// Triggers downstream computation on events.
    /// </summary>
    Trigger,

    /// <summary>
    /// Validates data and produces validation results.
    /// </summary>
    Validation
}

/// <summary>
/// Metadata associated with a node for configuration and introspection.
/// </summary>
public sealed record NodeMetadata
{
    /// <summary>
    /// CSS selector or element ID for UI binding.
    /// </summary>
    public string? TargetSelector { get; init; }

    /// <summary>
    /// Custom tags for categorization.
    /// </summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();

    /// <summary>
    /// Debounce delay for value changes.
    /// </summary>
    public TimeSpan? DebounceDelay { get; init; }

    /// <summary>
    /// Throttle interval for updates.
    /// </summary>
    public TimeSpan? ThrottleInterval { get; init; }

    /// <summary>
    /// Priority level for scheduling (higher = sooner).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Whether this node's value should be persisted across sessions.
    /// </summary>
    public bool IsPersistent { get; init; }

    /// <summary>
    /// Whether this node is visible in diagnostics.
    /// </summary>
    public bool IsInternal { get; init; }

    /// <summary>
    /// Custom data for extensibility.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomData { get; init; } =
        new Dictionary<string, object>();

    public static NodeMetadata Default => new();
}

/// <summary>
/// Interface for nodes that hold state.
/// </summary>
public interface IStatefulNode : INode
{
    /// <summary>
    /// The type of value this node holds.
    /// </summary>
    Type ValueType { get; }

    /// <summary>
    /// Gets the current value (boxed).
    /// </summary>
    object? GetValue();
}

/// <summary>
/// Generic interface for strongly-typed stateful nodes.
/// </summary>
public interface IStatefulNode<T> : IStatefulNode
{
    /// <summary>
    /// Gets the current typed value.
    /// </summary>
    new T Value { get; }
}

/// <summary>
/// Interface for nodes that can be executed.
/// </summary>
public interface IExecutableNode : INode
{
    /// <summary>
    /// Whether this node can execute in parallel with others.
    /// </summary>
    bool IsParallelizable { get; }

    /// <summary>
    /// Estimated execution cost for scheduling optimization.
    /// </summary>
    int ExecutionCost { get; }
}
